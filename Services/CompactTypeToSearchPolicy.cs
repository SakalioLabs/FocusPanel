using System.Linq;

namespace FocusPanel.Services;

internal static class CompactTypeToSearchPolicy
{
    internal static string? GetInitialQuery(
        string? text,
        ShellKeyboardFocusKind focusKind,
        bool isCompactDockFocused,
        bool hasCommandModifier,
        bool hasTransientSurface)
    {
        if (!isCompactDockFocused
            || hasCommandModifier
            || hasTransientSurface
            || focusKind is
                ShellKeyboardFocusKind.TextInput
                or ShellKeyboardFocusKind.SelectionInput
            || string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Any(character =>
                !char.IsControl(character)
                && !char.IsWhiteSpace(character))
            ? text
            : null;
    }
}
