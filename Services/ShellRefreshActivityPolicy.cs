namespace FocusPanel.Services;

internal readonly record struct ShellRefreshActivity(
    bool Clock,
    bool SystemStatus,
    bool TaskSummary);

internal static class ShellRefreshActivityPolicy
{
    internal static bool BecameVisible(
        bool wasVisible,
        bool isVisible)
        => !wasVisible && isVisible;

    internal static ShellRefreshActivity GetActivity(
        bool isShellVisible,
        bool isStatusCenterOpen,
        bool isCalendarOpen)
        => new(
            Clock: isShellVisible,
            SystemStatus: isShellVisible && isStatusCenterOpen,
            TaskSummary: isShellVisible && isCalendarOpen);
}
