using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FocusPanel.Services;

public static class TaskbarWatchdog
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF10 = 0x79;
    private const uint WmHotKey = 0x0312;
    private const int HotKeyId = 0x4650;

    public static bool TryStart(int parentProcessId, string sessionFile, out string? error)
    {
        error = null;
        string readyFile = sessionFile + ".ready";
        try
        {
            File.Delete(readyFile);
            string executable = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法定位 FocusPanel 可执行文件。");

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--taskbar-watchdog");
            startInfo.ArgumentList.Add(parentProcessId.ToString());
            startInfo.ArgumentList.Add(sessionFile);
            Process.Start(startInfo);

            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(readyFile))
                    return true;
                Thread.Sleep(40);
            }

            error = "恢复守护进程没有完成初始化，已取消更改系统任务栏设置。";
            return false;
        }
        catch (Exception ex)
        {
            error = $"无法启动恢复守护进程：{ex.Message}";
            return false;
        }
    }

    public static int Run(int parentProcessId, string sessionFile)
    {
        bool hotKeyRegistered = NativeMethods.RegisterHotKey(
            IntPtr.Zero,
            HotKeyId,
            ModControl | ModAlt | ModShift | ModNoRepeat,
            VkF10);

        if (!hotKeyRegistered)
            return 2;

        try
        {
            File.WriteAllText(sessionFile + ".ready", DateTimeOffset.Now.ToString("O"));
            Process? parent = null;
            try
            {
                parent = Process.GetProcessById(parentProcessId);
            }
            catch
            {
                RestoreSafeState(
                    sessionFile,
                    keepDesktopRecoveryArmed: false);
                return 0;
            }

            while (!parent.HasExited)
            {
                while (NativeMethods.PeekMessage(out NativeMessage message, IntPtr.Zero, 0, 0, 1))
                {
                    if (message.Message == WmHotKey && message.WParam.ToInt32() == HotKeyId)
                    {
                        try
                        {
                            File.WriteAllText(sessionFile + ".disabled", DateTimeOffset.Now.ToString("O"));
                        }
                        catch
                        {
                            // The taskbar restoration below remains the primary safety action.
                        }

                        RestoreSafeState(
                            sessionFile,
                            keepDesktopRecoveryArmed: true);
                    }
                }

                Thread.Sleep(100);
            }

            RestoreSafeState(
                sessionFile,
                keepDesktopRecoveryArmed: false);
            return 0;
        }
        finally
        {
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotKeyId);
            try { File.Delete(sessionFile + ".ready"); } catch { }
        }
    }

    private static void RestoreSafeState(
        string sessionFile,
        bool keepDesktopRecoveryArmed) =>
        WatchdogRecoveryCoordinator.Restore(
            sessionFile,
            RestoreWithRetry,
            () =>
                new DesktopCrashRecoveryService()
                    .RestoreIfRequested(
                        force: false,
                        keepMarker:
                            keepDesktopRecoveryArmed));

    private static void RestoreWithRetry(string sessionFile)
    {
        for (int attempt = 0; attempt < 20 && File.Exists(sessionFile); attempt++)
        {
            TaskbarController.RestoreSessionFile(sessionFile);
            if (File.Exists(sessionFile))
                Thread.Sleep(150);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr HWnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public System.Drawing.Point Point;
        public uint Private;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
    }
}

internal static class WatchdogRecoveryCoordinator
{
    internal static void Restore(
        string sessionFile,
        Action<string> restoreTaskbar,
        Func<DesktopCrashRecoveryResult>
            restoreDesktop)
    {
        ArgumentNullException.ThrowIfNull(
            restoreTaskbar);
        ArgumentNullException.ThrowIfNull(
            restoreDesktop);

        try
        {
            restoreTaskbar(sessionFile);
        }
        catch
        {
            // Desktop recovery must still run if the taskbar boundary fails.
        }

        try
        {
            _ = restoreDesktop();
        }
        catch
        {
            // The persistent marker remains available for startup retry.
        }
    }
}
