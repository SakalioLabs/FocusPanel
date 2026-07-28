using System;
using FocusPanel.Models;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarAppPresentationTests
{
    [Fact]
    public void ActiveApplication_DescribesCurrentUse()
    {
        TaskbarAppItem item = Running(
            windowCount: 2,
            active: true);

        Assert.Equal(
            "正在使用 · 2 个窗口",
            item.StatusSummary);
        Assert.Equal(
            "编辑器，正在使用 · 2 个窗口",
            item.AccessibleName);
        Assert.Equal(
            "左键打开窗口列表，右键管理应用",
            item.InteractionHint);
    }

    [Fact]
    public void BackgroundApplication_DescribesRunningState()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: false);

        Assert.Equal(
            "正在运行 · 1 个窗口",
            item.StatusSummary);
        Assert.Equal(
            "左键切换或最小化，右键管理应用",
            item.InteractionHint);
    }

    [Fact]
    public void PinnedStoppedApplication_DescribesLaunchAction()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "编辑器",
            PinnedLaunches = new[]
            {
                new AppLaunchItem
                {
                    DisplayName = "编辑器",
                    LaunchKind =
                        AppLaunchKind.Executable,
                    LaunchTarget =
                        @"C:\Apps\Editor.exe"
                }
            }
        };

        Assert.Equal(
            "已固定 · 未运行",
            item.StatusSummary);
        Assert.Equal(
            "编辑器，已固定 · 未运行",
            item.AccessibleName);
        Assert.Equal(
            "左键启动，右键管理应用",
            item.InteractionHint);
    }

    [Fact]
    public void UnpinnedStoppedFallback_RemainsExplicit()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "未知应用"
        };

        Assert.Equal(
            "未运行",
            item.StatusSummary);
        Assert.Equal(
            "未知应用，未运行",
            item.AccessibleName);
    }

    private static TaskbarAppItem Running(
        int windowCount,
        bool active)
    {
        var windows = new WindowReference[
            windowCount];
        for (int index = 0;
             index < windowCount;
             index++)
        {
            windows[index] =
                new WindowReference(
                    new IntPtr(index + 1),
                    $"文档 {index + 1}",
                    active && index == 0);
        }

        return new TaskbarAppItem
        {
            DisplayName = "编辑器",
            RunningTask = new WindowTaskItem
            {
                AppKey = "editor",
                IdentityKey = "exe:editor",
                DisplayName = "编辑器",
                IsActive = active,
                Windows = windows
            }
        };
    }
}
