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
