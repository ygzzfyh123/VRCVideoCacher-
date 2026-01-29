using System.Collections.Immutable;
using System.Globalization;
using Serilog;
using ValveKeyValue;

namespace VRCVideoCacher;

public class FileTools
{
    private static readonly ILogger Log = Program.Logger.ForContext<FileTools>();
    private static readonly string YtdlPathVrc;
    private static readonly string BackupPathVrc;
    private static readonly string YtdlPathReso;
    private static readonly string BackupPathReso;
    private static readonly ImmutableList<string> SteamPaths = [".var/app/com.valvesoftware.Steam", ".steam/steam", ".local/share/Steam"];

    static FileTools()
    {
        string resoPath; 
        if (!string.IsNullOrEmpty(ConfigManager.Config.ResonitePath))
            resoPath = ConfigManager.Config.ResonitePath;
        else
            resoPath = $@"{GetResonitePath()}\steamapps\common\Resonite";

        YtdlPathReso = $@"{resoPath}\RuntimeData\yt-dlp.exe";
        BackupPathReso = $"{YtdlPathReso}.bkp";

        string localLowPath;
        if (OperatingSystem.IsWindows())
        { 
            localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
        }
        else if (OperatingSystem.IsLinux())
        { 
            var compatPath = GetCompatPath("438100") ?? throw new Exception("无法找到 VRChat 兼容数据");
            localLowPath = Path.Join(compatPath, "pfx/drive_c/users/steamuser/AppData/LocalLow");
        }
        else
        {
            throw new NotImplementedException("未知平台");
        }
        YtdlPathVrc = Path.Join(localLowPath, "VRChat/VRChat/Tools/yt-dlp.exe");
        BackupPathVrc = Path.Join(localLowPath, "VRChat/VRChat/Tools/yt-dlp.exe.bkp");
    }

    private static string? GetResonitePath()
    {
        const string appid = "2519830";
        if (!OperatingSystem.IsWindows())
        {
            Log.Error("GetResonitePath 目前仅在 Windows 上受支持");
            return null;
        }
        const string libraryFolders = @"C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf";
        if (!Path.Exists(libraryFolders))
        {
            Log.Error("GetResonitePath: 未在预期位置找到 Steam 的 libraryfolders.vdf: {Path}", libraryFolders);
            return null;
        }

        try
        {
            var stream = File.OpenRead(libraryFolders);
            KVObject data = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);
            foreach (var folder in data)
            {
                var apps = (IEnumerable<KVObject>)folder["apps"];
                if (apps.Any(app => app.Name == appid))
                {
                    return folder["path"].ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("GetResonitePath: 读取 libraryfolders.vdf 时发生异常: {Error}", e.Message);
        }

        return null;
    }

    // Linux only
    private static string? GetCompatPath(string appid)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("GetCompatPath is only supported on Linux");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var steamPaths = SteamPaths.Select(path => Path.Join(home, path))
            .Where(Path.Exists);
        var steam = steamPaths.First();
        if (!Path.Exists(steam))
        {
            Log.Error("未找到 Steam 文件夹！");
            return null;
        }

        Log.Debug("使用的 Steam 路径: {Steam}", steam);
        var libraryFolders = Path.Join(steam, "steamapps/libraryfolders.vdf");
        var stream = File.OpenRead(libraryFolders);

        KVObject data = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);

        List<string> libraryPaths = [];
        foreach (var folder in data)
        {
            // var label = folder["label"]?.ToString(CultureInfo.InvariantCulture);
            // var name = string.IsNullOrEmpty(label) ? folder.Name : label;
            // See https://github.com/ValveResourceFormat/ValveKeyValue/issues/30#issuecomment-1581924891
            var apps = (IEnumerable<KVObject>)folder["apps"];
            if (apps.Any(app => app.Name == appid))
                libraryPaths.Add(folder["path"].ToString(CultureInfo.InvariantCulture));
        }

        var paths = libraryPaths
            .Select(path => Path.Join(path, $"steamapps/compatdata/{appid}"))
            .Where(Path.Exists)
            .ToImmutableList();
        return paths.Count > 0 ? paths.First() : null;
    }

    public static string? LocateFile(string filename)
    {
        var systemPath = Environment.GetEnvironmentVariable("PATH");
        if (systemPath == null) return null;

        var systemPaths = systemPath.Split(Path.PathSeparator);

        var paths = systemPaths
            .Select(path => Path.Combine(path, filename))
            .Where(Path.Exists)
            .ToImmutableList();
        return paths.Count > 0 ? paths.First() : null;
    }

    public static void MarkFileExecutable(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserExecute;
            File.SetUnixFileMode(path, mode);
        }
    }

    public static void BackupAllYtdl()
    {
        if (ConfigManager.Config.PatchVRC)
            BackupAndReplaceYtdl(YtdlPathVrc, BackupPathVrc);
        if (ConfigManager.Config.PatchResonite)
            BackupAndReplaceYtdl(YtdlPathReso, BackupPathReso);
    }

    public static void RestoreAllYtdl()
    {
        RestoreYtdl(YtdlPathVrc, BackupPathVrc);
        RestoreYtdl(YtdlPathReso, BackupPathReso);
    }

    private static void BackupAndReplaceYtdl(string ytdlPath, string backupPath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(ytdlPath) ?? string.Empty))
        {
            Log.Error("YT-DLP 目录不存在，游戏可能未安装：{path}", ytdlPath);
            return;
        }
        if (File.Exists(ytdlPath))
        {
            var hash = Program.ComputeBinaryContentHash(File.ReadAllBytes(ytdlPath));
            if (hash == Program.YtdlpHash)
            {
            Log.Information("YT-DLP 已经被替换。");
                return;
            }
            if (File.Exists(backupPath))
            {
                File.SetAttributes(backupPath, FileAttributes.Normal);
                File.Delete(backupPath);
            }
            File.Move(ytdlPath, backupPath);
            Log.Information("已备份原始 YT-DLP。");
        }
        using var stream = Program.GetYtDlpStub();
        using var fileStream = File.Create(ytdlPath);
        stream.CopyTo(fileStream);
        fileStream.Close();
        var attr = File.GetAttributes(ytdlPath);
        attr |= FileAttributes.ReadOnly;
        File.SetAttributes(ytdlPath, attr);
        Log.Information("已安装替代 YT-DLP。");
    }

    private static void RestoreYtdl(string ytdlPath, string backupPath)
    {
        if (!File.Exists(backupPath))
            return;
        
        Log.Information("正在还原 yt-dlp...");
        if (File.Exists(ytdlPath))
        {
            File.SetAttributes(ytdlPath, FileAttributes.Normal);
            File.Delete(ytdlPath);
        }
        File.Move(backupPath, ytdlPath);
        var attr = File.GetAttributes(ytdlPath);
        attr &= ~FileAttributes.ReadOnly;
        File.SetAttributes(ytdlPath, attr);
        Log.Information("已还原 YT-DLP。");
    }
}