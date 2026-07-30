using System;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellSearchPolicyTests
{
    [Fact]
    public void Compose_EmptyQueryPreservesApplicationOrderAndOmitsWindows()
    {
        AppLaunchItem[] applications =
        {
            App("固定二", "exe:c:\\two.exe"),
            App("固定一", "exe:c:\\one.exe")
        };

        var results = ShellSearchPolicy.Compose(
            applications,
            new[] { Running("记事本", "会议记录", 9) },
            string.Empty);

        Assert.Equal(
            new[] { "固定二", "固定一" },
            results.Select(item => item.DisplayName));
        Assert.All(
            results,
            item => Assert.Equal(
                ShellSearchResultKind.Application,
                item.Kind));
    }

    [Fact]
    public void Compose_ExactWindowTitleOutranksWeakApplicationSubstring()
    {
        var results = ShellSearchPolicy.Compose(
            new[] { App("项目会议记录工具", "exe:c:\\notes.exe") },
            new[] { Running("记事本", "会议记录", 11) },
            "会议记录");

        Assert.NotEmpty(results);
        ShellSearchResult first = results.First();
        Assert.Equal(ShellSearchResultKind.Window, first.Kind);
        Assert.Equal(new IntPtr(11), first.Window?.Handle);
    }

    [Fact]
    public void Compose_ExactApplicationNameWinsEqualWindowApplicationMatch()
    {
        var results = ShellSearchPolicy.Compose(
            new[] { App("记事本", "exe:c:\\notepad.exe") },
            new[] { Running("记事本", "未命名 - 记事本", 12) },
            "记事本");

        Assert.Equal(
            ShellSearchResultKind.Application,
            results[0].Kind);
    }

    [Fact]
    public void Compose_ActiveWindowWinsWhenWindowRanksTie()
    {
        WindowTaskItem running = new()
        {
            DisplayName = "浏览器",
            IdentityKey = "exe:c:\\browser.exe",
            Windows = new[]
            {
                new WindowReference(new IntPtr(21), "文档", false),
                new WindowReference(new IntPtr(22), "文档", true)
            }
        };

        var windows = ShellSearchPolicy.Compose(
                Array.Empty<AppLaunchItem>(),
                new[] { running },
                "文档")
            .ToArray();

        Assert.Equal(new IntPtr(22), windows[0].Window?.Handle);
        Assert.Equal(new IntPtr(21), windows[1].Window?.Handle);
    }

    [Fact]
    public void Compose_WindowStableKeyUsesHandleAndSurvivesTitleChanges()
    {
        ShellSearchResult first = Assert.Single(
            ShellSearchPolicy.Compose(
                Array.Empty<AppLaunchItem>(),
                new[] { Running("编辑器", "旧标题", 31) },
                "标题"));
        ShellSearchResult second = Assert.Single(
            ShellSearchPolicy.Compose(
                Array.Empty<AppLaunchItem>(),
                new[] { Running("编辑器", "新标题", 31) },
                "标题"));

        Assert.Equal(first.StableKey, second.StableKey);
    }

    [Fact]
    public void Compose_BlankWindowTitleUsesApplicationFallback()
    {
        ShellSearchResult result =
            Assert.Single(
                ShellSearchPolicy.Compose(
                        Array.Empty<AppLaunchItem>(),
                        new[]
                        {
                            Running(
                                "终端",
                                " ",
                                41)
                        },
                        "终端")
                    .Where(
                        item =>
                            item.Kind
                            == ShellSearchResultKind
                                .Window));

        Assert.Equal("终端 窗口", result.DisplayName);
        Assert.Equal(ShellSearchResultKind.Window, result.Kind);
    }

    [Fact]
    public void Compose_ReusesNormalizedCamelCaseAndAccentSearch()
    {
        var results = ShellSearchPolicy.Compose(
            new[] { App("CaféEditor", "exe:c:\\editor.exe") },
            Array.Empty<WindowTaskItem>(),
            "cafe editor");

        Assert.Equal("CaféEditor", Assert.Single(results).DisplayName);
    }

    [Theory]
    [InlineData("任务管理器", SystemManagementTool.TaskManager)]
    [InlineData("taskmgr", SystemManagementTool.TaskManager)]
    [InlineData("硬盘分区", SystemManagementTool.DiskManagement)]
    [InlineData("devmgmt", SystemManagementTool.DeviceManager)]
    [InlineData("admin terminal", SystemManagementTool.TerminalAdministrator)]
    public void Compose_FindsSystemCommandByNameOrAlias(
        string query,
        SystemManagementTool expectedTool)
    {
        ShellSearchResult result =
            Assert.Single(
                ShellSearchPolicy.Compose(
                    Array.Empty<AppLaunchItem>(),
                    Array.Empty<WindowTaskItem>(),
                    query));

        Assert.Equal(
            ShellSearchResultKind.SystemCommand,
            result.Kind);
        Assert.Equal(
            expectedTool,
            result.ManagementTool);
        Assert.True(
            result.IsSystemCommand);
        Assert.False(
            result.CanTogglePin);
        Assert.StartsWith(
            "system:",
            result.StableKey);
        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Glyph));
    }

    [Fact]
    public void Compose_EmptyQueryDoesNotMixSystemCommandsIntoPinnedApps()
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "设置",
                        "exe:c:\\settings.exe")
                },
                Array.Empty<WindowTaskItem>(),
                string.Empty);

        ShellSearchResult result =
            Assert.Single(results);
        Assert.Equal(
            ShellSearchResultKind.Application,
            result.Kind);
        Assert.Null(result.ManagementTool);
    }

    [Fact]
    public void Compose_ExactApplicationStaysAheadOfEqualSystemCommand()
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "设置",
                        "exe:c:\\settings.exe")
                },
                Array.Empty<WindowTaskItem>(),
                "设置");

        Assert.Equal(
            ShellSearchResultKind.Application,
            results[0].Kind);
        Assert.Contains(
            results,
            item =>
                item.ManagementTool
                == SystemManagementTool.Settings);
    }

    [Theory]
    [InlineData("运行", "RunDialog")]
    [InlineData("win a", "QuickSettings")]
    [InlineData("消息", "Notifications")]
    [InlineData("键盘", "InputSwitcher")]
    [InlineData("win tab", "TaskView")]
    [InlineData("天气", "Widgets")]
    [InlineData("查看桌面", "ShowDesktop")]
    public void Compose_FindsSafeShellActionByNameOrAlias(
        string query,
        string expectedAction)
    {
        ShellSearchResult result =
            ShellSearchPolicy.Compose(
                    Array.Empty<AppLaunchItem>(),
                    Array.Empty<WindowTaskItem>(),
                    query)
                .First(
                    item =>
                        item.ShellAction
                        .HasValue);

        Assert.Equal(
            ShellSearchResultKind.SystemCommand,
            result.Kind);
        Assert.Equal(
            expectedAction,
            result.ShellAction
                ?.ToString());
        Assert.StartsWith(
            "shell:",
            result.StableKey);
        Assert.Equal(
            "Windows 快捷命令",
            result.SecondaryText);
        Assert.Null(
            result.ManagementTool);
        Assert.False(
            result.CanTogglePin);
    }

    [Fact]
    public void Compose_CalculationIsFirstAndCanBeCopied()
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "计算器",
                        "exe:c:\\calc.exe")
                },
                Array.Empty<WindowTaskItem>(),
                "2 + 3 * 4");

        ShellSearchResult result =
            results[0];
        Assert.Equal(
            ShellSearchResultKind.Calculation,
            result.Kind);
        Assert.Equal(
            "14",
            result.DisplayName);
        Assert.Equal(
            "14",
            result.CalculationResult);
        Assert.Equal(
            "计算结果 · 点击或按 Enter 复制",
            result.SecondaryText);
        Assert.True(
            result.IsCalculation);
        Assert.True(
            result.UsesGlyph);
        Assert.False(
            result.CanTogglePin);
        Assert.StartsWith(
            "calculation:",
            result.StableKey);
    }

    [Theory]
    [InlineData(
        "音量 35",
        "SetVolume",
        35)]
    [InlineData(
        "volume +10",
        "AdjustVolume",
        10)]
    [InlineData(
        "静音",
        "SetMuted",
        0)]
    public void Compose_AudioCommandIsFirstAndExecutable(
        string query,
        string kind,
        int percent)
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "音量助手",
                        "exe:c:\\volume.exe")
                },
                Array.Empty<WindowTaskItem>(),
                query);

        ShellSearchResult result =
            results[0];
        Assert.Equal(
            ShellSearchResultKind.AudioCommand,
            result.Kind);
        Assert.Equal(
            kind,
            result.AudioCommand?.Kind
                .ToString());
        Assert.Equal(
            percent,
            result.AudioCommand?.Percent);
        Assert.True(result.IsAudioCommand);
        Assert.True(result.UsesGlyph);
        Assert.False(result.CanTogglePin);
        Assert.StartsWith(
            "audio:",
            result.StableKey);
        Assert.Contains(
            "Enter",
            result.SecondaryText);
    }

    [Theory]
    [InlineData("专注 25", 25)]
    [InlineData("focus 45 min", 45)]
    [InlineData("开始番茄钟60分钟", 60)]
    public void Compose_FocusCommandIsFirstAndExecutable(
        string query,
        int minutes)
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "专注工具",
                        "exe:c:\\focus.exe")
                },
                Array.Empty<WindowTaskItem>(),
                query);

        ShellSearchResult result =
            results[0];
        Assert.Equal(
            ShellSearchResultKind.FocusCommand,
            result.Kind);
        Assert.Equal(
            minutes,
            result.FocusCommand
                ?.DurationMinutes);
        Assert.True(result.IsFocusCommand);
        Assert.True(result.UsesGlyph);
        Assert.False(result.CanTogglePin);
        Assert.StartsWith(
            "focus:start:",
            result.StableKey);
        Assert.Contains(
            "开始",
            result.SecondaryText);
    }

    [Theory]
    [InlineData("任务 买牛奶", "买牛奶")]
    [InlineData("todo: write report", "write report")]
    [InlineData("task: prepare release", "prepare release")]
    public void Compose_TaskCaptureIsFirstAndExplicit(
        string query,
        string title)
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "任务工具",
                        "exe:c:\\tasks.exe")
                },
                Array.Empty<WindowTaskItem>(),
                query);

        ShellSearchResult result =
            results[0];
        Assert.Equal(
            ShellSearchResultKind.TaskCapture,
            result.Kind);
        Assert.Equal(
            title,
            result.TaskCaptureCommand?.Title);
        Assert.True(result.IsTaskCapture);
        Assert.True(result.UsesGlyph);
        Assert.False(result.CanTogglePin);
        Assert.StartsWith(
            "task:capture:",
            result.StableKey);
    }

    [Fact]
    public void Compose_TaskManagerAliasIsNotCaptured()
    {
        ShellSearchResult result =
            ShellSearchPolicy.Compose(
                    Array.Empty<AppLaunchItem>(),
                    Array.Empty<WindowTaskItem>(),
                    "task manager")
                .First();

        Assert.Equal(
            ShellSearchResultKind.SystemCommand,
            result.Kind);
        Assert.Null(result.TaskCaptureCommand);
        Assert.Equal(
            SystemManagementTool.TaskManager,
            result.ManagementTool);
    }

    [Fact]
    public void Compose_FindsExistingTaskAndExposesDirectCompletion()
    {
        var task =
            new TaskSearchItem(
                27,
                "整理 0.10.60 发布说明",
                1,
                "Inbox",
                "In Progress",
                DateTime.Now);

        ShellSearchResult result =
            Assert.Single(
                ShellSearchPolicy.Compose(
                        Array.Empty<AppLaunchItem>(),
                        Array.Empty<WindowTaskItem>(),
                        "发布说明",
                        taskItems:
                            new[]
                            {
                                task
                            })
                    .Where(item =>
                        item.IsTask));

        Assert.Equal(
            ShellSearchResultKind.Task,
            result.Kind);
        Assert.Same(
            task,
            result.TaskItem);
        Assert.Equal(
            "task:item:27",
            result.StableKey);
        Assert.Equal(
            "Inbox · In Progress",
            result.SecondaryText);
        Assert.True(result.UsesGlyph);
        Assert.True(
            result.CanCompleteTask);
        Assert.False(
            result.CanTogglePin);
    }

    [Fact]
    public void Compose_EmptyQueryDoesNotExposeTaskSnapshot()
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "固定应用",
                        "exe:c:\\app.exe")
                },
                Array.Empty<WindowTaskItem>(),
                string.Empty,
                taskItems:
                    new[]
                    {
                        new TaskSearchItem(
                            3,
                            "不应泄露的待办",
                            1,
                            "Inbox",
                            "To Do",
                            DateTime.Now)
                    });

        Assert.Single(results);
        Assert.DoesNotContain(
            results,
            item =>
                item.IsTask);
    }

    [Fact]
    public void Compose_ExactApplicationStaysAheadOfEqualTaskTitle()
    {
        var results =
            ShellSearchPolicy.Compose(
                new[]
                {
                    App(
                        "发布工具",
                        "exe:c:\\release.exe")
                },
                Array.Empty<WindowTaskItem>(),
                "发布工具",
                taskItems:
                    new[]
                    {
                        new TaskSearchItem(
                            5,
                            "发布工具",
                            1,
                            "Inbox",
                            "To Do",
                            DateTime.Now)
                    });

        Assert.Equal(
            ShellSearchResultKind.Application,
            results[0].Kind);
        Assert.Contains(
            results,
            item =>
                item.Kind
                == ShellSearchResultKind.Task);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("calc")]
    [InlineData("1/0")]
    public void Compose_DoesNotInventCalculationForInvalidInput(
        string query)
    {
        Assert.DoesNotContain(
            ShellSearchPolicy.Compose(
                Array.Empty<AppLaunchItem>(),
                Array.Empty<WindowTaskItem>(),
                query),
            result =>
                result.Kind
                == ShellSearchResultKind
                    .Calculation);
    }

    [Fact]
    public void Compose_NonPositiveLimitReturnsEmpty()
    {
        Assert.Empty(
            ShellSearchPolicy.Compose(
                new[] { App("应用", "exe:c:\\app.exe") },
                Array.Empty<WindowTaskItem>(),
                "应用",
                0));
    }

    private static AppLaunchItem App(
        string name,
        string identity) => new()
    {
        DisplayName = name,
        LaunchKind = AppLaunchKind.Executable,
        LaunchTarget = identity["exe:".Length..],
        IdentityKey = identity
    };

    private static WindowTaskItem Running(
        string appName,
        string title,
        int handle) => new()
    {
        DisplayName = appName,
        IdentityKey = "exe:c:\\running.exe",
        Windows =
            new[]
            {
                new WindowReference(
                    new IntPtr(handle),
                    title)
            }
    };
}
