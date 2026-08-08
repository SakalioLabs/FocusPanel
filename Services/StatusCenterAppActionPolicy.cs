namespace FocusPanel.Services;

internal enum StatusCenterAppAction
{
    ActivateOrLaunch,
    ToggleWindowList
}

internal static class StatusCenterAppActionPolicy
{
    internal static StatusCenterAppAction Resolve(
        int windowCount) =>
        windowCount > 1
            ? StatusCenterAppAction.ToggleWindowList
            : StatusCenterAppAction.ActivateOrLaunch;
}
