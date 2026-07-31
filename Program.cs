using System;
using System.Runtime.InteropServices;
using System.Threading;
using FocusPanel.Services;
using Velopack;

namespace FocusPanel;

public static class Program
{
    private const string SingleInstanceMutexName = @"Local\FocusPanel.Application.SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2
            && string.Equals(
                args[0],
                "--restore-after-exit",
                StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                args[1],
                out int restoreParentProcessId))
        {
            Environment.ExitCode =
                RestoreRestartCoordinator.Run(
                    restoreParentProcessId);
            return;
        }

        if (args.Length > 0
            && string.Equals(args[0], DesktopVisibilityElevatedHelper.Command, StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = DesktopVisibilityElevatedHelper.Run(args);
            return;
        }

        if (args.Length > 0
            && string.Equals(
                args[0],
                DesktopVisibilityElevatedHelper
                    .SessionCommand,
                StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode =
                DesktopVisibilityElevatedHelper
                    .RunSession(args);
            return;
        }

        bool isUiSmokeTest = args.Length > 0
            && string.Equals(
                args[0],
                "--ui-smoke-test",
                StringComparison.OrdinalIgnoreCase);
        if (isUiSmokeTest)
        {
            Environment.ExitCode = UiSmokeTestRunner.Run(
                args.Length > 1 ? args[1] : null,
                args.Length > 2 ? args[2] : null,
                args.Length > 3 ? args[3] : null,
                args.Length > 4 ? args[4] : null);
            return;
        }

        bool isWatchdog = args.Length > 0
            && string.Equals(
                args[0],
                "--taskbar-watchdog",
                StringComparison.OrdinalIgnoreCase);
        Mutex? instanceMutex = null;
        bool ownsMutex = false;

        if (!isWatchdog)
        {
            instanceMutex = new Mutex(true, SingleInstanceMutexName, out ownsMutex);
            if (!ownsMutex)
            {
                TryShowExistingInstance();
                instanceMutex.Dispose();
                return;
            }

            VelopackApp.Build().Run();
        }

        try
        {
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
        finally
        {
            if (ownsMutex)
                instanceMutex?.ReleaseMutex();
            instanceMutex?.Dispose();
        }
    }

    private static void TryShowExistingInstance()
    {
        IntPtr hwnd = NativeMethods.FindWindow(null, "FocusPanel");
        if (hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(hwnd, ShellMessages.ShowMainWindow, IntPtr.Zero, IntPtr.Zero);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string? className, string windowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
