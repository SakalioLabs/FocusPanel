namespace FocusPanel.Services;

internal enum ShellClosingAction
{
    KeepRunning,
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
            return ShellClosingAction.KeepRunning;
        if (shutdownCompleted)
            return ShellClosingAction.AllowClose;
        return shutdownStarted
            ? ShellClosingAction
                .WaitForAsyncShutdown
            : ShellClosingAction
                .BeginAsyncShutdown;
    }
}
