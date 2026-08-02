using System;

namespace FocusPanel.Services;

internal readonly record struct WorkspaceNavigationState(
    bool Dashboard,
    bool Files,
    bool Tasks,
    bool Pomodoro,
    bool Ai,
    bool Settings)
{
    internal int ActiveCount =>
        (Dashboard ? 1 : 0)
        + (Files ? 1 : 0)
        + (Tasks ? 1 : 0)
        + (Pomodoro ? 1 : 0)
        + (Ai ? 1 : 0)
        + (Settings ? 1 : 0);
}

internal static class WorkspaceNavigationStateComposer
{
    internal static WorkspaceNavigationState Compose(
        string? destination,
        bool isSettingsOpen)
    {
        if (isSettingsOpen)
        {
            return new WorkspaceNavigationState(
                false,
                false,
                false,
                false,
                false,
                true);
        }

        return new WorkspaceNavigationState(
            Is(destination, "Dashboard"),
            Is(destination, "Files"),
            Is(destination, "Tasks"),
            Is(destination, "Pomodoro"),
            Is(destination, "AI"),
            false);
    }

    private static bool Is(
        string? destination,
        string expected) =>
        string.Equals(
            destination,
            expected,
            StringComparison.Ordinal);
}
