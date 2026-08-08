using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarAppCollectionSynchronizerTests
{
    [Fact]
    public void UnchangedSnapshot_PreservesInstancesAndRaisesNoCollectionChanges()
    {
        TaskbarAppItem first = App("exe:c:\\one.exe", "一");
        TaskbarAppItem second = App("exe:c:\\two.exe", "二");
        var items = new ObservableCollection<TaskbarAppItem> { first, second };
        int changes = 0;
        items.CollectionChanged += (_, _) => changes++;

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                App("exe:c:\\one.exe", "一"),
                App("exe:c:\\two.exe", "二")
            });

        Assert.Same(first, items[0]);
        Assert.Same(second, items[1]);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ActiveStateChange_UpdatesApplicationWithoutReplacingButton()
    {
        TaskbarAppItem first = RunningApp("exe:c:\\one.exe", "一", 1, false);
        TaskbarAppItem second = RunningApp("exe:c:\\two.exe", "二", 2, false);
        var items = new ObservableCollection<TaskbarAppItem> { first, second };
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, args) => actions.Add(args.Action);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                RunningApp("exe:c:\\one.exe", "一", 1, true),
                RunningApp("exe:c:\\two.exe", "二", 2, false)
            });

        Assert.Same(first, items[0]);
        Assert.Same(second, items[1]);
        Assert.True(items[0].IsActive);
        Assert.Empty(actions);
    }

    [Fact]
    public void Reorder_UsesMoveWithoutResettingApplicationList()
    {
        TaskbarAppItem first = App("exe:c:\\one.exe", "一");
        TaskbarAppItem second = App("exe:c:\\two.exe", "二");
        var items = new ObservableCollection<TaskbarAppItem> { first, second };
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, args) => actions.Add(args.Action);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                App("exe:c:\\two.exe", "二"),
                App("exe:c:\\one.exe", "一")
            });

        Assert.Same(second, items[0]);
        Assert.Same(first, items[1]);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Move }, actions);
    }

    [Fact]
    public void ActiveWindowChange_UpdatesExistingApplicationInPlace()
    {
        TaskbarAppItem first =
            MultiWindowApp(
                "exe:c:\\editor.exe",
                firstWindowActive: true);
        TaskbarAppItem second =
            RunningApp(
                "exe:c:\\other.exe",
                "其他",
                3,
                false);
        var items =
            new ObservableCollection<TaskbarAppItem>
            {
                first,
                second
            };
        var actions =
            new List<NotifyCollectionChangedAction>();
        items.CollectionChanged +=
            (_, args) => actions.Add(args.Action);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                MultiWindowApp(
                    "exe:c:\\editor.exe",
                    firstWindowActive: false),
                RunningApp(
                    "exe:c:\\other.exe",
                    "其他",
                    3,
                    false)
            });

        Assert.Same(first, items[0]);
        Assert.Same(second, items[1]);
        Assert.False(
            items[0].Windows[0].IsActive);
        Assert.True(
            items[0].Windows[1].IsActive);
        Assert.Empty(actions);
    }

    [Fact]
    public void StatusCenterWindowList_PersistsUntilAppStopsBeingMultiWindow()
    {
        TaskbarAppItem current =
            MultiWindowApp(
                "exe:c:\\editor.exe",
                firstWindowActive: true);
        current.IsStatusCenterWindowListExpanded =
            true;
        var items =
            new ObservableCollection<TaskbarAppItem>
            {
                current
            };

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                MultiWindowApp(
                    "exe:c:\\editor.exe",
                    firstWindowActive: false)
            });

        Assert.Same(current, items[0]);
        Assert.True(
            current.IsStatusCenterWindowListExpanded);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                RunningApp(
                    "exe:c:\\editor.exe",
                    "编辑器",
                    9,
                    true)
            });

        Assert.False(
            current.IsStatusCenterWindowListExpanded);
    }

    [Fact]
    public void ActiveStateChange_RaisesPresentationNotifications()
    {
        TaskbarAppItem current =
            RunningApp(
                "exe:c:\\one.exe",
                "一",
                1,
                false);
        var items =
            new ObservableCollection<TaskbarAppItem>
            {
                current
            };
        var changed =
            new List<string?>();
        current.PropertyChanged +=
            (_, args) => changed.Add(
                args.PropertyName);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                RunningApp(
                    "exe:c:\\one.exe",
                    "一",
                    1,
                    true)
            });

        Assert.Same(current, items[0]);
        Assert.True(current.IsActive);
        Assert.Contains(
            nameof(TaskbarAppItem.IsActive),
            changed);
        Assert.Contains(
            nameof(TaskbarAppItem.StatusSummary),
            changed);
        Assert.Contains(
            nameof(TaskbarAppItem.AccessibleName),
            changed);
        Assert.Contains(
            nameof(TaskbarAppItem.HasMultipleWindows),
            changed);
        Assert.Contains(
            nameof(TaskbarAppItem.WindowCountBadgeText),
            changed);
        Assert.Contains(
            nameof(TaskbarAppItem.WindowPreviewText),
            changed);
    }

    [Fact]
    public void WindowStateChange_UpdatesExistingApplicationInPlace()
    {
        TaskbarAppItem current =
            RunningApp(
                "exe:c:\\one.exe",
                "一",
                1,
                false,
                TrackedWindowState.Normal);
        var items =
            new ObservableCollection<
                TaskbarAppItem>
            {
                current
            };
        var changed =
            new List<string?>();
        current.PropertyChanged +=
            (_, args) => changed.Add(
                args.PropertyName);

        TaskbarAppCollectionSynchronizer
            .Synchronize(
                items,
                new[]
                {
                    RunningApp(
                        "exe:c:\\one.exe",
                        "一",
                        1,
                        false,
                        TrackedWindowState
                            .Minimized)
                });

        Assert.Same(current, items[0]);
        Assert.Equal(
            TrackedWindowState.Minimized,
            items[0].Windows[0].State);
        Assert.True(
            items[0].IsFullyMinimized);
        Assert.Contains(
            nameof(
                TaskbarAppItem
                    .IsFullyMinimized),
            changed);
        Assert.Contains(
            nameof(
                TaskbarAppItem
                    .StatusSummary),
            changed);
    }

    [Fact]
    public void TopmostStateChange_UpdatesExistingApplicationInPlace()
    {
        TaskbarAppItem current =
            RunningApp(
                "exe:c:\\one.exe",
                "一",
                1,
                false,
                isTopmost: false);
        var items =
            new ObservableCollection<
                TaskbarAppItem>
            {
                current
            };

        TaskbarAppCollectionSynchronizer
            .Synchronize(
                items,
                new[]
                {
                    RunningApp(
                        "exe:c:\\one.exe",
                        "一",
                        1,
                        false,
                        isTopmost: true)
                });

        Assert.Same(current, items[0]);
        Assert.True(
            items[0].Windows[0].IsTopmost);
    }

    [Fact]
    public void DifferentIdentity_IsStillInsertedInsteadOfMerged()
    {
        TaskbarAppItem current =
            App("exe:c:\\one.exe", "一");
        var items =
            new ObservableCollection<TaskbarAppItem>
            {
                current
            };

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[]
            {
                App("exe:c:\\two.exe", "二")
            });

        Assert.NotSame(current, items[0]);
        Assert.Equal(
            "exe:c:\\two.exe",
            items[0].IdentityKey);
    }

    [Fact]
    public void AttentionChange_RefreshesExistingApplicationInstance()
    {
        TaskbarAppItem current = RunningApp(
            "exe:c:\\chat.exe",
            "聊天",
            17,
            false);
        var items = new ObservableCollection<TaskbarAppItem>
        {
            current
        };
        TaskbarAppItem desired = RunningApp(
            "exe:c:\\chat.exe",
            "聊天",
            17,
            false,
            attentionRequested: true);

        TaskbarAppCollectionSynchronizer.Synchronize(
            items,
            new[] { desired });

        Assert.Same(current, items[0]);
        Assert.True(items[0].IsAttentionRequested);
    }

    private static TaskbarAppItem App(string identity, string name) => new()
    {
        IdentityKey = identity,
        DisplayName = name
    };

    private static TaskbarAppItem RunningApp(
        string identity,
        string name,
        int handle,
        bool active,
        TrackedWindowState state =
            TrackedWindowState.Normal,
        bool isTopmost = false,
        bool attentionRequested = false) => new()
    {
        IdentityKey = identity,
        DisplayName = name,
        RunningTask = new WindowTaskItem
        {
            AppKey = identity,
            IdentityKey = identity,
            DisplayName = name,
            IsActive = active,
            Windows = new[]
            {
                new WindowReference(
                    new IntPtr(handle),
                    name,
                    false,
                    state,
                    isTopmost,
                    attentionRequested)
            }
        }
    };

    private static TaskbarAppItem MultiWindowApp(
        string identity,
        bool firstWindowActive) =>
        new()
        {
            IdentityKey = identity,
            DisplayName = "编辑器",
            RunningTask = new WindowTaskItem
            {
                AppKey = identity,
                IdentityKey = identity,
                DisplayName = "编辑器",
                IsActive = true,
                Windows = new[]
                {
                    new WindowReference(
                        new IntPtr(1),
                        "文档一",
                        firstWindowActive),
                    new WindowReference(
                        new IntPtr(2),
                        "文档二",
                        !firstWindowActive)
                }
            }
        };
}
