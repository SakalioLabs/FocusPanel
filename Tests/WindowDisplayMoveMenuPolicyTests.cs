using System;
using System.Drawing;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowDisplayMoveMenuPolicyTests
{
    [Fact]
    public void CreateOptions_OrdersPhysicalDisplaysAndLabelsPrimary()
    {
        ShellDisplaySnapshot[] displays =
        {
            new(
                new Rectangle(0, -100, 1920, 1080),
                true,
                @"\\.\DISPLAY1",
                new Rectangle(0, -100, 1920, 1040)),
            new(
                new Rectangle(-1280, 120, 1280, 720),
                false,
                @"\\.\DISPLAY2")
        };

        var options =
            WindowDisplayMoveMenuPolicy
                .CreateOptions(displays);

        Assert.Collection(
            options,
            left =>
            {
                Assert.Equal(
                    @"\\.\DISPLAY2",
                    left.DeviceName);
                Assert.Equal(
                    "显示器 1 · 1280×720 · (-1280,120)",
                    left.DisplayName);
                Assert.Equal(
                    displays[1].Bounds,
                    left.WorkArea);
            },
            primary =>
            {
                Assert.Equal(
                    @"\\.\DISPLAY1",
                    primary.DeviceName);
                Assert.Equal(
                    "显示器 2 · 主屏 · 1920×1080 · (0,-100)",
                    primary.DisplayName);
                Assert.Equal(
                    displays[0].WorkingArea,
                    primary.WorkArea);
            });
    }

    [Fact]
    public void CreateOptions_FiltersInvalidOrUnnamedDisplays()
    {
        var options =
            WindowDisplayMoveMenuPolicy
                .CreateOptions(
                    new[]
                    {
                        new ShellDisplaySnapshot(
                            Rectangle.Empty,
                            true,
                            "PRIMARY"),
                        new ShellDisplaySnapshot(
                            new Rectangle(0, 0, 100, 100),
                            false,
                            "")
                    });

        Assert.Empty(options);
    }

    [Fact]
    public void ResolveWindow_MarksOnlyTheSingleCurrentDisplay()
    {
        WindowDisplayMoveOption[] options =
            Options();

        var states =
            WindowDisplayMoveMenuPolicy
                .ResolveWindow(
                    options,
                    area => area.Left != 100);

        Assert.True(states[0].CanMove);
        Assert.False(states[0].IsCurrent);
        Assert.False(states[1].CanMove);
        Assert.True(states[1].IsCurrent);
        Assert.True(states[2].CanMove);
        Assert.False(states[2].IsCurrent);
    }

    [Fact]
    public void ResolveWindow_AmbiguousFailuresDoNotClaimCurrentScreen()
    {
        WindowDisplayMoveOption[] options =
            Options();

        var states =
            WindowDisplayMoveMenuPolicy
                .ResolveWindow(
                    options,
                    area => area.Left == 200);

        Assert.All(
            states,
            state => Assert.False(
                state.IsCurrent));
        Assert.False(states[0].CanMove);
        Assert.False(states[1].CanMove);
        Assert.True(states[2].CanMove);
    }

    [Fact]
    public void ResolveWindow_IsolatesNativeProbeFailure()
    {
        WindowDisplayMoveOption[] options =
            Options();

        var states =
            WindowDisplayMoveMenuPolicy
                .ResolveWindow(
                    options,
                    area => area.Left switch
                    {
                        0 => throw new
                            InvalidOperationException(),
                        100 => false,
                        _ => true
                    });

        Assert.False(states[0].CanMove);
        Assert.False(states[1].CanMove);
        Assert.True(states[2].CanMove);
        Assert.All(
            states,
            state => Assert.False(
                state.IsCurrent));
    }

    private static WindowDisplayMoveOption[]
        Options() =>
        new[]
        {
            Option("一", 0),
            Option("二", 100),
            Option("三", 200)
        };

    private static WindowDisplayMoveOption Option(
        string name,
        int left) =>
        new(
            name,
            name,
            new Rectangle(left, 0, 100, 100));
}
