namespace FocusPanel.Services;

internal enum StatusCenterEntryAction
{
    ToggleStatusCenter,
    TogglePanelNotifications
}

internal static class StatusCenterEntryActionPolicy
{
    internal static StatusCenterEntryAction Resolve(
        bool shiftPressed) =>
        shiftPressed
            ? StatusCenterEntryAction.TogglePanelNotifications
            : StatusCenterEntryAction.ToggleStatusCenter;
}
