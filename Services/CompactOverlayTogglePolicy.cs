namespace FocusPanel.Services;

internal enum CompactOverlayToggleAction
{
    ExpandAndOpen,
    CloseSurface
}

internal static class CompactOverlayTogglePolicy
{
    internal static CompactOverlayToggleAction Decide(
        bool hasOwnedSurfaceOpen) =>
        hasOwnedSurfaceOpen
            ? CompactOverlayToggleAction.CloseSurface
            : CompactOverlayToggleAction.ExpandAndOpen;
}
