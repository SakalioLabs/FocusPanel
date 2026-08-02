namespace FocusPanel.Services;

internal enum TimeEntryAction
{
    ToggleCalendar,
    ShowDesktop
}

internal static class TimeEntryPolicy
{
    public static TimeEntryAction FromLeftClick(
        bool shiftPressed) =>
        shiftPressed
            ? TimeEntryAction.ShowDesktop
            : TimeEntryAction.ToggleCalendar;
}
