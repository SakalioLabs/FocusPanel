using System;
using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskSummaryReaderTests
{
    [Fact]
    public void Read_UsesVisibleCalendarGridAndGroupsSessions()
    {
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        var reader = new TaskSummaryReader(
            (start, end) =>
            {
                capturedStart = start;
                capturedEnd = end;
                return new TaskSummaryRawData(
                    7,
                    new List<TaskSummarySession>
                    {
                        new(
                            new DateTime(
                                2026,
                                7,
                                8,
                                9,
                                0,
                                0),
                            25),
                        new(
                            new DateTime(
                                2026,
                                7,
                                8,
                                14,
                                0,
                                0),
                            40),
                        new(
                            new DateTime(
                                2026,
                                7,
                                10,
                                10,
                                0,
                                0),
                            15)
                    });
            });

        TaskSummarySnapshot snapshot =
            reader.Read(
                new DateTime(2026, 7, 29));

        Assert.True(snapshot.IsValid);
        Assert.Equal(
            new DateTime(2026, 7, 1),
            snapshot.DisplayedMonth);
        Assert.Equal(7, snapshot.OpenTaskCount);
        Assert.Equal(
            CalendarMonthComposer.GetGridStart(
                new DateTime(2026, 7, 1)),
            capturedStart);
        Assert.Equal(
            capturedStart?.AddDays(
                CalendarMonthComposer.DayCount),
            capturedEnd);
        Assert.Equal(
            new CalendarFocusSummary(2, 65),
            snapshot.FocusByDate[
                new DateTime(2026, 7, 8)]);
        Assert.Equal(
            new CalendarFocusSummary(1, 15),
            snapshot.FocusByDate[
                new DateTime(2026, 7, 10)]);
    }

    [Fact]
    public void Read_ClampsInvalidTaskCount()
    {
        var reader = new TaskSummaryReader(
            (_, _) =>
                new TaskSummaryRawData(
                    -4,
                    Array.Empty<
                        TaskSummarySession>()));

        TaskSummarySnapshot snapshot =
            reader.Read(
                new DateTime(2026, 7, 1));

        Assert.True(snapshot.IsValid);
        Assert.Equal(0, snapshot.OpenTaskCount);
    }

    [Fact]
    public void ReadFailure_ReturnsInvalidSnapshotForRequestedMonth()
    {
        var reader = new TaskSummaryReader(
            (_, _) =>
                throw new InvalidOperationException(
                    "database is busy"));

        TaskSummarySnapshot snapshot =
            reader.Read(
                new DateTime(2026, 8, 19));

        Assert.False(snapshot.IsValid);
        Assert.Equal(
            new DateTime(2026, 8, 1),
            snapshot.DisplayedMonth);
        Assert.Empty(snapshot.FocusByDate);
    }
}
