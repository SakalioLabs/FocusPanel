namespace FocusPanel.Services;

public static class ShellAutoHidePolicy
{
    public static bool ShouldHide(
        bool isWorkspacePinned,
        bool isDragging,
        bool isTransientInteractionActive,
        bool isCursorInside,
        bool isInputFocusActive,
        bool ignoreInputFocus)
        => !isWorkspacePinned
            && !isDragging
            && !isTransientInteractionActive
            && !isCursorInside
            && (ignoreInputFocus || !isInputFocusActive);
}
