using Newtonsoft.Json;
using System.Runtime.InteropServices;
using Serilog;
using SharpCompress.Readers;
using VRCVideoCacher.Models;

namespace VRCVideoCacher.YTDL;

public class YtdlManager
{
    private static readonly ILogger Log = Program.Logger.ForContext<YtdlManager>();
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "VRCVideoCacher" } }
    };
    public static readonly string CookiesPath;
    public static readonly string YtdlPath;
    private const string YtdlpApiUrl = "https://api.github.com/repos/yt-dlp/yt-dlp-nightly-builds/releases/latest";
    private const string FfmpegNightlyApiUrl = "https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/latest";
    private const string FfmpegApiUrl = "https://api.github.com/repos/GyanD/codexffmpeg/releases/latest";
    private const string DenoApiUrl = "https://api.github.com/repos/denoland/deno/releases/latest";

    static YtdlManager()
    {
        CookiesPath = Path.Combine(Program.DataPath, "youtube_cookies.txt");

        // try to locate in PATH
        if (string.IsNullOrEmpty(ConfigManager.Config.ytdlPath))
            YtdlPath = FileTools.LocateFile(OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp") ?? throw new FileNotFoundException("无法找到 yt-dlp");
        else if (Path.IsPathRooted(ConfigManager.Config.ytdlPath))
            YtdlPath = ConfigManager.Config.ytdlPath;
        else
            YtdlPath = Path.Combine(Program.DataPath, ConfigManager.Config.ytdlPath);
        
        Log.Debug("Using ytdl path: {YtdlPath}", YtdlPath);
    }
    
    public static void StartYtdlDownloadThread()
    {
        Task.Run(YtdlDownloadTask);
    }

    private static async Task YtdlDownloadTask()
    {
        const int interval = 60 * 60 * 1000; // 1 hour
        while (true)
        {
            await Task.Delay(interval);
            await TryDownloadYtdlp();
        }
        // ReSharper disable once FunctionNeverReturns
    }

    public static async Task TryDownloadYtdlp()
    {
        Log.Information("正在检查 YT-DLP 更新...");
        using var response = await HttpClient.GetAsync(YtdlpApiUrl);
        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("检查 YT-DLP 更新失败。");
            return;
        }
        var data = await response.Content.ReadAsStringAsync();
        var json = JsonConvert.DeserializeObject<GitHubRelease>(data);
        if (json == null)
        {
            Log.Error("解析 YT-DLP 更新响应失败。");
            return;
        }

        var currentYtdlVersion = Versions.CurrentVersion.ytdlp;
        if (!File.Exists(YtdlPath))
            currentYtdlVersion = "未安装";

        var latestVersion = json.tag_name;
        Log.Information("YT-DLP 当前: {Installed}，最新: {Latest}", currentYtdlVersion, latestVersion);
        if (string.IsNullOrEmpty(latestVersion))
        {
            Log.Warning("检查 YT-DLP 更新失败。");
            return;
        }
        if (currentYtdlVersion == latestVersion)
        {
            Log.Information("YT-DLP 已是最新。");
            return;
        }
        Log.Information("YT-DLP 有可用更新，正在更新...");

        await DownloadYtdl(json);
    }

    public static async Task TryDownloadDeno()
    {
        if (string.IsNullOrEmpty(ConfigManager.UtilsPath))
            throw new Exception("获取 Utils 路径失败");
        
        var denoPath = Path.Combine(ConfigManager.UtilsPath, OperatingSystem.IsWindows() ? "deno.exe" : "deno");
        
        using var apiResponse = await HttpClient.GetAsync(DenoApiUrl);
        if (!apiResponse.IsSuccessStatusCode)
        {
            Log.Warning("获取最新 Ffmpeg 发布信息失败: {ResponseStatusCode}", apiResponse.StatusCode);
            return;
        }
        var data = await apiResponse.Content.ReadAsStringAsync();
        var json = JsonConvert.DeserializeObject<GitHubRelease>(data);
        if (json == null)
        {
            Log.Error("解析 deno 发布响应失败。");
            return;
        }
        
        var currentDenoVersion = Versions.CurrentVersion.deno;
        if (!File.Exists(denoPath))
            currentDenoVersion = "未安装";

        var latestVersion = json.tag_name;
        Log.Information("Deno 当前: {Installed}，最新: {Latest}", currentDenoVersion, latestVersion);
        if (string.IsNullOrEmpty(latestVersion))
        {
            Log.Warning("检查 Deno 更新失败。");
            return;
        }
        if (currentDenoVersion == latestVersion)
        {
            Log.Information("Deno 已是最新。");
            return;
        }
        Log.Information("Deno 有可用更新，正在更新...");

        string assetName;
        if (OperatingSystem.IsWindows())
        {
            assetName = "deno-x86_64-pc-windows-msvc.zip";
        }
        else if (OperatingSystem.IsLinux())
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X64:
                    assetName = "deno-x86_64-unknown-linux-gnu.zip";
                    break;
                case Architecture.Arm64:
                    assetName = "deno-aarch64-unknown-linux-gnu.zip";
                    break;
                default:
                    Log.Error("不支持的架构 {OSArchitecture}", RuntimeInformation.OSArchitecture);
                    return;
            }
        }
        else
        {
            Log.Error("不支持的操作系统 {OperatingSystem}", Environment.OSVersion);
            return;
        }
        // deno-x86_64-pc-windows-msvc.zip -> deno-x86_64-pc-windows-msvc
        var assets = json.assets.Where(asset => asset.name == assetName).ToList();
        if (assets.Count < 1)
        {
            Log.Error("无法为该平台找到 Deno 资源 {AssetName}。", assetName);
            return;
        }

        Log.Information("正在下载 Deno...");
        var url = assets.First().browser_download_url;

        using var response = await HttpClient.GetAsync(url);
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = ReaderFactory.Open(responseStream);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key == null || reader.Entry.IsDirectory)
                continue;
            
            Log.Debug("正在提取文件 {Name} ({Size} 字节)", reader.Entry.Key, reader.Entry.Size);
            var path = Path.Combine(ConfigManager.UtilsPath, reader.Entry.Key);
            await using var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var entryStream = reader.OpenEntryStream();
            await entryStream.CopyToAsync(outputStream);
            FileTools.MarkFileExecutable(path);
            Versions.CurrentVersion.deno = json.tag_name;
            Versions.Save();
            Log.Information("Deno 下载并提取完成。");
            return;
        }

        Log.Error("提取 Deno 文件失败。");
    }

    public static async Task TryDownloadFfmpeg()
    {
        if (string.IsNullOrEmpty(ConfigManager.UtilsPath))
            throw new Exception("获取 Utils 路径失败");

        var ffmpegPath = Path.Combine(ConfigManager.UtilsPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

        // Make sure we can write into the folder
        try
        {
            var probeFilePath = Path.Combine(ConfigManager.UtilsPath, "_temp_permission_prober");
            if (File.Exists(probeFilePath))
                File.Delete(probeFilePath);
            File.Create(probeFilePath, 0, FileOptions.DeleteOnClose);
        }
        catch (Exception ex)
        {
            Log.Warning($"跳过 ffmpeg 下载: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (!ConfigManager.Config.CacheYouTube)
            return;

        using var apiResponse = await HttpClient.GetAsync(OperatingSystem.IsWindows() ? FfmpegApiUrl : FfmpegNightlyApiUrl);
        if (!apiResponse.IsSuccessStatusCode)
        {
            Log.Warning("获取最新 Ffmpeg 发布信息失败: {ResponseStatusCode}", apiResponse.StatusCode);
            return;
        }
        var data = await apiResponse.Content.ReadAsStringAsync();
        var json = JsonConvert.DeserializeObject<GitHubRelease>(data);
        if (json == null)
        {
            Log.Error("解析 ffmpeg 发布响应失败。");
            return;
        }

        var currentffmpegVersion = Versions.CurrentVersion.ffmpeg;
        if (!File.Exists(ffmpegPath))
            currentffmpegVersion = "未安装";

        var latestVersion = OperatingSystem.IsWindows() ? json.tag_name : json.name;
        Log.Information("FFmpeg 当前: {Installed}，最新: {Latest}", currentffmpegVersion, latestVersion);
        if (string.IsNullOrEmpty(latestVersion))
        {
            Log.Warning("检查 FFmpeg 更新失败。");
            return;
        }
        if (currentffmpegVersion == latestVersion)
        {
            Log.Information("FFmpeg 已是最新。");
            return;
        }
        Log.Information("FFmpeg 有可用更新，正在更新...");

        string assetSuffix;
        if (OperatingSystem.IsWindows())
        {
            assetSuffix = "full_build-shared.zip";
        }
        else if (OperatingSystem.IsLinux())
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X64:
                    assetSuffix = "master-latest-linux64-gpl.tar.xz";
                    break;
                case Architecture.Arm64:
                    assetSuffix = "master-latest-linuxarm64-gpl.tar.xz";
                    break;
                default:
                    Log.Error("不支持的架构 {OSArchitecture}", RuntimeInformation.OSArchitecture);
                    return;
            }
        }
        else
        {
            Log.Error("不支持的操作系统 {OperatingSystem}", Environment.OSVersion);
            return;
        }
        var url = json.assets
            .FirstOrDefault(assetVersion => assetVersion.name.EndsWith(assetSuffix, StringComparison.OrdinalIgnoreCase))
            ?.browser_download_url ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            Log.Error("无法为该平台找到 FFmpeg 资源。");
            return;
        }
        Log.Information("正在下载 FFmpeg...");

        using var response = await HttpClient.GetAsync(url);
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = ReaderFactory.Open(responseStream);
        var success = false;
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.Key == null || reader.Entry.IsDirectory)
                continue;

            if (reader.Entry.Key.Contains("/bin/"))
            {
                var fileName = Path.GetFileName(reader.Entry.Key);
                Log.Debug("正在提取文件 {Name} ({Size} 字节)", fileName, reader.Entry.Size);
                var path = Path.Combine(ConfigManager.UtilsPath, fileName);
                await using var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                await using var entryStream = reader.OpenEntryStream();
                await entryStream.CopyToAsync(outputStream);
                FileTools.MarkFileExecutable(path);
                success = true;
            }
        }

        if (!success)
        {
            Log.Error("提取 FFmpeg 文件失败。");
            return;
        }
        
        Versions.CurrentVersion.ffmpeg = latestVersion;
        Versions.Save();
        Log.Information("FFmpeg 下载并提取完成。");
    }
    
    private static async Task DownloadYtdl(GitHubRelease json)
    {
        if (File.Exists(YtdlPath) && File.GetAttributes(YtdlPath).HasFlag(FileAttributes.ReadOnly))
        {
            Log.Warning("跳过 yt-dlp 下载：目标路径不可写。");
            return;
        }

        string assetName;
        if (OperatingSystem.IsWindows())
        {
            assetName = "yt-dlp.exe";
        }
        else if (OperatingSystem.IsLinux())
        {
            assetName = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "yt-dlp_linux",
                Architecture.Arm64 => "yt-dlp_linux_aarch64",
                _ => throw new Exception($"不支持的架构 {RuntimeInformation.OSArchitecture}"),
            };
        }
        else
        {
            throw new Exception($"不支持的操作系统 {Environment.OSVersion}");
        }

        foreach (var assetVersion in json.assets)
        {
            if (assetVersion.name != assetName)
                continue;

            await using var stream = await HttpClient.GetStreamAsync(assetVersion.browser_download_url);
                if (string.IsNullOrEmpty(ConfigManager.UtilsPath))
                throw new Exception("获取 YT-DLP 路径失败");

            await using var fileStream = new FileStream(YtdlPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
            Log.Information("已下载 YT-DLP。");
            FileTools.MarkFileExecutable(YtdlPath);
            Versions.CurrentVersion.ytdlp = json.tag_name;
            Versions.Save();
            return;
        }
        throw new Exception("下载 YT-DLP 失败");
    }
    
    private static readonly List<string> YtdlConfigPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yt-dlp.conf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yt-dlp", "config"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yt-dlp", "config.txt"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "yt-dlp", "config"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "yt-dlp", "config.txt"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "yt-dlp.conf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "yt-dlp.conf.txt"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "yt-dlp/config"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "yt-dlp/config.txt"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".yt-dlp/config"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".yt-dlp/config.txt"),
    ];
    
    public static bool GlobalYtdlConfigExists()
    {
        return YtdlConfigPaths.Any(File.Exists);
    }
    
    public static void DeleteGlobalYtdlConfig()
    {
        foreach (var configPath in YtdlConfigPaths)
        {
            if (File.Exists(configPath))
            {
                Log.Information("Deleting global YT-DLP config: {ConfigPath}", configPath);
                File.Delete(configPath);
            }
        }
    }
}