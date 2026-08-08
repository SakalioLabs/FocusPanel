using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarAppComposerTests
{
    [Fact]
    public void Compose_MergesPinnedAndRunningWithSameIdentity()
    {
        var composer = new TaskbarAppComposer();

        IReadOnlyList<TaskbarAppItem> result = composer.Compose(
            new[] { Pinned("Editor", "exe:c:\\apps\\editor.exe") },
            new[] { Running("editor", "exe:c:\\apps\\editor.exe", 1) });

        TaskbarAppItem item = Assert.Single(result);
        Assert.True(item.IsPinned);
        Assert.True(item.IsRunning);
        Assert.Equal("Editor", item.DisplayName);
    }

    [Fact]
    public void Compose_DoesNotMergeSameDisplayNameWithDifferentIdentity()
    {
        var composer = new TaskbarAppComposer();

        IReadOnlyList<TaskbarAppItem> result = composer.Compose(
            new[] { Pinned("工具", "exe:c:\\one\\tool.exe") },
            new[] { Running("工具", "exe:c:\\two\\tool.exe", 2) });

        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].IdentityKey, result[1].IdentityKey);
    }

    [Fact]
    public void Compose_KeepsPinnedOrderAndStableFirstSeenRunningOrder()
    {
        var composer = new TaskbarAppComposer();
        AppLaunchItem firstPin = Pinned("固定一", "exe:c:\\pin1.exe");
        AppLaunchItem secondPin = Pinned("固定二", "exe:c:\\pin2.exe");
        WindowTaskItem firstRun = Running("运行一", "exe:c:\\run1.exe", 1);
        WindowTaskItem secondRun = Running("运行二", "exe:c:\\run2.exe", 2);

        composer.Compose(
            new[] { firstPin, secondPin },
            new[] { firstRun, secondRun });
        IReadOnlyList<TaskbarAppItem> reorderedSnapshot = composer.Compose(
            new[] { firstPin, secondPin },
            new[] { Active(secondRun), firstRun });

        Assert.Equal(
            new[] { "固定一", "固定二", "运行一", "运行二" },
            reorderedSnapshot.Select(item => item.DisplayName));
    }

    [Fact]
    public void Compose_RemovesClosedRuntimeAndReaddsItAtTheEnd()
    {
        var composer = new TaskbarAppComposer();
        WindowTaskItem first = Running("一", "exe:c:\\one.exe", 1);
        WindowTaskItem second = Running("二", "exe:c:\\two.exe", 2);

        composer.Compose(Array.Empty<AppLaunchItem>(), new[] { first, second });
        composer.Compose(Array.Empty<AppLaunchItem>(), new[] { second });
        IReadOnlyList<TaskbarAppItem> reopened = composer.Compose(
            Array.Empty<AppLaunchItem>(),
            new[] { first, second });

        Assert.Equal(new[] { "二", "一" }, reopened.Select(item => item.DisplayName));
    }

    [Fact]
    public void RunningOnlyItem_CreatesPersistableExecutableLaunch()
    {
        TaskbarAppItem item = new TaskbarAppComposer().Compose(
            Array.Empty<AppLaunchItem>(),
            new[] { Running("Editor", "exe:c:\\editor.exe", 1, @"C:\Editor.exe") })
            .Single();

        AppLaunchItem launch = Assert.IsType<AppLaunchItem>(item.CreateLaunchItem());
        Assert.Equal(AppLaunchKind.Executable, launch.LaunchKind);
        Assert.Equal(@"C:\Editor.exe", launch.LaunchTarget);
        Assert.True(item.CanPin);
    }

    [Fact]
    public void BackgroundOnlyItem_RemainsLaunchableAndPinnable()
    {
        var composer = new TaskbarAppComposer();
        var background = new WindowTaskItem
        {
            AppKey = "exe:c:\\apps\\sync.exe",
            IdentityKey = "exe:c:\\apps\\sync.exe",
            DisplayName = "Sync",
            ExecutablePath = @"C:\Apps\Sync.exe",
            Windows = Array.Empty<WindowReference>()
        };

        TaskbarAppItem item = Assert.Single(
            composer.Compose(
                Array.Empty<AppLaunchItem>(),
                new[] { background }));

        Assert.True(item.IsRunning);
        Assert.True(item.IsBackgroundOnly);
        Assert.True(item.CanPin);
        AppLaunchItem launch = Assert.IsType<AppLaunchItem>(
            item.CreateLaunchItem());
        Assert.Equal(@"C:\Apps\Sync.exe", launch.LaunchTarget);
    }

    [Fact]
    public void UnpinnedRunningItem_RemainsUntilItsLastWindowCloses()
    {
        var composer = new TaskbarAppComposer();
        AppLaunchItem pinned = Pinned("Editor", "exe:c:\\editor.exe");
        WindowTaskItem running = Running(
            "Editor",
            "exe:c:\\editor.exe",
            1,
            @"C:\Editor.exe");

        Assert.Single(composer.Compose(new[] { pinned }, new[] { running }));

        TaskbarAppItem afterUnpin = Assert.Single(
            composer.Compose(Array.Empty<AppLaunchItem>(), new[] { running }));
        Assert.False(afterUnpin.IsPinned);
        Assert.True(afterUnpin.IsRunning);

        Assert.Empty(composer.Compose(
            Array.Empty<AppLaunchItem>(),
            Array.Empty<WindowTaskItem>()));
    }

    [Fact]
    public void PinnedItem_RemainsAfterItsLastWindowCloses()
    {
        var composer = new TaskbarAppComposer();
        AppLaunchItem pinned = Pinned("Editor", "exe:c:\\editor.exe");
        WindowTaskItem running = Running("Editor", "exe:c:\\editor.exe", 1);

        composer.Compose(new[] { pinned }, new[] { running });
        TaskbarAppItem stopped = Assert.Single(
            composer.Compose(new[] { pinned }, Array.Empty<WindowTaskItem>()));

        Assert.True(stopped.IsPinned);
        Assert.False(stopped.IsRunning);
    }

    private static AppLaunchItem Pinned(string name, string identity) => new()
    {
        DisplayName = name,
        LaunchKind = AppLaunchKind.Executable,
        LaunchTarget = name + ".exe",
        IdentityKey = identity,
        IsPinned = true
    };

    private static WindowTaskItem Running(
        string name,
        string identity,
        int handle,
        string? executable = null) => new()
    {
        AppKey = identity,
        IdentityKey = identity,
        DisplayName = name,
        ExecutablePath = executable,
        Windows = new[] { new WindowReference(new IntPtr(handle), name) }
    };

    private static WindowTaskItem Active(WindowTaskItem item) => new()
    {
        AppKey = item.AppKey,
        IdentityKey = item.IdentityKey,
        DisplayName = item.DisplayName,
        ExecutablePath = item.ExecutablePath,
        Windows = item.Windows,
        IsActive = true
    };
}
