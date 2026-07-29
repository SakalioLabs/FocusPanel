using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarWindowCyclePolicyTests
{
    private static readonly WindowReference[]
        Windows =
        {
            new(
                new IntPtr(1),
                "窗口一"),
            new(
                new IntPtr(2),
                "窗口二",
                IsActive: true),
            new(
                new IntPtr(3),
                "窗口三")
        };

    [Fact]
    public void WheelDown_SelectsNextWindow()
    {
        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                Windows,
                wheelDelta: -120);

        Assert.Equal(
            new IntPtr(3),
            target?.Handle);
    }

    [Fact]
    public void WheelUp_SelectsPreviousWindow()
    {
        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                Windows,
                wheelDelta: 120);

        Assert.Equal(
            new IntPtr(1),
            target?.Handle);
    }

    [Theory]
    [InlineData(-120, 1)]
    [InlineData(120, 3)]
    public void Cycle_WrapsAtBothEnds(
        int wheelDelta,
        int expectedHandle)
    {
        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                Windows,
                wheelDelta,
                preferredCurrentHandle:
                    wheelDelta < 0
                        ? new IntPtr(3)
                        : new IntPtr(1));

        Assert.Equal(
            new IntPtr(expectedHandle),
            target?.Handle);
    }

    [Fact]
    public void PreferredHandle_ContinuesRapidCycleBeforeSnapshotRefresh()
    {
        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                Windows,
                wheelDelta: -120,
                preferredCurrentHandle:
                    new IntPtr(3));

        Assert.Equal(
            new IntPtr(1),
            target?.Handle);
    }

    [Theory]
    [InlineData(-120, 1)]
    [InlineData(120, 3)]
    public void NoActiveWindow_UsesDirectionEdge(
        int wheelDelta,
        int expectedHandle)
    {
        var inactive = new[]
        {
            Windows[0],
            Windows[2]
        };

        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                inactive,
                wheelDelta);

        Assert.Equal(
            new IntPtr(expectedHandle),
            target?.Handle);
    }

    [Fact]
    public void SingleWindowOrZeroDelta_DoesNotConsumeScroll()
    {
        Assert.Null(
            TaskbarWindowCyclePolicy.SelectTarget(
                new[] { Windows[0] },
                wheelDelta: -120));
        Assert.Null(
            TaskbarWindowCyclePolicy.SelectTarget(
                Windows,
                wheelDelta: 0));
    }
}
