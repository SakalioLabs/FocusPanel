using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

public sealed record CalendarFocusSummary(
    int SessionCount,
    int DurationMinutes);

public sealed record CalendarDayItem(
    DateTime Date,
    int DayNumber,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsSelected,
    int FocusSessionCount,
    int FocusMinutes)
{
    public bool HasFocus => FocusSessionCount > 0;
    public string AccessibleName =>
        $"{Date:M月d日}"
        + (FocusSessionCount > 0
            ? $"，完成 {FocusSessionCount} 次专注，共 {FocusMinutes} 分钟"
            : "，没有专注记录");
}

public static class CalendarMonthComposer
{
    public const int DayCount = 42;

    public static DateTime GetGridStart(
        DateTime displayedMonth)
    {
        DateTime firstDay = new(
            displayedMonth.Year,
            displayedMonth.Month,
            1);
        int daysSinceMonday =
            ((int)firstDay.DayOfWeek + 6) % 7;
        return firstDay.AddDays(-daysSinceMonday);
    }

    public static IReadOnlyList<CalendarDayItem> Compose(
        DateTime displayedMonth,
        DateTime selectedDate,
        DateTime today,
        IReadOnlyDictionary<DateTime, CalendarFocusSummary>
            focusByDate)
    {
        DateTime month = new(
            displayedMonth.Year,
            displayedMonth.Month,
            1);
        DateTime start = GetGridStart(month);
        var items = new List<CalendarDayItem>(DayCount);

        for (int index = 0; index < DayCount; index++)
        {
            DateTime date = start.AddDays(index);
            focusByDate.TryGetValue(
                date.Date,
                out CalendarFocusSummary? focus);
            items.Add(new CalendarDayItem(
                date,
                date.Day,
                date.Year == month.Year
                    && date.Month == month.Month,
                date.Date == today.Date,
                date.Date == selectedDate.Date,
                focus?.SessionCount ?? 0,
                focus?.DurationMinutes ?? 0));
        }

        return items;
    }
}
