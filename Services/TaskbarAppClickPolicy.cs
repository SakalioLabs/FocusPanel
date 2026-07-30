namespace FocusPanel.Services;

internal enum TaskbarAppClickAction
{
    ActivateOrShowWindows,
    LaunchNewInstance,
    LaunchElevated,
    None
}

internal static class TaskbarAppClickPolicy
{
    internal static TaskbarAppClickAction FromLeftClick(
        bool shiftPressed,
        bool controlPressed,
        bool canLaunchNewInstance)
    {
        if (shiftPressed && controlPressed)
            return TaskbarAppClickAction.LaunchElevated;

        return shiftPressed && canLaunchNewInstance
            ? TaskbarAppClickAction.LaunchNewInstance
            : TaskbarAppClickAction.ActivateOrShowWindows;
    }

    internal static TaskbarAppClickAction FromMiddleClick(
        bool canLaunchNewInstance)
    {
        return canLaunchNewInstance
            ? TaskbarAppClickAction.LaunchNewInstance
            : TaskbarAppClickAction.None;
    }
}
