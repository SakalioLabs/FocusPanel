namespace FocusPanel.Services;

internal static class TaskbarHoverPreviewPolicy
{
    internal static bool ShouldOpen(
        int windowCount,
        bool isPointerOver,
        bool isMouseButtonPressed,
        bool hasOpenMenu) =>
        windowCount > 0
        && isPointerOver
        && !isMouseButtonPressed
        && !hasOpenMenu;
}
