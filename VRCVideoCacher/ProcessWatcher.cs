using System.Diagnostics;
using System.Linq;
using Serilog;

namespace VRCVideoCacher;

public class ProcessWatcher
{
    private static readonly ILogger Log = Program.Logger.ForContext<ProcessWatcher>();

    /// <summary>
    /// 监视指定的游戏可执行文件（例如 "VRChat.exe"），当检测到游戏启动时启动主程序（若未运行）。
    /// </summary>
    public static async Task RunWatcher(string gameExe = "VRChat.exe", int pollMs = 2000)
    {
        Log.Information("ProcessWatcher 已启动，正在监视 {GameExe}", gameExe);
        var gameName = Path.GetFileNameWithoutExtension(gameExe);
        var ourExeName = OperatingSystem.IsWindows() ? "VRCVideoCacher.exe" : "VRCVideoCacher";
        while (true)
        {
            try
            {
                var gameProcess = Process.GetProcessesByName(gameName).FirstOrDefault();
                if (gameProcess != null)
                {
                    Log.Information("{GameExe} 已启动 (PID {Pid})，尝试在未运行时启动主程序", gameExe, gameProcess.Id);
                    var ourProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ourExeName));
                    if (!ourProcesses.Any())
                    {
                        var path = Path.Combine(Program.CurrentProcessPath, ourExeName);
                        var proc = new Process()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = path,
                                UseShellExecute = true,
                                WorkingDirectory = Program.CurrentProcessPath
                            }
                        };
                        try
                        {
                            proc.Start();
                            Log.Information("已启动 {Exe}", ourExeName);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("启动 {Exe} 失败：{Message}", ourExeName, ex.Message);
                        }
                    }

                    // 等待游戏退出，再继续监视
                    while (!gameProcess.HasExited)
                        await Task.Delay(2000);
                    Log.Information("{GameExe} 已退出；监视继续", gameExe);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ProcessWatcher 循环错误：{Message}", ex.Message);
            }

            await Task.Delay(pollMs);
        }
    }
}

