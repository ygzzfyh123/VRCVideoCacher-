using System.Runtime.Versioning;
using Serilog;
using ShellLink;
using ShellLink.Structures;

namespace VRCVideoCacher;

public class AutoStartShortcut
{
    private static readonly ILogger Log = Program.Logger.ForContext<AutoStartShortcut>();
    private static readonly byte[] ShortcutSignatureBytes = { 0x4C, 0x00, 0x00, 0x00 }; // ShellLinkHeader 的签名
    private const string ShortcutName = "VRCVideoCacher";
    
    [SupportedOSPlatform("windows")]
    public static void TryUpdateShortcutPath()
    {
        var shortcut = GetOurShortcut();
        if (shortcut == null)
            return;

        var info = ShellLink.Shortcut.ReadFromFile(shortcut);
        if (info.LinkTargetIDList.Path == Environment.ProcessPath &&
            info.StringData.WorkingDir == Path.GetDirectoryName(Environment.ProcessPath))
            return;
        
        Log.Information("正在更新 VRCX 自动启动捷径路径...");
        info.LinkTargetIDList.Path = Environment.ProcessPath;
        info.StringData.WorkingDir = Path.GetDirectoryName(Environment.ProcessPath);
        info.WriteToFile(shortcut);
    }

    private static bool StartupEnabled()
    {
        if (string.IsNullOrEmpty(GetOurShortcut()))
            return false;

        return true;
    }

    [SupportedOSPlatform("windows")]
    public static void CreateShortcut()
    {
        if (StartupEnabled())
            return;
        
        Log.Information("将 VRCVideoCacher 添加到 VRCX 自动启动...");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCX", "startup");
        var shortcutPath = Path.Combine(path, $"{ShortcutName}.lnk");
        if (!Directory.Exists(path))
        {
            Log.Information("未检测到 VRCX 安装");
            return;
        }
        
        var shortcut = new ShellLink.Shortcut
        {
            LinkTargetIDList = new LinkTargetIDList
            {
                Path = Environment.ProcessPath
            },
            StringData = new StringData
            {
                WorkingDir = Path.GetDirectoryName(Environment.ProcessPath)
            }
        };
        shortcut.WriteToFile(shortcutPath);
    }

    private static string? GetOurShortcut()
    {
        var shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCX", "startup");
        if (!Directory.Exists(shortcutPath))
            return null;
        
        var shortcuts = FindShortcutFiles(shortcutPath);
        foreach(var shortCut in shortcuts)
        {
            if (shortCut.Contains(ShortcutName))
                return shortCut;
        }

        return null;
    }
    
    private static List<string> FindShortcutFiles(string folderPath)
    {
        var directoryInfo = new DirectoryInfo(folderPath);
        var files = directoryInfo.GetFiles();
        var ret = new List<string>();

        foreach (var file in files)
        {
            if (IsShortcutFile(file.FullName))
                ret.Add(file.FullName);
        }

        return ret;
    }
    
    private static bool IsShortcutFile(string filePath)
    {
        var headerBytes = new byte[4];
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fileStream.Length >= 4)
        {
            fileStream.ReadExactly(headerBytes, 0, 4);
        }

        return headerBytes.SequenceEqual(ShortcutSignatureBytes);
    }
}