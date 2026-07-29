using System;

namespace FocusPanel.Services;

public enum CalendarNavigationAction
{
    PreviousDay,
    NextDay,
    PreviousWeek,
    NextWeek,
    PreviousMonth,
    NextMonth,
    Today
}

internal static class
    CalendarKeyboardNavigationPolicy
{
    internal static DateTime GetTargetDate(
        DateTime selectedDate,
        CalendarNavigationAction action,
        DateTime today)
    {
        DateTime selected =
            selectedDate.Date;
        try
        {
            return action switch
            {
                CalendarNavigationAction
                    .PreviousDay =>
                    selected.AddDays(-1),
                CalendarNavigationAction
                    .NextDay =>
                    selected.AddDays(1),
                CalendarNavigationAction
                    .PreviousWeek =>
                    selected.AddDays(-7),
                CalendarNavigationAction
                    .NextWeek =>
                    selected.AddDays(7),
                CalendarNavigationAction
                    .PreviousMonth =>
                    selected.AddMonths(-1),
                CalendarNavigationAction
                    .NextMonth =>
                    selected.AddMonths(1),
                CalendarNavigationAction
                    .Today =>
                    today.Date,
                _ => selected
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return selected;
        }
    }
}
