using Newtonsoft.Json;
using Serilog;
using VRCVideoCacher.YTDL;

// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace VRCVideoCacher;

public class ConfigManager
{
    public static readonly ConfigModel Config;
    private static readonly ILogger Log = Program.Logger.ForContext<ConfigManager>();
    private static readonly string ConfigFilePath;
    public static readonly string UtilsPath;

    static ConfigManager()
    {
        Log.Information("正在加载配置...");
        ConfigFilePath = Path.Combine(Program.DataPath, "Config.json");
        Log.Debug("使用的配置文件路径: {ConfigFilePath}", ConfigFilePath);

        if (!File.Exists(ConfigFilePath))
        {
            Config = new ConfigModel();
            FirstRun();
        }
        else
        {
            Config = JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(ConfigFilePath)) ?? new ConfigModel();
        }
        if (Config.ytdlWebServerURL.EndsWith('/'))
            Config.ytdlWebServerURL = Config.ytdlWebServerURL.TrimEnd('/');

        UtilsPath = Path.GetDirectoryName(Config.ytdlPath) ?? string.Empty;
        if (!UtilsPath.EndsWith("Utils"))
            UtilsPath = Path.Combine(UtilsPath, "Utils");

        Directory.CreateDirectory(UtilsPath);
        
        Log.Information("配置已加载。");
        TrySaveConfig();
    }

    public static void TrySaveConfig()
    {
        var newConfig = JsonConvert.SerializeObject(Config, Formatting.Indented);
        var oldConfig = File.Exists(ConfigFilePath) ? File.ReadAllText(ConfigFilePath) : string.Empty;
        if (newConfig == oldConfig)
            return;
        
        Log.Information("配置已更改，正在保存...");
        File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(Config, Formatting.Indented));
        Log.Information("配置已保存。");
    }
    
    private static bool GetUserConfirmation(string prompt, bool defaultValue)
    {
        var defaultOption = defaultValue ? "Y/n" : "y/N";
        var message = $"{prompt} ({defaultOption}):";
        message = message.TrimStart();
        Log.Information(message);
        var input = Console.ReadLine();
        return string.IsNullOrEmpty(input) ? defaultValue : input.Equals("y", StringComparison.CurrentCultureIgnoreCase);
    }

    private static void FirstRun()
    {
        Log.Information("看起来这是您第一次运行 VRCVideoCacher，让我们创建一个基本配置文件。");
        
        var autoSetup = GetUserConfirmation("您是否只希望使用 VRCVideoCacher 来修复 YouTube 视频？", true);
        if (autoSetup)
        {
            Log.Information("已创建基础配置。您可以稍后在 Config.json 中修改。");
        }
        else
        {
            Config.CacheYouTube = GetUserConfirmation("是否要缓存/下载 YouTube 视频？", true);
            if (Config.CacheYouTube)
            {
                var maxResolution = GetUserConfirmation("是否以 4K 分辨率缓存/下载 YouTube 视频？", true);
                Config.CacheYouTubeMaxResolution = maxResolution ? 2160 : 1080;
            }

            var vrDancingPyPyChoice = GetUserConfirmation("是否要缓存/下载 VRDancing 和 PyPyDance 的视频？", true);
            Config.CacheVRDancing = vrDancingPyPyChoice;
            Config.CachePyPyDance = vrDancingPyPyChoice;

            Config.PatchResonite = GetUserConfirmation("是否启用 Resonite 支持？", false);
        }

        if (OperatingSystem.IsWindows() && GetUserConfirmation("是否将 VRCVideoCacher 添加到 VRCX 自动启动？", true))
        {
            AutoStartShortcut.CreateShortcut();
        }

        if (YtdlManager.GlobalYtdlConfigExists() && GetUserConfirmation(@"检测到全局 yt-dlp 配置，是否删除 %AppData%\yt-dlp\config？（为使 VRCVideoCacher 正常工作需要删除）", true))
        {
            YtdlManager.DeleteGlobalYtdlConfig();
        }

        Log.Information("您需要安装浏览器扩展以获取 YouTube cookies（这将修复 YouTube 机器人错误）");
        Log.Information("Chrome: https://chromewebstore.google.com/detail/vrcvideocacher-cookies-ex/kfgelknbegappcajiflgfbjbdpbpokge");
        Log.Information("Firefox: https://addons.mozilla.org/en-US/firefox/addon/vrcvideocachercookiesexporter/");
        Log.Information("更多信息: https://github.com/clienthax/VRCVideoCacherBrowserExtension");
        TrySaveConfig();
    }
}

// ReSharper disable InconsistentNaming
public class ConfigModel
{
    public string ytdlWebServerURL = "http://localhost:9696";
    public string ytdlPath = OperatingSystem.IsWindows() ? "Utils\\yt-dlp.exe" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRCVideoCacher/Utils/yt-dlp");
    public bool ytdlUseCookies = true;
    public bool ytdlAutoUpdate = true;
    public string ytdlAdditionalArgs = string.Empty;
    public string ytdlDubLanguage = string.Empty;
    public int ytdlDelay = 0;
    public string CachedAssetPath = "";
    public string[] BlockedUrls = ["https://na2.vrdancing.club/sampleurl.mp4"];
    public string BlockRedirect = "https://www.youtube.com/watch?v=byv2bKekeWQ";
    public bool CacheYouTube = false;
    public int CacheYouTubeMaxResolution = 1080;
    public int CacheYouTubeMaxLength = 120;
    public float CacheMaxSizeInGb = 0;
    public bool CachePyPyDance = false;
    public bool CacheVRDancing = false;
    public bool PatchResonite = false;
    public string ResonitePath = "";
    public bool PatchVRC = true;
    public bool AutoUpdate = true;
    public string[] PreCacheUrls = [];
    
}
// ReSharper restore InconsistentNaming