using System;
using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskSummaryApplyPolicyTests
{
    [Fact]
    public void InvalidSnapshot_PreservesCurrentSummary()
    {
        TaskSummaryApplyDecision decision =
            TaskSummaryApplyPolicy.GetDecision(
                TaskSummarySnapshot.Invalid(
                    new DateTime(2026, 7, 1)),
                new DateTime(2026, 7, 29));

        Assert.False(decision.ApplyOpenTaskCount);
        Assert.False(decision.ApplyCalendar);
    }

    [Fact]
    public void CurrentMonthSnapshot_AppliesCountAndCalendar()
    {
        TaskSummarySnapshot snapshot = ValidSnapshot(
            new DateTime(2026, 7, 1));

        TaskSummaryApplyDecision decision =
            TaskSummaryApplyPolicy.GetDecision(
                snapshot,
                new DateTime(2026, 7, 29));

        Assert.True(decision.ApplyOpenTaskCount);
        Assert.True(decision.ApplyCalendar);
    }

    [Fact]
    public void StaleMonthSnapshot_UpdatesCountButNotCalendar()
    {
        TaskSummarySnapshot snapshot = ValidSnapshot(
            new DateTime(2026, 6, 1));

        TaskSummaryApplyDecision decision =
            TaskSummaryApplyPolicy.GetDecision(
                snapshot,
                new DateTime(2026, 7, 1));

        Assert.True(decision.ApplyOpenTaskCount);
        Assert.False(decision.ApplyCalendar);
    }

    private static TaskSummarySnapshot ValidSnapshot(
        DateTime month) =>
        new(
            true,
            month,
            3,
            new Dictionary<
                DateTime,
                CalendarFocusSummary>());
}
