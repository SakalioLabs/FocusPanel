namespace FocusPanel.Services;

internal static class WindowTrackingActivityPolicy
{
    internal static bool ShouldProcessWindowEvent(bool isTrackingActive)
        => isTrackingActive;

    internal static bool ShouldRefreshAfterActivityChange(
        bool wasTrackingActive,
        bool isTrackingActive)
        => !wasTrackingActive && isTrackingActive;
}
