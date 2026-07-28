namespace FocusPanel.Services;

public static class ShellAutoHidePolicy
{
    public static bool ShouldHide(
        bool isDragging,
        bool isTransientInteractionActive,
        bool isCursorInside,
        bool hasKeyboardFocus,
        bool ignoreKeyboardFocus)
        => !isDragging
            && !isTransientInteractionActive
            && !isCursorInside
            && (ignoreKeyboardFocus || !hasKeyboardFocus);
}
