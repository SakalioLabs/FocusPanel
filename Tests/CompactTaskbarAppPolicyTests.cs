using System;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CompactTaskbarAppPolicyTests
{
    [Fact]
    public void Select_KeepsPinnedAndWindowedAppsButExcludesBackgroundOnly()
    {
        TaskbarAppItem pinnedStopped =
            Pinned("固定但未运行");
        TaskbarAppItem visibleRunning =
            Running(
                "有窗口",
                new WindowReference(
                    new IntPtr(7),
                    "窗口"));
        TaskbarAppItem backgroundOnly =
            Running("纯后台");
        TaskbarAppItem minimizedRunning =
            Running(
                "已最小化",
                new WindowReference(
                    new IntPtr(8),
                    "最小化窗口",
                    State:
                        TrackedWindowState
                            .Minimized));
        var staleUnpinned = new TaskbarAppItem
        {
            IdentityKey = "stale",
            DisplayName = "已停止临时项"
        };

        TaskbarAppItem[] result =
            CompactTaskbarAppPolicy
                .Select(
                    new[]
                    {
                        backgroundOnly,
                        pinnedStopped,
                        staleUnpinned,
                        visibleRunning,
                        minimizedRunning
                    })
                .ToArray();

        Assert.Equal(
            new[]
            {
                "固定但未运行",
                "有窗口",
                "已最小化"
            },
            result.Select(item =>
                item.DisplayName));
        Assert.DoesNotContain(
            result,
            item => item.IsBackgroundOnly);
    }

    [Fact]
    public void Select_KeepsExplicitlyPinnedBackgroundApp()
    {
        TaskbarAppItem pinnedBackground =
            new()
            {
                IdentityKey =
                    "run:用户固定的后台应用",
                DisplayName = "用户固定的后台应用",
                RunningTask = new WindowTaskItem
                {
                    IdentityKey =
                        "run:用户固定的后台应用",
                    DisplayName =
                        "用户固定的后台应用",
                    Windows =
                        Array.Empty<WindowReference>()
                },
                PinnedLaunches = new[]
                {
                    new AppLaunchItem
                    {
                        IdentityKey =
                            "run:用户固定的后台应用",
                        DisplayName =
                            "用户固定的后台应用",
                        LaunchKind =
                            AppLaunchKind.Executable,
                        LaunchTarget =
                            @"C:\Pinned.exe",
                        IsPinned = true
                    }
                }
            };

        Assert.True(
            CompactTaskbarAppPolicy
                .ShouldShow(
                    pinnedBackground));
    }

    private static TaskbarAppItem Pinned(
        string name) =>
        new()
        {
            IdentityKey = "pin:" + name,
            DisplayName = name,
            PinnedLaunches = new[]
            {
                new AppLaunchItem
                {
                    IdentityKey =
                        "pin:" + name,
                    DisplayName = name,
                    LaunchKind =
                        AppLaunchKind.Executable,
                    LaunchTarget =
                        @"C:\Pinned.exe",
                    IsPinned = true
                }
            }
        };

    private static TaskbarAppItem Running(
        string name,
        params WindowReference[] windows) =>
        new()
        {
            IdentityKey = "run:" + name,
            DisplayName = name,
            RunningTask = new WindowTaskItem
            {
                IdentityKey = "run:" + name,
                DisplayName = name,
                Windows = windows
            }
        };
}
