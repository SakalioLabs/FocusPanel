namespace FocusPanel.Services;

internal enum ShellClosingAction
{
    HideToTray,
    BeginAsyncShutdown,
    WaitForAsyncShutdown,
    AllowClose
}

internal static class ShellShutdownPolicy
{
    internal static ShellClosingAction Decide(
        bool isExitRequested,
        bool shutdownStarted,
        bool shutdownCompleted)
    {
        if (!isExitRequested)
            return ShellClosingAction.HideToTray;
        if (shutdownCompleted)
            return ShellClosingAction.AllowClose;
        return shutdownStarted
            ? ShellClosingAction
                .WaitForAsyncShutdown
            : ShellClosingAction
                .BeginAsyncShutdown;
    }
}
