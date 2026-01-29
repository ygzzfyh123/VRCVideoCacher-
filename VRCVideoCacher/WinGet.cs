using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Serilog;

namespace VRCVideoCacher;

public class WinGet
{
    private static readonly ILogger Log = Program.Logger.ForContext<WinGet>();
    private const string WingetExe = "winget.exe";
    private static readonly Dictionary<string, string> WingetPackages = new()
    {
        { "VP9 Video Extensions", "9n4d0msmp0pt" },
        { "AV1 Video Extension", "9mvzqvxjbq9v" },
        { "Dolby Digital Plus decoder for PC OEMs", "9nvjqjbdkn97" }
    };
    
    [SupportedOSPlatform("windows")]
    public static async Task TryInstallPackages()
    {
        Log.Information("检查缺失的解码器包...");
        if (!IsOurPackagesInstalled())
        {
            Log.Information("正在安装缺失的解码器包...");
            await InstallAllPackages();
        }
    }

    private static bool IsOurPackagesInstalled()
    {
        foreach (var package in WingetPackages.Values)
        {
            if (!IsPackageInstalled(package))
            {
                return false;
            }
        }

        Log.Information("解码器包已安装。");
        return true;
    }

    private static bool IsPackageInstalled(string packageId)
    {
        try
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = WingetExe,
                    Arguments = $"list \"{packageId}\" -s msstore --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Error("检测包安装状态时出错: {Message}", ex.Message);
            return false;
        }
    }

    private static async Task InstallAllPackages()
    {
        foreach (var package in WingetPackages.Values)
        {
            await InstallPackage(package);
        }
    }

    private static async Task InstallPackage(string packageId)
    {
        try
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = WingetExe,
                    Arguments = $"install --id {packageId} -s msstore --accept-package-agreements --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };
            process.Start();
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
            {
                if (!string.IsNullOrEmpty(line.Trim()))
                    Log.Debug("{Winget}: " + line, WingetExe);
            }
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                throw new Exception($"Installation failed with exit code {process.ExitCode}. Error: {error}");
            
            var packageName = WingetPackages.FirstOrDefault(x => x.Value == packageId).Key;
            if (process.ExitCode == 0)
                Log.Information("Successfully installed package: {packageName}", packageName);
        }
        catch (Exception ex)
        {
            Log.Error("安装包时出错: {Message}", ex.Message);
        }
    }
}