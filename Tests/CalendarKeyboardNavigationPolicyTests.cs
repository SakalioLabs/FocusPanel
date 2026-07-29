using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    CalendarKeyboardNavigationPolicyTests
{
    [Theory]
    [InlineData(
        CalendarNavigationAction.PreviousDay,
        2026,
        7,
        14)]
    [InlineData(
        CalendarNavigationAction.NextDay,
        2026,
        7,
        16)]
    [InlineData(
        CalendarNavigationAction.PreviousWeek,
        2026,
        7,
        8)]
    [InlineData(
        CalendarNavigationAction.NextWeek,
        2026,
        7,
        22)]
    public void ArrowActions_MoveByDayOrWeek(
        CalendarNavigationAction action,
        int year,
        int month,
        int day)
    {
        Assert.Equal(
            new DateTime(
                year,
                month,
                day),
            CalendarKeyboardNavigationPolicy
                .GetTargetDate(
                    new DateTime(
                        2026,
                        7,
                        15),
                    action,
                    DateTime.MinValue));
    }

    [Theory]
    [InlineData(
        2024,
        1,
        31,
        CalendarNavigationAction.NextMonth,
        2024,
        2,
        29)]
    [InlineData(
        2025,
        3,
        31,
        CalendarNavigationAction.PreviousMonth,
        2025,
        2,
        28)]
    [InlineData(
        2026,
        12,
        30,
        CalendarNavigationAction.NextMonth,
        2027,
        1,
        30)]
    public void PageActions_PreserveDayAndClampMonth(
        int sourceYear,
        int sourceMonth,
        int sourceDay,
        CalendarNavigationAction action,
        int targetYear,
        int targetMonth,
        int targetDay)
    {
        Assert.Equal(
            new DateTime(
                targetYear,
                targetMonth,
                targetDay),
            CalendarKeyboardNavigationPolicy
                .GetTargetDate(
                    new DateTime(
                        sourceYear,
                        sourceMonth,
                        sourceDay),
                    action,
                    DateTime.MinValue));
    }

    [Fact]
    public void TodayAction_UsesProvidedClockDate()
    {
        Assert.Equal(
            new DateTime(
                2030,
                5,
                6),
            CalendarKeyboardNavigationPolicy
                .GetTargetDate(
                    new DateTime(
                        2026,
                        7,
                        15),
                    CalendarNavigationAction
                        .Today,
                    new DateTime(
                        2030,
                        5,
                        6,
                        23,
                        59,
                        0)));
    }

    [Theory]
    [InlineData(
        CalendarNavigationAction.PreviousDay)]
    [InlineData(
        CalendarNavigationAction.PreviousWeek)]
    [InlineData(
        CalendarNavigationAction.PreviousMonth)]
    public void MinimumDate_DoesNotOverflow(
        CalendarNavigationAction action)
    {
        Assert.Equal(
            DateTime.MinValue,
            CalendarKeyboardNavigationPolicy
                .GetTargetDate(
                    DateTime.MinValue,
                    action,
                    DateTime.Today));
    }
}
