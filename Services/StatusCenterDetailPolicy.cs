namespace FocusPanel.Services;

internal enum StatusCenterDetail
{
    None,
    Applications,
    Network,
    ApplicationAudio,
    MediaAndBattery,
    InputMethod,
    PanelNotifications
}

internal static class StatusCenterDetailPolicy
{
    internal static StatusCenterDetail Toggle(
        StatusCenterDetail current,
        StatusCenterDetail requested) =>
        current == requested
            ? StatusCenterDetail.None
            : requested;
}
