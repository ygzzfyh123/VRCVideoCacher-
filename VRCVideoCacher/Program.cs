using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using VRCVideoCacher.API;
using VRCVideoCacher.YTDL;
using System.Threading;
using System.Windows.Forms;

namespace VRCVideoCacher;

internal static class Program
{
    public static string YtdlpHash = string.Empty;
    public const string Version = "2026.1.9";
    public static readonly ILogger Logger = Log.ForContext("SourceContext", "Core");
    public static readonly string CurrentProcessPath = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
    public static readonly string DataPath = OperatingSystem.IsWindows()
        ? CurrentProcessPath
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCVideoCacher");

    public static async Task Main(string[] args)
    {
        Console.Title = $"VRC视频缓存器 v{Version}";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3} {Coalesce(Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),'<none>')}] {@m}\n{@x}",
                theme: TemplateTheme.Literate))
            .CreateLogger();
        // 立即处理 --config 参数以便在进行网络/更新检查前弹出 GUI
        if (args.Contains("--config"))
        {
            ConfigForm.RunForm();
            return;
        }
        const string elly = "Elly";
        const string natsumi = "Natsumi";
        const string haxy = "Haxy";
        Logger.Information("VRCVideoCacher 版本 {Version}，由 {Elly}, {Natsumi}, {Haxy} 制作", Version, elly, natsumi, haxy);
        
        Directory.CreateDirectory(DataPath);
        try
        {
            await Updater.CheckForUpdates();
        }
        catch (Exception ex)
        {
            Logger.Warning("检查更新失败（网络或 DNS 问题），将继续启动：{Message}", ex.Message);
        }
        Updater.Cleanup();
        if (Environment.CommandLine.Contains("--Reset"))
        {
            FileTools.RestoreAllYtdl();
            Environment.Exit(0);
        }
        if (Environment.CommandLine.Contains("--Hash"))
        {
            Console.WriteLine(GetOurYtdlpHash());
            Environment.Exit(0);
        }
        Console.CancelKeyPress += (_, _) => Environment.Exit(0);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => OnAppQuit();

        YtdlpHash = GetOurYtdlpHash();

        if (ConfigManager.Config.ytdlAutoUpdate && !string.IsNullOrEmpty(ConfigManager.Config.ytdlPath))
        {
            await YtdlManager.TryDownloadYtdlp();
            YtdlManager.StartYtdlDownloadThread();
            _ = YtdlManager.TryDownloadDeno();
            _ = YtdlManager.TryDownloadFfmpeg();
        }

        if (OperatingSystem.IsWindows())
            AutoStartShortcut.TryUpdateShortcutPath();
        WebServer.Init();
        FileTools.BackupAllYtdl();
        await BulkPreCache.DownloadFileList();

        // --- Watcher / scheduled task CLI handling ---
        // 运行监视器：用于在游戏启动时自动启动主程序
        if (args.Contains("--watch"))
        {
            var gameArg = args.FirstOrDefault(a => a.StartsWith("--watch-game="));
            var gameExe = gameArg != null ? gameArg.Split('=', 2)[1] : "VRChat.exe";
            await ProcessWatcher.RunWatcher(gameExe);
            return;
        }

        if (args.Contains("--config"))
        {
            // 打开配置 GUI 窗口并在关闭后退出
            ConfigForm.RunForm();
            return;
        }

        // 在当前用户登录时创建一个 scheduled task 来启动本程序的 watcher 模式
        if (args.Contains("--install-watch-task"))
        {
            var exePath = Environment.ProcessPath;
            var taskName = "VRCVideoCacherWatcher";
            var argumentsStr = "--watch";
            var schtasksCmd = $"schtasks /Create /SC ONLOGON /RL HIGHEST /F /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" {argumentsStr}\"";
            try
            {
                var proc = Process.Start(new ProcessStartInfo("cmd.exe", $"/C {schtasksCmd}") { CreateNoWindow = true, UseShellExecute = false });
                proc?.WaitForExit();
                Logger.Information("已创建计划任务 {TaskName}", taskName);
            }
            catch (Exception ex)
            {
                Logger.Error("创建计划任务失败: {Msg}", ex.Message);
            }

            Environment.Exit(0);
        }

        if (args.Contains("--uninstall-watch-task"))
        {
            var taskName = "VRCVideoCacherWatcher";
            var schtasksCmd = $"schtasks /Delete /TN \"{taskName}\" /F";
            try
            {
                var proc = Process.Start(new ProcessStartInfo("cmd.exe", $"/C {schtasksCmd}") { CreateNoWindow = true, UseShellExecute = false });
                proc?.WaitForExit();
                Logger.Information("已删除计划任务 {TaskName}", taskName);
            }
            catch (Exception ex)
            {
                Logger.Error("删除计划任务失败: {Msg}", ex.Message);
            }

            Environment.Exit(0);
        }

        if (ConfigManager.Config.ytdlUseCookies && !IsCookiesEnabledAndValid())
            Logger.Warning("未找到 cookies，请使用浏览器扩展发送 cookies 或在配置中禁用 \"ytdlUseCookies\"。");

        CacheManager.Init();

        // run after init to avoid text spam blocking user input
        if (OperatingSystem.IsWindows())
            _ = WinGet.TryInstallPackages();

        if (YtdlManager.GlobalYtdlConfigExists())
            Logger.Error("检测到全局 yt-dlp 配置文件 \"%AppData%\\yt-dlp\"。请删除以避免与 VRCVideoCacher 冲突。");
        
        await Task.Delay(-1);
    }

    public static bool IsCookiesEnabledAndValid()
    {
        if (!ConfigManager.Config.ytdlUseCookies)
            return false;

        if (!File.Exists(YtdlManager.CookiesPath))
            return false;
        
        var cookies = File.ReadAllText(YtdlManager.CookiesPath);
        return IsCookiesValid(cookies);
    }

    public static bool IsCookiesValid(string cookies)
    {
        if (string.IsNullOrEmpty(cookies))
            return false;

        if (cookies.Contains("youtube.com") && cookies.Contains("LOGIN_INFO"))
            return true;

        return false;
    }

    public static Stream GetYtDlpStub()
    {
        return GetEmbeddedResource("VRCVideoCacher.yt-dlp-stub.exe");
    }
    
    private static Stream GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new Exception($"{resourceName} not found in resources.");

        return stream;
    }

    private static string GetOurYtdlpHash()
    {
        var stream = GetYtDlpStub();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        stream.Dispose();
        return ComputeBinaryContentHash(ms.ToArray());
    }
    
    public static string ComputeBinaryContentHash(byte[] base64)
    {
        return Convert.ToBase64String(SHA256.HashData(base64));
    }

    private static void OnAppQuit()
    {
        FileTools.RestoreAllYtdl();
        Logger.Information("正在退出...");
    }
}