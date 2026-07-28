namespace FocusPanel.Services;

public static class ShellTransientSurfacePolicy
{
    public static bool IsActive(
        bool explicitInteraction,
        bool hasMouseCapture,
        bool hasOpenSelectionPopup)
        => explicitInteraction
            || hasMouseCapture
            || hasOpenSelectionPopup;
}
