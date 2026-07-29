using System;
using System.Linq;
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
            "左键打开窗口列表，右键管理应用；Shift+左键或中键启动新实例",
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
            "左键切换或最小化，右键管理应用；Shift+左键或中键启动新实例",
            item.InteractionHint);
    }

    [Fact]
    public void MultiWindowApplication_ProvidesBadgeAndBoundedPreview()
    {
        TaskbarAppItem item = Running(
            windowCount: 5,
            active: false);

        Assert.True(item.HasMultipleWindows);
        Assert.True(item.HasWindowPreview);
        Assert.Equal(
            "5",
            item.WindowCountBadgeText);
        string[] lines =
            item.WindowPreviewText.Split(
                Environment.NewLine);
        Assert.Equal(4, lines.Length);
        Assert.Equal("• 文档 1", lines[0]);
        Assert.Equal("• 文档 3", lines[2]);
        Assert.Equal(
            "• 另有 2 个窗口",
            lines[3]);
    }

    [Fact]
    public void SingleWindowApplication_HidesBadgeButKeepsTitlePreview()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: false);

        Assert.False(item.HasMultipleWindows);
        Assert.True(item.HasWindowPreview);
        Assert.Equal(
            "• 文档 1",
            item.WindowPreviewText);
    }

    [Fact]
    public void WindowPreview_NormalizesLineBreakAndFallsBackForBlankTitle()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "编辑器",
            RunningTask = new WindowTaskItem
            {
                AppKey = "editor",
                IdentityKey = "exe:editor",
                DisplayName = "编辑器",
                Windows = new[]
                {
                    new WindowReference(
                        new IntPtr(1),
                        "  第一行\r\n第二行  "),
                    new WindowReference(
                        new IntPtr(2),
                        "   ")
                }
            }
        };

        Assert.Equal(
            "• 第一行  第二行"
            + Environment.NewLine
            + "• 编辑器",
            item.WindowPreviewText);
    }

    [Fact]
    public void LargeWindowGroup_CapsBadgeAndTruncatesUnsafeTitle()
    {
        var windows =
            new WindowReference[101];
        windows[0] = new WindowReference(
            new IntPtr(1),
            string.Concat(
                Enumerable.Repeat(
                    "🙂",
                    80)));
        for (int index = 1;
             index < windows.Length;
             index++)
        {
            windows[index] =
                new WindowReference(
                    new IntPtr(index + 1),
                    $"窗口 {index + 1}");
        }

        var item = new TaskbarAppItem
        {
            DisplayName = "编辑器",
            RunningTask = new WindowTaskItem
            {
                AppKey = "editor",
                IdentityKey = "exe:editor",
                DisplayName = "编辑器",
                Windows = windows
            }
        };

        Assert.Equal(
            "99+",
            item.WindowCountBadgeText);
        string firstPreviewLine =
            item.WindowPreviewText.Split(
                Environment.NewLine)[0];
        Assert.DoesNotContain(
            '\r',
            firstPreviewLine);
        Assert.DoesNotContain(
            '\n',
            firstPreviewLine);
        Assert.EndsWith(
            "…",
            firstPreviewLine);
        Assert.False(
            char.IsHighSurrogate(
                firstPreviewLine[^2]));
        Assert.Contains(
            "另有 98 个窗口",
            item.WindowPreviewText);
    }

    [Fact]
    public void PinnedStoppedApplication_DescribesLaunchAction()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "编辑器",
            LaunchItem = new AppLaunchItem
            {
                DisplayName = "编辑器",
                LaunchKind = AppLaunchKind.Executable,
                LaunchTarget = @"C:\Apps\Editor.exe"
            },
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
            "左键启动，右键管理应用；Shift+左键或中键启动新实例",
            item.InteractionHint);
    }

    [Fact]
    public void ProtectedRunningApplication_DoesNotPromiseNewInstance()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "受保护应用",
            RunningTask = new WindowTaskItem
            {
                AppKey = "protected",
                IdentityKey = "temporary:protected",
                DisplayName = "受保护应用",
                Windows = new[]
                {
                    new WindowReference(
                        new IntPtr(1),
                        "受保护窗口")
                }
            }
        };

        Assert.False(item.CanLaunchNewInstance);
        Assert.Equal(
            "左键切换或最小化，右键管理应用",
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
        Assert.False(item.HasWindowPreview);
        Assert.Equal(
            string.Empty,
            item.WindowPreviewText);
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
                ExecutablePath =
                    @"C:\Apps\Editor.exe",
                IsActive = active,
                Windows = windows
            }
        };
    }
}
