namespace FocusPanel.Services;

internal enum StartEntryAction
{
    ToggleFocusPanelStart,
    OpenWindowsStart
}

internal static class StartEntryPolicy
{
    internal static StartEntryAction FromLeftClick(
        bool shiftPressed) =>
        shiftPressed
            ? StartEntryAction.OpenWindowsStart
            : StartEntryAction.ToggleFocusPanelStart;
}
