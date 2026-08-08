using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowDisplayOverviewPolicyTests
{
    [Fact]
    public void CreateOptions_CountsDistinctWindowsAndUsesPhysicalOrder()
    {
        var applications = new[]
        {
            Application(
                Window(1, "右一", "RIGHT", "显示器 2 · 主屏", 1),
                Window(2, "左一", "LEFT", "显示器 1", 0)),
            Application(
                Window(1, "重复句柄", "RIGHT", "显示器 2 · 主屏", 1),
                Window(3, "右二", "RIGHT", "显示器 2 · 主屏", 1))
        };

        var options =
            WindowDisplayOverviewPolicy
                .CreateOptions(applications);

        Assert.Collection(
            options,
            all =>
            {
                Assert.Equal(
                    WindowDisplayOverviewPolicy
                        .AllDisplaysValue,
                    all.Value);
                Assert.Equal(
                    "全部屏幕 · 3",
                    all.DisplayName);
            },
            left =>
            {
                Assert.Equal("LEFT", left.Value);
                Assert.Equal(
                    "显示器 1 · 1 个窗口",
                    left.DisplayName);
            },
            right =>
            {
                Assert.Equal("RIGHT", right.Value);
                Assert.Equal(
                    "显示器 2 · 主屏 · 2 个窗口",
                    right.DisplayName);
            });
        Assert.True(
            WindowDisplayOverviewPolicy
                .IsUseful(options));
    }

    [Fact]
    public void CreateOptions_LeavesUnknownWindowsInAllWithoutInventingAFilter()
    {
        var options =
            WindowDisplayOverviewPolicy
                .CreateOptions(
                    new[]
                    {
                        Application(
                            new WindowReference(
                                new IntPtr(4),
                                "未知屏幕"))
                    });

        WindowDisplayFilterOption all =
            Assert.Single(options);
        Assert.Equal(1, all.WindowCount);
        Assert.False(
            WindowDisplayOverviewPolicy
                .IsUseful(options));
    }

    [Fact]
    public void NormalizeSelection_PreservesAvailableDeviceAndResetsDisconnectedOne()
    {
        var options =
            WindowDisplayOverviewPolicy
                .CreateOptions(
                    new[]
                    {
                        Application(
                            Window(
                                5,
                                "左",
                                "LEFT",
                                "显示器 1",
                                0),
                            Window(
                                6,
                                "右",
                                "RIGHT",
                                "显示器 2",
                                1))
                    });

        Assert.Equal(
            "RIGHT",
            WindowDisplayOverviewPolicy
                .NormalizeSelection(
                    "right",
                    options));
        Assert.Equal(
            WindowDisplayOverviewPolicy
                .AllDisplaysValue,
            WindowDisplayOverviewPolicy
                .NormalizeSelection(
                    "DISCONNECTED",
                    options));
    }

    [Fact]
    public void WindowReference_LocalizesStateAndIncludesDisplay()
    {
        var window = new WindowReference(
            new IntPtr(7),
            "文档",
            State: TrackedWindowState.Maximized,
            DisplayDeviceName: "RIGHT",
            DisplayLabel: "显示器 2 · 主屏",
            DisplayOrder: 1);

        Assert.True(window.HasDisplayLabel);
        Assert.Equal(
            "已最大化 · 显示器 2 · 主屏",
            window.StateAndDisplayText);
    }

    private static WindowTaskItem Application(
        params WindowReference[] windows) =>
        new()
        {
            IdentityKey = Guid.NewGuid()
                .ToString("N"),
            Windows = windows
        };

    private static WindowReference Window(
        long handle,
        string title,
        string device,
        string display,
        int order) =>
        new(
            new IntPtr(handle),
            title,
            DisplayDeviceName: device,
            DisplayLabel: display,
            DisplayOrder: order);
}
