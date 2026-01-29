# 即将推出带有新界面的 Steam 版本！
### [点击这里](https://store.steampowered.com/app/4296960/VRCVideoCacher/) 将我们加入愿望单

![img](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/4296960/d1bac93e4abb00108cda2137260b76a25bcffea4/header.jpg)

# VRCVideoCacher

### 什么是 VRCVideoCacher？

VRCVideoCacher 是一个用于将 VRChat 视频缓存到本地磁盘并修复 YouTube 视频加载失败问题的工具。

### 它如何工作？

该程序会将 VRChat 的 `yt-dlp.exe` 替换为我们自带的 stub 版本，程序启动时会替换为真正的二进制，退出时会恢复原文件。

自动安装缺失的解码器： [VP9](https://apps.microsoft.com/detail/9n4d0msmp0pt) | [AV1](https://apps.microsoft.com/detail/9mvzqvxjbq9v) | [AC-3](https://apps.microsoft.com/detail/9nvjqjbdkn97)

### 是否有风险？

- 来自 VR 或 EAC？没有已知风险。
- 来自 YouTube/Google？有可能，因此我们强烈建议尽可能使用备用的 Google 帐号。

### 如何规避 YouTube 机器人检测

要修复 YouTube 视频加载失败的问题，您需要安装我们的浏览器扩展（Chrome：[这里](https://chromewebstore.google.com/detail/vrcvideocacher-cookies-ex/kfgelknbegappcajiflgfbjbdpbpokge)；Firefox：[这里](https://addons.mozilla.org/en-US/firefox/addon/vrcvideocachercookiesexporter)）。更多信息见：[项目扩展仓库](https://github.com/clienthax/VRCVideoCacherBrowserExtension)。

在 VRCVideoCacher 运行期间，至少在一次登录且已加载扩展的浏览器中访问 YouTube.com，待程序获取到 cookies 后可以安全地卸载扩展。但请注意：如果之后使用相同浏览器再次访问并保持登录状态，YouTube 可能会刷新 cookies，使已存储的 cookies 失效。为避免此问题，建议在程序获取 cookies 后删除浏览器中的 YouTube cookies，或使用一个与主浏览器分离的备用浏览器/账号。

### 修复 YouTube 视频有时无法播放的问题

> 加载失败。找不到文件，或不支持的编码格式，或视频分辨率过高，或系统资源不足。

同步系统时间：打开 Windows 设置 -> 时间和语言 -> 日期和时间，点击“其他设置”下的“立即同步”。

编辑 `Config.json`，将 `ytdlDelay` 设置为例如 `10`（秒）。

### 修复公共世界中缓存视频无法播放的问题

> 试图播放未受信任的 URL（域：localhost），未被允许在公共世界中播放。

以管理员身份运行记事本，编辑 `C:\Windows\System32\drivers\etc\hosts`，在文件末尾添加一行：

```
127.0.0.1 localhost.youtube.com
```

然后编辑 `Config.json` 并将 `ytdlWebServerURL` 设置为 `http://localhost.youtube.com:9696`

### 在 Linux 上运行

- 安装 `dotnet-runtime-10.0`
- 使用 `./VRCVideoCacher` 运行
- 默认情况下，VRCVideoCacher 会尝试下载并使用自带的二进制；如果希望使用系统包，请在 `Config.json` 中将 `ytdlPath` 设置为 `""`，然后手动通过 `pip install "yt-dlp[default,curl-cffi]"` 安装 `yt-dlp`，并确保安装 `deno` 和 `ffmpeg`。请保持 yt-dlp 更新以避免问题。

### 卸载

- 如果您安装了 VRCX，请删除 `%AppData%\VRCX\startup` 下名为 `VRCVideoCacher` 的启动捷径。
- 删除 `%AppData%\..\LocalLow\VRChat\VRChat\Tools` 中的 `yt-dlp.exe`，然后重启 VRChat 或重新进入世界。

### 配置选项

| 选项                     | 说明                                                                                                                                                                                                                                                                                           |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ytdlWebServerURL         | 用于规避 VRChat 的公共世界视频播放器白名单，具体用法见上文。                                                                                                                                                                                                                                   |
| ytdlPath                 | 指定 yt-dlp 可执行文件路径，默认 `Utils\\yt-dlp.exe`；若设置为空字符串 `""` 将使用系统 PATH，这会同时禁用 yt-dlp、ffmpeg 和 deno 的自动更新功能。                                                                                                                                                |
| ytdlUseCookies           | 使用浏览器扩展获取 cookies（Chrome/Firefox），用于规避 YouTube 的机器人检测。                                                                                                                                                                                                                  |
| ytdlAutoUpdate           | 自动更新 yt-dlp、ffmpeg 和 deno。                                                                                                                                                                                                                                                               |
| ytdlAdditionalArgs       | 添加自定义的 [yt-dlp 参数](https://github.com/yt-dlp/yt-dlp?tab=readme-ov-file#usage-and-options)（仅在了解含义时使用）。                                                                                                                                                                           |
| ytdlDubLanguage          | 为 AVPro 和缓存视频设置首选音频语言（可能导致自动翻译等问题）。例如 `de` 表示德语。                                                                                                                                                                                                                |
| ytdlDelay                | 延迟（秒），默认 `0`；某些情况下在游戏内播放 YouTube 视频需要稍作延迟（例如 `8`）。                                                                                                                                                                                                               |
| CachedAssetPath          | 存储已缓存视频的位置，例如将视频存放在独立磁盘 `D:\\DownloadedVideos`。                                                                                                                                                                                                                         |
| BlockedUrls              | 列表中指定的 URL 将不会加载，也可用来屏蔽域名（例如 `[ "https://youtube.com", "https://youtu.be" ]`）。                                                                                                                                                                                               |
| BlockRedirect            | 当被屏蔽时用于替代播放的本地视频。                                                                                                                                                                                                                                                              |
| CacheYouTube             | 将 YouTube 视频下载到 `CachedAssets` 以提升下次播放速度。                                                                                                                                                                                                                                      |
| CacheYouTubeMaxResolution| 缓存 YouTube 视频的最大分辨率（分辨率越高下载越慢），例如 `2160` 表示 4K。                                                                                                                                                                                                                        |
| CacheYouTubeMaxLength    | 缓存视频的最大时长（分钟），例如 `60` 表示 1 小时。                                                                                                                                                                                                                                              |
| CacheMaxSizeInGb         | `CachedAssets` 文件夹的最大占用（GB），`0` 表示不限制。                                                                                                                                                                                                                                          |
| CachePyPyDance           | 下载在 [PyPyDance](https://vrchat.com/home/world/wrld_f20326da-f1ac-45fc-a062-609723b097b1) 世界中播放的视频。                                                                                                                                                                                         |
| CacheVRDancing           | 下载在 [VRDancing](https://vrchat.com/home/world/wrld_42377cf1-c54f-45ed-8996-5875b0573a83) 世界中播放的视频。                                                                                                                                                                                         |
| PatchResonite            | 启用 Resonite 支持。                                                                                                                                                                                                                                                                              |
| PatchVRC                 | 启用对 VRChat 的支持。                                                                                                                                                                                                                                                                            |
| AutoUpdate               | 当有新版本可用时自动安装更新。                                                                                                                                                                                                                                                                    |
| PreCacheUrls             | 从 JSON 列表预下载所有视频，例如 `[{"fileName":"video.mp4","url":"https:\/\/example.com\/video.mp4","lastModified":1631653260,"size":124029113},...]`，其中 `lastModified` 和 `size` 为可选字段用于文件完整性校验。                                                                                              |

> Generate PoToken 工具已被[弃用](https://github.com/iv-org/youtube-trusted-session-generator?tab=readme-ov-file#tool-is-deprecated)

## 自动在游戏启动时运行

本仓库新增了一个“监视器”模式和安装/卸载 Windows 计划任务的命令行选项，便于在指定游戏启动时自动运行主程序。

用法示例（Windows）：

- 安装计划任务（登录时启动 watcher，默认监视 `VRChat.exe`）：

```bash
VRCVideoCacher.exe --install-watch-task
```

- 卸载计划任务：

```bash
VRCVideoCacher.exe --uninstall-watch-task
```

- 手动以 watcher 模式运行（可指定监视的游戏可执行文件）：

```bash
VRCVideoCacher.exe --watch --watch-game=VRChat.exe
```

说明：
- watcher 模式会轮询进程列表，检测到指定的游戏可执行文件启动后会尝试启动 `VRCVideoCacher.exe`（如果尚未运行）。
- 安装计划任务通常需要管理员权限；计划任务通过 `schtasks` 创建，默认任务名为 `VRCVideoCacherWatcher`。