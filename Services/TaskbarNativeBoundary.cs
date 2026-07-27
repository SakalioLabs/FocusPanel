using System;

namespace FocusPanel.Services;

internal interface ITaskbarNativeApi
{
    IntPtr FindPrimaryTaskbar();
    bool IsWindowVisible(IntPtr taskbar);
    bool TryGetPrimaryBounds(out TaskbarController.NativeRect bounds);
    bool TryGetWorkArea(out TaskbarController.NativeRect workArea);
    uint GetAppBarState(IntPtr taskbar);
    void SetAppBarState(IntPtr taskbar, uint state);
    void SetTaskbarVisible(IntPtr taskbar, bool visible);
    bool SetWorkArea(TaskbarController.NativeRect workArea);
}

internal interface ITaskbarWatchdogLauncher
{
    bool TryStart(int parentProcessId, string sessionFile, out string? error);
}
