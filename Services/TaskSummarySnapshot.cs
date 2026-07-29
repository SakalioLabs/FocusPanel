using System;
using System.Collections.Generic;

namespace FocusPanel.Services;

public readonly record struct TaskSummarySnapshot(
    bool IsValid,
    DateTime DisplayedMonth,
    int OpenTaskCount,
    IReadOnlyDictionary<DateTime, CalendarFocusSummary>
        FocusByDate)
{
    public static TaskSummarySnapshot Invalid(
        DateTime displayedMonth) =>
        new(
            false,
            NormalizeMonth(displayedMonth),
            0,
            new Dictionary<
                DateTime,
                CalendarFocusSummary>());

    internal static DateTime NormalizeMonth(
        DateTime value) =>
        new(value.Year, value.Month, 1);
}

public readonly record struct TaskSummarySession(
    DateTime StartTime,
    int DurationMinutes);

internal readonly record struct TaskSummaryRawData(
    int OpenTaskCount,
    IReadOnlyList<TaskSummarySession> Sessions);
