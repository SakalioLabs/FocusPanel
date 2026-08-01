namespace FocusPanel.Services;

internal enum TaskbarWheelAction
{
    Ignore,
    ScrollApps,
    CycleWindows
}

internal static class TaskbarWheelPolicy
{
    internal static TaskbarWheelAction GetAction(
        int delta,
        bool controlPressed,
        int windowCount)
    {
        if (delta == 0)
            return TaskbarWheelAction.Ignore;

        return controlPressed && windowCount >= 2
            ? TaskbarWheelAction.CycleWindows
            : TaskbarWheelAction.ScrollApps;
    }
}
