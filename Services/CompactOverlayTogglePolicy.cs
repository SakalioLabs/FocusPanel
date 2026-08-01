namespace FocusPanel.Services;

internal enum CompactOverlayToggleAction
{
    ExpandAndOpen,
    Collapse
}

internal static class CompactOverlayTogglePolicy
{
    internal static CompactOverlayToggleAction Decide(
        bool hasOwnedSurfaceOpen) =>
        hasOwnedSurfaceOpen
            ? CompactOverlayToggleAction.Collapse
            : CompactOverlayToggleAction.ExpandAndOpen;
}
