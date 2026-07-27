namespace FocusPanel.Services;

public static class ShellAutoHidePolicy
{
    public static bool ShouldHide(
        bool isDragging,
        bool isCursorInside,
        bool hasKeyboardFocus,
        bool ignoreKeyboardFocus)
        => !isDragging
            && !isCursorInside
            && (ignoreKeyboardFocus || !hasKeyboardFocus);
}
