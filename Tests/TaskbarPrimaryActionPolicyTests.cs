using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarPrimaryActionPolicyTests
{
    [Fact]
    public void VisibleWindow_ActivatesOrMinimizes()
    {
        Assert.Equal(
            TaskbarPrimaryAction.ActivateOrMinimize,
            TaskbarPrimaryActionPolicy.Get(
                Running(
                    new[]
                    {
                        new WindowReference(
                            new IntPtr(1),
                            "窗口")
                    })));
    }

    [Fact]
    public void BackgroundOnlyProcess_LaunchesReliableTarget()
    {
        TaskbarAppItem item = Running(
            Array.Empty<WindowReference>());

        Assert.True(item.IsBackgroundOnly);
        Assert.Equal(
            TaskbarPrimaryAction.Launch,
            TaskbarPrimaryActionPolicy.Get(item));
    }

    [Fact]
    public void ProtectedBackgroundProcessWithoutTarget_DoesNothing()
    {
        var item = new TaskbarAppItem
        {
            RunningTask = new WindowTaskItem
            {
                IdentityKey = "window:7",
                AppKey = "window:7",
                DisplayName = "受保护后台应用",
                Windows = Array.Empty<
                    WindowReference>()
            }
        };

        Assert.Equal(
            TaskbarPrimaryAction.None,
            TaskbarPrimaryActionPolicy.Get(item));
    }

    private static TaskbarAppItem Running(
        WindowReference[] windows) => new()
    {
        DisplayName = "同步助手",
        RunningTask = new WindowTaskItem
        {
            IdentityKey = @"exe:c:\apps\sync.exe",
            AppKey = @"exe:c:\apps\sync.exe",
            DisplayName = "同步助手",
            ExecutablePath = @"C:\Apps\Sync.exe",
            Windows = windows
        }
    };
}
