namespace FocusPanel.Services;

internal enum FocusEntryAction
{
    ToggleFocusCenter,
    OpenLastWorkspace
}

internal static class FocusEntryPolicy
{
    public static FocusEntryAction FromLeftClick(
        bool shiftPressed) =>
        shiftPressed
            ? FocusEntryAction.OpenLastWorkspace
            : FocusEntryAction.ToggleFocusCenter;
}
