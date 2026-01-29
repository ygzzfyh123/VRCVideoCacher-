using System.Text;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using VRCVideoCacher.Models;
using VRCVideoCacher.YTDL;

namespace VRCVideoCacher.API;

public class ApiController : WebApiController
{
    private static readonly Serilog.ILogger Log = Program.Logger.ForContext<ApiController>();
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "VRCVideoCacher" } }
    };

    [Route(HttpVerbs.Post, "/youtube-cookies")]
    public async Task ReceiveYoutubeCookies()
    {
        using var reader = new StreamReader(HttpContext.OpenRequestStream(), Encoding.UTF8);
        var cookies = await reader.ReadToEndAsync();
        if (!Program.IsCookiesValid(cookies))
        {
            Log.Error("收到无效的 cookies，可能您尚未登录，未保存。");
            HttpContext.Response.StatusCode = 400;
            await HttpContext.SendStringAsync("无效的 cookies。", "text/plain", Encoding.UTF8);
            return;
        }

        await File.WriteAllTextAsync(YtdlManager.CookiesPath, cookies);

        HttpContext.Response.StatusCode = 200;
        await HttpContext.SendStringAsync("已接收 cookies。", "text/plain", Encoding.UTF8);

        Log.Information("已从浏览器扩展接收 Youtube cookies。");
        if (!ConfigManager.Config.ytdlUseCookies)
            Log.Warning("配置未启用从浏览器扩展使用 cookies。");
    }

    [Route(HttpVerbs.Get, "/getvideo")]
    public async Task GetVideo()
    {
        // escape double quotes for our own safety
        var requestUrl = Request.QueryString["url"]?.Replace("\"", "%22").Trim();
        var avPro = string.Compare(Request.QueryString["avpro"], "true", StringComparison.OrdinalIgnoreCase) == 0;
        var source = Request.QueryString["source"];

        if (string.IsNullOrEmpty(requestUrl))
        {
            Log.Error("未提供 URL。");
            await HttpContext.SendStringAsync("未提供 URL。", "text/plain", Encoding.UTF8);
            return;
        }

        Log.Information("请求的 URL: {URL}", requestUrl);

        if (requestUrl.StartsWith("https://dmn.moe"))
        {
            requestUrl = requestUrl.Replace("/sr/", "/yt/");
            Log.Information("检测到 YTS URL，已修改为: {URL}", requestUrl);
            var resolvedUrl = await GetRedirectUrl(requestUrl);
            if (!string.IsNullOrEmpty(resolvedUrl))
            {
                requestUrl = resolvedUrl;
                Log.Information("YTS URL 已解析为: {URL}", resolvedUrl);
            }
            else
            {
                Log.Error("解析 YTS URL 失败: {URL}", requestUrl);
            }
        }

        if (ConfigManager.Config.BlockedUrls.Any(blockedUrl => requestUrl.StartsWith(blockedUrl)))
        {
            Log.Warning("URL 被屏蔽: {url}", requestUrl);
            requestUrl = ConfigManager.Config.BlockRedirect;
        }

        var videoInfo = await VideoId.GetVideoId(requestUrl, avPro);
        if (videoInfo == null)
        {
            Log.Information("无法获取 URL 的视频信息: {URL}", requestUrl);
            return;
        }

        var (isCached, filePath, fileName) = GetCachedFile(videoInfo.VideoId, avPro);
        if (isCached)
        {
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
            var url = $"{ConfigManager.Config.ytdlWebServerURL}/{fileName}";
            Log.Information("Responding with Cached URL: {URL}", url);
            await HttpContext.SendStringAsync(url, "text/plain", Encoding.UTF8);
            return;
        }

        if (string.IsNullOrEmpty(videoInfo.VideoId))
        {
            Log.Information("无法获取视频 ID：跳过。");
            await HttpContext.SendStringAsync(string.Empty, "text/plain", Encoding.UTF8);
            return;
        }

        if (requestUrl.StartsWith("https://mightygymcdn.nyc3.cdn.digitaloceanspaces.com"))
        {
            Log.Information("检测到 Mighty Gym URL：跳过。");
            await HttpContext.SendStringAsync(string.Empty, "text/plain", Encoding.UTF8);
            return;
        }

        if (source == "resonite")
        {
            Log.Information("来自 resonite 的请求：发送 JSON。");
            await HttpContext.SendStringAsync(await VideoId.GetURLResonite(requestUrl), "text/plain", Encoding.UTF8);
            return;
        }

        if (ConfigManager.Config.CacheYouTubeMaxResolution <= 360)
            avPro = false; // disable browser impersonation when it isn't needed

        // pls no villager
        if (requestUrl.StartsWith("https://anime.illumination.media"))
            avPro = true;
        else if (requestUrl.Contains(".imvrcdn.com") ||
                 (requestUrl.Contains(".illumination.media") && !requestUrl.StartsWith("https://yt.illumination.media")))
        {
            Log.Information("检测到 Illumination 媒体 URL：跳过。");
            await HttpContext.SendStringAsync(string.Empty, "text/plain", Encoding.UTF8);
            return;
        }

        // bypass vfi - cinema 
        if (requestUrl.StartsWith("https://virtualfilm.institute"))
        {
            Log.Information("检测到 VFI -Cinema URL：跳过。");
            await HttpContext.SendStringAsync(string.Empty, "text/plain", Encoding.UTF8);
            return;
        }

        var (response, success) = await VideoId.GetUrl(videoInfo, avPro);
        if (!success)
        {
            Log.Error("Get URL: {error}", response);
            // only send the error back if it's for YouTube, otherwise let it play the request URL normally
            if (videoInfo.UrlType == UrlType.YouTube)
            {
                HttpContext.Response.StatusCode = 500;
                await HttpContext.SendStringAsync(response, "text/plain", Encoding.UTF8);
                return;
            }
            response = string.Empty;
        }

        if (videoInfo.UrlType == UrlType.YouTube ||
            videoInfo.VideoUrl.StartsWith("https://manifest.googlevideo.com") ||
            videoInfo.VideoUrl.Contains("googlevideo.com"))
        {
            await VideoTools.Prefetch(response);
            if (ConfigManager.Config.ytdlDelay > 0)
            {
                Log.Information("延迟 {delay} 秒后返回 YouTube URL（可帮助解决视频错误）", ConfigManager.Config.ytdlDelay);
                await Task.Delay(ConfigManager.Config.ytdlDelay * 1000);
            }
        }

        Log.Information("返回 URL: {URL}", response);
        await HttpContext.SendStringAsync(response, "text/plain", Encoding.UTF8);
        // check if file is cached again to handle race condition
        (isCached, _, _) = GetCachedFile(videoInfo.VideoId, avPro);
        if (!isCached)
            VideoDownloader.QueueDownload(videoInfo);
    }

    private static (bool isCached, string filePath, string fileName) GetCachedFile(string videoId, bool avPro)
    {
        var ext = avPro ? "webm" : "mp4";
        var fileName = $"{videoId}.{ext}";
        var filePath = Path.Combine(CacheManager.CachePath, fileName);
        var isCached = File.Exists(filePath);
        if (avPro && !isCached)
        {
            // retry with .mp4
            fileName = $"{videoId}.mp4";
            filePath = Path.Combine(CacheManager.CachePath, fileName);
            isCached = File.Exists(filePath);
        }
        return (isCached, filePath, fileName);
    }

    private static async Task<string?> GetRedirectUrl(string requestUrl)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, requestUrl);
        using var res = await HttpClient.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            return null;

        return res.RequestMessage?.RequestUri?.ToString();
    }
}