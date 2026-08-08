using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class BackgroundAppSnapshotComposerTests
{
    [Fact]
    public void HiddenOwner_BecomesBackgroundOnlyTask()
    {
        WindowTaskItem item = Assert.Single(
            BackgroundAppSnapshotComposer.Append(
                Array.Empty<WindowTaskItem>(),
                new[]
                {
                    Background(
                        "同步助手",
                        @"exe:c:\apps\sync.exe",
                        @"C:\Apps\Sync.exe")
                }));

        Assert.Empty(item.Windows);
        Assert.Equal("同步助手", item.DisplayName);
        Assert.Equal(
            @"C:\Apps\Sync.exe",
            item.ExecutablePath);
    }

    [Fact]
    public void VisibleIdentity_DoesNotCreateDuplicateBackgroundItem()
    {
        WindowTaskItem visible = Visible(
            "聊天",
            @"aumid:chat.app",
            @"C:\Apps\Chat.exe");

        var result = BackgroundAppSnapshotComposer.Append(
            new[] { visible },
            new[]
            {
                Background(
                    "聊天",
                    @"AUMID:CHAT.APP",
                    @"C:\Apps\Chat.exe")
            });

        Assert.Single(result);
        Assert.Same(visible, result[0]);
    }

    [Fact]
    public void SameExecutable_DoesNotDuplicateWhenWindowIdentityDiffers()
    {
        WindowTaskItem visible = Visible(
            "聊天",
            @"aumid:chat.app",
            @"C:\Apps\Chat.exe");

        var result = BackgroundAppSnapshotComposer.Append(
            new[] { visible },
            new[]
            {
                Background(
                    "聊天辅助宿主",
                    @"exe:c:\apps\chat.exe",
                    @"c:\apps\CHAT.exe")
            });

        Assert.Single(result);
    }

    [Fact]
    public void MultipleProcessesWithSameIdentity_MergeOnce()
    {
        var result = BackgroundAppSnapshotComposer.Append(
            Array.Empty<WindowTaskItem>(),
            new[]
            {
                Background(
                    "同步助手",
                    @"exe:c:\apps\sync.exe",
                    @"C:\Apps\Sync.exe",
                    processId: 8),
                Background(
                    "同步助手辅助进程",
                    @"EXE:C:\APPS\SYNC.EXE",
                    @"C:\Apps\Sync.exe",
                    processId: 9)
            });

        Assert.Single(result);
        Assert.Equal("同步助手", result[0].DisplayName);
    }

    [Fact]
    public void BackgroundItems_AreAppendedInStableDisplayOrder()
    {
        WindowTaskItem visible = Visible(
            "当前应用",
            "exe:visible",
            @"C:\Apps\Visible.exe");

        var result = BackgroundAppSnapshotComposer.Append(
            new[] { visible },
            new[]
            {
                Background("Zeta", "exe:z", @"C:\Apps\Z.exe"),
                Background("Alpha", "exe:a", @"C:\Apps\A.exe")
            });

        Assert.Equal(
            new[] { "当前应用", "Alpha", "Zeta" },
            new[]
            {
                result[0].DisplayName,
                result[1].DisplayName,
                result[2].DisplayName
            });
    }

    private static BackgroundAppObservation Background(
        string name,
        string identity,
        string path,
        uint processId = 7) => new(
            processId,
            name,
            path,
            identity,
            null,
            null);

    private static WindowTaskItem Visible(
        string name,
        string identity,
        string path) => new()
    {
        DisplayName = name,
        IdentityKey = identity,
        AppKey = identity,
        ExecutablePath = path,
        Windows = new[]
        {
            new WindowReference(
                new IntPtr(1),
                name)
        }
    };
}
