namespace FocusPanel.Services;

internal enum TaskbarAppClickAction
{
    ActivateOrShowWindows,
    LaunchNewInstance,
    None
}

internal static class TaskbarAppClickPolicy
{
    internal static TaskbarAppClickAction FromLeftClick(
        bool shiftPressed,
        bool canLaunchNewInstance)
    {
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
