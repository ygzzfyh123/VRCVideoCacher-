using System.Diagnostics;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Semver;
using Serilog;
using VRCVideoCacher.Models;

namespace VRCVideoCacher;

public class Updater
{
    private const string UpdateUrl = "https://api.github.com/repos/EllyVR/VRCVideoCacher/releases/latest";
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "VRCVideoCacher.Updater" } }
    };
    private static readonly ILogger Log = Program.Logger.ForContext<Updater>();
    private static readonly string FileName =  OperatingSystem.IsWindows() ? "VRCVideoCacher.exe" : "VRCVideoCacher";
    private static readonly string FilePath = Path.Combine(Program.CurrentProcessPath, FileName);
    private static readonly string BackupFilePath = Path.Combine(Program.CurrentProcessPath, "VRCVideoCacher.bkp");
    private static readonly string TempFilePath = Path.Combine(Program.CurrentProcessPath, "VRCVideoCacher.Temp");

    public static async Task CheckForUpdates()
    {
        Log.Information("检查更新...");
        var isDebug = false;
#if DEBUG
            isDebug = true;
#endif
        if (Program.Version.Contains("-dev") || isDebug)
        {
            Log.Information("处于开发模式，跳过更新检查。");
            return;
        }
        using var response = await HttpClient.GetAsync(UpdateUrl);
        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("检查更新失败。");
            return;
        }
        var data = await response.Content.ReadAsStringAsync();
        var latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(data);
        if (latestRelease == null)
        {
            Log.Error("解析更新响应失败。");
            return;
        }
        var latestVersion = SemVersion.Parse(latestRelease.tag_name);
        var currentVersion = SemVersion.Parse(Program.Version);
        Log.Information("最新版本: {Latest}, 已安装版本: {Installed}", latestVersion, currentVersion);
        if (SemVersion.ComparePrecedence(currentVersion, latestVersion) >= 0)
        {
            Log.Information("没有可用更新。");
            return;
        }
        Log.Information("发现更新: {Version}", latestVersion);
        if (ConfigManager.Config.AutoUpdate)
        {
            await UpdateAsync(latestRelease);
            return;
        }
        Log.Information("自动更新已禁用。请从 releases 页面手动更新： https://github.com/EllyVR/VRCVideoCacher/releases");
    }
        
    public static void Cleanup()
    {
        if (File.Exists(BackupFilePath))
        {
            File.Delete(BackupFilePath);
            // silly temporary config reset to test video prefetch
            ConfigManager.Config.ytdlDelay = 0;
            ConfigManager.TrySaveConfig();
        }
    }
        
    private static async Task UpdateAsync(GitHubRelease release)
    {
        foreach (var asset in release.assets)
        {
            if (asset.name != FileName)
                continue;

            File.Move(FilePath, BackupFilePath);
            
            try
            {
                await using var stream = await HttpClient.GetStreamAsync(asset.browser_download_url);
                await using var fileStream = new FileStream(TempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);
                fileStream.Close();

                if (await HashCheck(asset.digest))
                {
                    Log.Information("哈希校验通过，替换二进制。");
                    File.Move(TempFilePath, FilePath);
                }
                else
                {
                    Log.Information("哈希校验失败，回滚更新。");
                    File.Move(BackupFilePath,FilePath);
                    return;
                }
                Log.Information("已更新到版本 {Version}", release.tag_name);
                if (!OperatingSystem.IsWindows())
                    FileTools.MarkFileExecutable(FilePath);

                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = FilePath,
                        UseShellExecute = true,
                        WorkingDirectory = Program.CurrentProcessPath
                    }
                };
                process.Start();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log.Error("更新失败: {Message}", ex.Message);
                File.Move(BackupFilePath, FilePath);
                Console.ReadKey();
            }
        }
    }

    private static async Task<bool> HashCheck(string githubHash)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.Open(TempFilePath, FileMode.Open);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var hashString = Convert.ToHexString(hashBytes);
        githubHash = githubHash.Split(':')[1];
        var hashMatches = string.Equals(githubHash, hashString, StringComparison.OrdinalIgnoreCase);
        Log.Information("文件哈希: {FileHash} GitHub 哈希: {GitHubHash} 是否匹配: {HashMatches}", hashString, githubHash, hashMatches);
        return hashMatches;
    }
}