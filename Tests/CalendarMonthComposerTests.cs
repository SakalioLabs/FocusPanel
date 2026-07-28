using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CalendarMonthComposerTests
{
    [Fact]
    public void Compose_AlwaysBuildsSixMondayFirstWeeks()
    {
        IReadOnlyList<CalendarDayItem> days =
            CalendarMonthComposer.Compose(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 28),
                new DateTime(2026, 7, 28),
                EmptyFocus());

        Assert.Equal(42, days.Count);
        Assert.Equal(
            DayOfWeek.Monday,
            days[0].Date.DayOfWeek);
        Assert.Equal(
            new DateTime(2026, 6, 29),
            days[0].Date);
        Assert.Equal(
            new DateTime(2026, 8, 9),
            days[^1].Date);
    }

    [Fact]
    public void Compose_MarksTodaySelectionAndAdjacentMonths()
    {
        IReadOnlyList<CalendarDayItem> days =
            CalendarMonthComposer.Compose(
                new DateTime(2026, 7, 15),
                new DateTime(2026, 7, 28),
                new DateTime(2026, 7, 28),
                EmptyFocus());

        CalendarDayItem selected =
            Assert.Single(days, item => item.IsSelected);
        CalendarDayItem today =
            Assert.Single(days, item => item.IsToday);

        Assert.Equal(
            new DateTime(2026, 7, 28),
            selected.Date);
        Assert.Same(selected, today);
        Assert.True(selected.IsCurrentMonth);
        Assert.Contains(
            days,
            item => !item.IsCurrentMonth);
    }

    [Fact]
    public void Compose_MapsFocusHistoryToExactDate()
    {
        var focus = new Dictionary<
            DateTime,
            CalendarFocusSummary>
        {
            [new DateTime(2026, 7, 12)] =
                new CalendarFocusSummary(3, 75)
        };

        CalendarDayItem day =
            CalendarMonthComposer.Compose(
                    new DateTime(2026, 7, 1),
                    new DateTime(2026, 7, 12),
                    new DateTime(2026, 7, 28),
                    focus)
                .Single(item =>
                    item.Date
                    == new DateTime(2026, 7, 12));

        Assert.True(day.HasFocus);
        Assert.Equal(3, day.FocusSessionCount);
        Assert.Equal(75, day.FocusMinutes);
        Assert.Contains("3 次专注", day.AccessibleName);
        Assert.Contains("75 分钟", day.AccessibleName);
    }

    [Fact]
    public void Compose_HandlesLeapYearFebruary()
    {
        IReadOnlyList<CalendarDayItem> days =
            CalendarMonthComposer.Compose(
                new DateTime(2028, 2, 20),
                new DateTime(2028, 2, 29),
                new DateTime(2028, 2, 1),
                EmptyFocus());

        Assert.Contains(
            days,
            item =>
                item.Date
                    == new DateTime(2028, 2, 29)
                && item.IsCurrentMonth);
        Assert.Equal(
            29,
            days.Count(item => item.IsCurrentMonth));
    }

    [Fact]
    public void GetGridStart_NormalizesAnyDateInMonth()
    {
        DateTime expected = new(2026, 6, 29);

        Assert.Equal(
            expected,
            CalendarMonthComposer.GetGridStart(
                new DateTime(2026, 7, 1)));
        Assert.Equal(
            expected,
            CalendarMonthComposer.GetGridStart(
                new DateTime(2026, 7, 31)));
    }

    private static IReadOnlyDictionary<
        DateTime,
        CalendarFocusSummary> EmptyFocus() =>
        new Dictionary<DateTime, CalendarFocusSummary>();
}
