using System;

namespace FocusPanel.Services;

internal interface ITaskbarNativeApi
{
    IntPtr FindPrimaryTaskbar();
    bool IsWindowVisible(IntPtr taskbar);
    uint GetAppBarState(IntPtr taskbar);
    void SetAppBarState(IntPtr taskbar, uint state);
    bool SetTaskbarVisible(IntPtr taskbar, bool visible);
    bool TryGetPrimaryMonitorInfo(
        IntPtr taskbar,
        out TaskbarController.NativeRect workArea,
        out TaskbarController.NativeRect bounds);
    bool SetWorkArea(TaskbarController.NativeRect workArea);
}

internal interface ITaskbarWatchdogLauncher
{
    bool TryStart(int parentProcessId, string sessionFile, out string? error);
}
