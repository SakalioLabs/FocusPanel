namespace FocusPanel.Services;

public enum ShellKeyboardFocusKind
{
    None,
    Command,
    TextInput,
    SelectionInput
}

public static class ShellFocusRetentionPolicy
{
    public static bool ShouldRetainShell(
        ShellKeyboardFocusKind focusKind)
        => focusKind is ShellKeyboardFocusKind.TextInput
            or ShellKeyboardFocusKind.SelectionInput;
}
