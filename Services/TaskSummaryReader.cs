using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public sealed class TaskSummaryReader
{
    private readonly Func<
        DateTime,
        DateTime,
        TaskSummaryRawData> _readData;

    public TaskSummaryReader()
        : this(ReadFromDatabase)
    {
    }

    internal TaskSummaryReader(
        Func<
            DateTime,
            DateTime,
            TaskSummaryRawData> readData)
    {
        _readData = readData
            ?? throw new ArgumentNullException(
                nameof(readData));
    }

    public TaskSummarySnapshot Read(
        DateTime displayedMonth)
    {
        DateTime month =
            TaskSummarySnapshot.NormalizeMonth(
                displayedMonth);
        DateTime gridStart =
            CalendarMonthComposer.GetGridStart(month);
        DateTime gridEnd = gridStart.AddDays(
            CalendarMonthComposer.DayCount);
        try
        {
            TaskSummaryRawData data =
                _readData(gridStart, gridEnd);
            IReadOnlyDictionary<
                DateTime,
                CalendarFocusSummary> focusByDate =
                data.Sessions
                    .GroupBy(session =>
                        session.StartTime.Date)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                            new CalendarFocusSummary(
                                group.Count(),
                                group.Sum(session =>
                                    session.DurationMinutes)));
            return new TaskSummarySnapshot(
                true,
                month,
                Math.Max(
                    0,
                    data.OpenTaskCount),
                focusByDate);
        }
        catch
        {
            return TaskSummarySnapshot.Invalid(
                month);
        }
    }

    private static TaskSummaryRawData ReadFromDatabase(
        DateTime gridStart,
        DateTime gridEnd)
    {
        using var context = new AppDbContext();
        int openTaskCount = context.Todos.Count(
            item =>
                item.ParentId != null
                && !item.IsCompleted);
        List<TaskSummarySession> sessions =
            context.PomodoroSessions
                .AsNoTracking()
                .Where(session =>
                    session.Status == "Completed"
                    && session.StartTime >= gridStart
                    && session.StartTime < gridEnd)
                .Select(session =>
                    new TaskSummarySession(
                        session.StartTime,
                        session.DurationMinutes))
                .ToList();
        return new TaskSummaryRawData(
            openTaskCount,
            sessions);
    }
}
