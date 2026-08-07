using System;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    TaskbarContextWindowPolicyTests
{
    [Fact]
    public void SmallList_PreservesOriginalOrder()
    {
        TaskbarContextWindowSlice result =
            TaskbarContextWindowPolicy.Select(
                new[]
                {
                    Window(1),
                    Window(2, isActive: true),
                    Window(3)
                });

        Assert.Equal(
            new[] { 1, 2, 3 },
            result.Windows.Select(window =>
                window.Handle.ToInt32()));
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(0, result.HiddenCount);
        Assert.False(result.HasHiddenWindows);
    }

    [Fact]
    public void LargeList_PutsActiveWindowFirst()
    {
        TaskbarContextWindowSlice result =
            TaskbarContextWindowPolicy.Select(
                new[]
                {
                    Window(1),
                    Window(2),
                    Window(3),
                    Window(4),
                    Window(5, isActive: true),
                    Window(6)
                });

        Assert.Equal(
            new[] { 5, 1, 2, 3 },
            result.Windows.Select(window =>
                window.Handle.ToInt32()));
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.HiddenCount);
        Assert.True(result.HasHiddenWindows);
    }

    [Fact]
    public void LargeListWithoutActive_KeepsFirstWindows()
    {
        TaskbarContextWindowSlice result =
            TaskbarContextWindowPolicy.Select(
                new[]
                {
                    Window(1),
                    Window(2),
                    Window(3),
                    Window(4),
                    Window(5)
                });

        Assert.Equal(
            new[] { 1, 2, 3, 4 },
            result.Windows.Select(window =>
                window.Handle.ToInt32()));
        Assert.Equal(1, result.HiddenCount);
    }

    [Fact]
    public void ZeroAndDuplicateHandles_DoNotInflateCount()
    {
        TaskbarContextWindowSlice result =
            TaskbarContextWindowPolicy.Select(
                new[]
                {
                    Window(0),
                    Window(1),
                    Window(1),
                    Window(2)
                },
                maximumVisible: 1);

        Assert.Single(result.Windows);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.HiddenCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLimit_IsRejected(
        int maximumVisible)
    {
        Assert.Throws<
            ArgumentOutOfRangeException>(() =>
            TaskbarContextWindowPolicy.Select(
                Array.Empty<WindowReference>(),
                maximumVisible));
    }

    private static WindowReference Window(
        int handle,
        bool isActive = false) =>
        new(
            new IntPtr(handle),
            $"窗口 {handle}",
            isActive);
}
