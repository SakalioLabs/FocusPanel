namespace FocusPanel.Services;

internal enum StatusCenterDetail
{
    None,
    Network,
    ApplicationAudio,
    MediaAndBattery,
    InputMethod
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
