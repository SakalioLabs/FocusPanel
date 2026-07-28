namespace FocusPanel.Services;

public static class ShellAutoHidePolicy
{
    public static bool ShouldHide(
        bool isDragging,
        bool isTransientInteractionActive,
        bool isCursorInside,
        bool isInputFocusActive,
        bool ignoreInputFocus)
        => !isDragging
            && !isTransientInteractionActive
            && !isCursorInside
            && (ignoreInputFocus || !isInputFocusActive);
}
