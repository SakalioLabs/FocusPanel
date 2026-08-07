namespace FocusPanel.Services;

internal enum StatusCenterDetail
{
    None,
    Network,
    ApplicationAudio,
    MediaAndBattery
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
