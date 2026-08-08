using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
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
            "左键打开此应用窗口总览，Ctrl+左键或 Ctrl+滚轮循环窗口，右键管理应用；"
            + "Shift+左键或中键启动新实例；"
            + "Ctrl+Shift+左键以管理员身份启动；"
            + "可拖入文件用此应用打开",
            item.InteractionHint);
    }

    [Fact]
    public void BackgroundApplication_DescribesRunningState()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: false);

        Assert.Equal(
            "后台运行 · 1 个窗口",
            item.StatusSummary);
        Assert.Equal(
            "左键切换，右键管理应用；"
            + "Shift+左键或中键启动新实例；"
            + "Ctrl+Shift+左键以管理员身份启动；"
            + "可拖入文件用此应用打开",
            item.InteractionHint);
    }

    [Fact]
    public void BackgroundOnlyApplication_OffersPanelRecoveryAction()
    {
        TaskbarAppItem item = Running(
            windowCount: 0,
            active: false);

        Assert.True(item.IsBackgroundOnly);
        Assert.True(item.IsRunning);
        Assert.Equal(
            "后台运行 · 无可见窗口",
            item.StatusSummary);
        Assert.Equal(
            "后台运行 · 无可见窗口",
            item.WindowSummary);
        Assert.StartsWith(
            "左键请求应用打开界面，右键管理应用",
            item.InteractionHint);
        Assert.False(item.HasWindowPreview);
    }

    [Fact]
    public void MinimizedApplication_DescribesRestoreInsteadOfAmbiguousToggle()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: false,
            state: TrackedWindowState.Minimized);

        Assert.True(item.IsFullyMinimized);
        Assert.Equal(
            "已最小化 · 1 个窗口",
            item.StatusSummary);
        Assert.Equal(
            "编辑器，已最小化 · 1 个窗口",
            item.AccessibleName);
        Assert.StartsWith(
            "左键还原并切换，右键管理应用",
            item.InteractionHint);
        Assert.Equal(
            "• 文档 1 · 已最小化",
            item.WindowPreviewText);
    }

    [Fact]
    public void ActiveSingleWindow_DescribesTheActualMinimizeAction()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: true);

        Assert.False(item.IsFullyMinimized);
        Assert.StartsWith(
            "左键最小化，右键管理应用",
            item.InteractionHint);
        Assert.Equal(
            "• 文档 1 · 当前窗口",
            item.WindowPreviewText);
    }

    [Fact]
    public void WindowPreview_LabelsMaximizedWindowsWithoutChangingNormalTitles()
    {
        TaskbarAppItem item = Running(
            windowCount: 1,
            active: false,
            state: TrackedWindowState.Maximized);

        Assert.False(item.IsFullyMinimized);
        Assert.Equal(
            "后台运行 · 1 个窗口",
            item.StatusSummary);
        Assert.Equal(
            "• 文档 1 · 已最大化",
            item.WindowPreviewText);
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
    public void WindowPreview_ShowsActiveAndTopmostTogether()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "播放器",
            RunningTask = new WindowTaskItem
            {
                AppKey = "player",
                IdentityKey = "exe:player",
                DisplayName = "播放器",
                Windows = new[]
                {
                    new WindowReference(
                        new IntPtr(1),
                        "正在播放",
                        IsActive: true,
                        IsTopmost: true)
                }
            }
        };

        Assert.Equal(
            "• 正在播放 · 当前窗口 · 已置顶",
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
            "左键启动，右键管理应用；Shift+左键或中键启动新实例；"
            + "Ctrl+Shift+左键以管理员身份启动；"
            + "可拖入文件用此应用打开；"
            + "Alt+↑/↓调整固定顺序",
            item.InteractionHint);
        Assert.Contains(
            "↑/↓浏览应用，Home/End 到首尾，PageUp/PageDown 翻页",
            item.AccessibleInteractionHint);
        Assert.True(item.CanLaunchElevated);
    }

    [Fact]
    public void PackagedApplication_DoesNotPromiseElevation()
    {
        var item = new TaskbarAppItem
        {
            DisplayName = "商店应用",
            LaunchItem = new AppLaunchItem
            {
                DisplayName = "商店应用",
                LaunchKind =
                    AppLaunchKind.ShellApp,
                LaunchTarget =
                    "Contoso.App_123!App"
            }
        };

        Assert.False(item.CanLaunchElevated);
        Assert.Null(
            item.CreateElevatedLaunchItem());
        Assert.DoesNotContain(
            "管理员",
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
            "左键切换，右键管理应用",
            item.InteractionHint);
    }

    [Fact]
    public void FileDropTarget_IsExplicitAndReversible()
    {
        var item =
            new TaskbarAppItem();
        var changes =
            new List<string?>();
        item.PropertyChanged +=
            (_, args) =>
                changes.Add(
                    args.PropertyName);

        item.SetFileDropTarget(true);
        Assert.True(
            item.IsFileDropTarget);
        Assert.Contains(
            nameof(
                TaskbarAppItem
                    .IsFileDropTarget),
            changes);

        changes.Clear();
        item.SetFileDropTarget(false);
        Assert.False(
            item.IsFileDropTarget);
        Assert.Single(
            changes);
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

    [Fact]
    public void ShortcutSlot_UpdatesTooltipAndAccessibility()
    {
        TaskbarAppItem item =
            Running(
                windowCount: 1,
                active: false);
        var changed =
            new List<string?>();
        item.PropertyChanged +=
            (_, e) =>
                changed.Add(
                    e.PropertyName);

        item.SetShortcutState(
            new TaskbarSlotShortcutState(
                3,
                CanActivateOrLaunch: true,
                CanLaunchNewInstance: true));

        Assert.True(
            item.HasShortcutGesture);
        Assert.Equal(
            "Ctrl+Alt+3",
            item.ShortcutGestureText);
        Assert.Equal(
            "3",
            item.ShortcutSlotText);
        Assert.Contains(
            "快速键 Ctrl+Alt+3",
            item.AccessibleName);
        Assert.Contains(
            "Ctrl+Alt+3 启动或切换",
            item.InteractionHint);
        Assert.Contains(
            "加 Shift 启动新实例",
            item.InteractionHint);
        Assert.Contains(
            nameof(
                TaskbarAppItem
                    .InteractionHint),
            changed);
        Assert.Contains(
            nameof(
                TaskbarAppItem
                    .AccessibleInteractionHint),
            changed);

        item.SetShortcutState(
            new TaskbarSlotShortcutState(
                3,
                CanActivateOrLaunch: true,
                CanLaunchNewInstance: false));

        Assert.DoesNotContain(
            "加 Shift",
            item.InteractionHint);

        item.SetShortcutState(
            TaskbarSlotShortcutState.None);

        Assert.False(
            item.HasShortcutGesture);
        Assert.Equal(
            string.Empty,
            item.ShortcutSlotText);
        Assert.DoesNotContain(
            "Ctrl+Alt",
            item.InteractionHint);
    }

    [Fact]
    public void JumpListAppId_PrefersRuntimeAndFallsBackToIdentity()
    {
        var running =
            new TaskbarAppItem
            {
                IdentityKey = "aumid:runtime.app",
                DisplayName = "编辑器",
                RunningTask =
                    new WindowTaskItem
                    {
                        IdentityKey =
                            "aumid:runtime.app",
                        AppKey =
                            "runtime",
                        DisplayName =
                            "编辑器",
                        ApplicationUserModelId =
                            "Runtime.App",
                        Windows =
                            new[]
                            {
                                new
                                    WindowReference(
                                        new IntPtr(1),
                                        "文档")
                            }
                    }
            };

        Assert.Equal(
            "Runtime.App",
            running
                .JumpListApplicationUserModelId);

        var pinned =
            new TaskbarAppItem
            {
                IdentityKey =
                    "aumid:contoso.editor",
                DisplayName =
                    "编辑器"
            };
        Assert.Equal(
            "contoso.editor",
            pinned
                .JumpListApplicationUserModelId);
        Assert.Null(
            new TaskbarAppItem
            {
                IdentityKey =
                    @"exe:c:\apps\editor.exe"
            }.JumpListApplicationUserModelId);
        Assert.Equal(
            "Contoso.Editor_Exact!App",
            new TaskbarAppItem
            {
                IdentityKey =
                    "aumid:contoso.editor_exact!app",
                LaunchItem =
                    new AppLaunchItem
                    {
                        ApplicationUserModelId =
                            "Contoso.Editor_Exact!App"
                    }
            }.JumpListApplicationUserModelId);
    }

    [Fact]
    public void DropPlacement_ExposesOnlyOneCueAndClears()
    {
        var item = new TaskbarAppItem();
        var changes = new List<string?>();
        item.PropertyChanged +=
            (_, e) =>
                changes.Add(
                    e.PropertyName);

        item.SetDropPlacement(
            TaskbarDropPlacement.Before);

        Assert.True(item.ShowsDropBefore);
        Assert.False(item.ShowsDropAfter);

        item.SetDropPlacement(
            TaskbarDropPlacement.After);

        Assert.False(item.ShowsDropBefore);
        Assert.True(item.ShowsDropAfter);

        item.SetDropPlacement(null);

        Assert.False(item.ShowsDropBefore);
        Assert.False(item.ShowsDropAfter);
        Assert.Equal(
            3,
            changes.Count(name =>
                name
                == nameof(
                    TaskbarAppItem
                        .ShowsDropBefore)));
        Assert.Equal(
            3,
            changes.Count(name =>
                name
                == nameof(
                    TaskbarAppItem
                        .ShowsDropAfter)));
    }

    private static TaskbarAppItem Running(
        int windowCount,
        bool active,
        TrackedWindowState state =
            TrackedWindowState.Normal)
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
                    active && index == 0,
                    state);
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
