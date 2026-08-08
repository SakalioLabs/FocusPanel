using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusNotificationCenterTests
{
    [Fact]
    public void Add_PrependsNotificationAndTracksUnreadCount()
    {
        var center = CreateCenter();

        center.Add(Create("first", "第一条"));
        center.Add(Create("second", "第二条"));

        Assert.Equal(2, center.UnreadCount);
        Assert.Equal(
            new[] { "second", "first" },
            center.Items.Select(item => item.Key));
        Assert.All(center.Items, item => Assert.True(item.IsUnread));
    }

    [Fact]
    public void Add_ReplacesDuplicateKeyWithoutGrowingHistory()
    {
        var center = CreateCenter();
        center.Add(Create("update", "旧版本"));
        center.MarkAllRead();

        center.Add(Create("UPDATE", "新版本"));

        FocusNotificationItem item = Assert.Single(center.Items);
        Assert.Equal("新版本", item.Message);
        Assert.True(item.IsUnread);
        Assert.Equal(1, center.UnreadCount);
    }

    [Fact]
    public void MarkAllReadAndClear_KeepCountsConsistent()
    {
        var center = CreateCenter();
        center.Add(Create("first", "第一条"));
        center.Add(Create("second", "第二条"));

        center.MarkAllRead();

        Assert.Equal(0, center.UnreadCount);
        Assert.All(center.Items, item => Assert.False(item.IsUnread));

        center.Clear();

        Assert.Empty(center.Items);
        Assert.Equal(0, center.UnreadCount);
    }

    [Fact]
    public void MarkRead_ChangesOnlyTheRequestedNotification()
    {
        var center = CreateCenter();
        center.Add(Create("first", "第一条"));
        center.Add(Create("second", "第二条"));

        center.MarkRead(center.Items[1]);

        Assert.False(center.Items[1].IsUnread);
        Assert.True(center.Items[0].IsUnread);
        Assert.Equal(1, center.UnreadCount);
    }

    [Fact]
    public void Remove_DeletesOneItemAndKeepsUnreadCountConsistent()
    {
        var center = CreateCenter();
        center.Add(Create("first", "第一条"));
        center.Add(Create("second", "第二条"));
        FocusNotificationItem removed = center.Items[0];

        center.Remove(removed);

        Assert.Equal("first", Assert.Single(center.Items).Key);
        Assert.Equal(1, center.UnreadCount);
        center.Remove(removed);
        Assert.Equal(1, center.UnreadCount);
    }

    [Fact]
    public void Invoke_MarksItemReadAndRunsItsAction()
    {
        var center = CreateCenter();
        int invocations = 0;
        center.Add(
            Create(
                "action",
                "可操作",
                () => invocations++));

        center.Invoke(center.Items[0]);

        Assert.Equal(1, invocations);
        Assert.Equal(0, center.UnreadCount);
        Assert.False(center.Items[0].IsUnread);
    }

    [Fact]
    public void Add_TrimsOldestItemsAtBoundedCapacity()
    {
        var center = CreateCenter();

        for (int index = 0;
             index < FocusNotificationCenter.MaximumItems + 3;
             index++)
        {
            center.Add(Create($"item-{index}", $"消息 {index}"));
        }

        Assert.Equal(FocusNotificationCenter.MaximumItems, center.Items.Count);
        Assert.Equal(FocusNotificationCenter.MaximumItems, center.UnreadCount);
        Assert.Equal("item-52", center.Items[0].Key);
        Assert.Equal("item-3", center.Items[^1].Key);
    }

    [Fact]
    public void Constructor_RestoresUnreadHistoryWithoutStaleActions()
    {
        DateTimeOffset createdAt =
            new(2026, 8, 8, 8, 30, 0, TimeSpan.Zero);
        var store = new RecordingStore(
            new FocusNotificationSnapshot(
                "update",
                "发现更新",
                "0.11.51",
                "\uE7E7",
                FocusToastKind.Information,
                "查看更新",
                createdAt,
                true));

        var center = new FocusNotificationCenter(store);

        FocusNotificationItem item = Assert.Single(center.Items);
        Assert.Equal(createdAt, item.CreatedAt);
        Assert.Equal(1, center.UnreadCount);
        Assert.True(item.IsUnread);
        Assert.False(item.HasAction);
        Assert.True(item.IsExpiredAction);
    }

    [Fact]
    public void Constructor_RestoresAllowlistedPanelAction()
    {
        int invocations = 0;
        FocusNotificationSnapshot snapshot =
            Snapshot("task") with
            {
                ActionLabel = "查看任务",
                ActionKind =
                    FocusNotificationActionKind.OpenTasks
            };
        var center = new FocusNotificationCenter(
            new RecordingStore(snapshot),
            actionKind =>
                actionKind == FocusNotificationActionKind.OpenTasks
                    ? () => invocations++
                    : null);

        FocusNotificationItem item = Assert.Single(center.Items);
        Assert.True(item.HasAction);
        Assert.False(item.IsExpiredAction);
        center.Invoke(item);

        Assert.Equal(1, invocations);
        Assert.False(item.IsUnread);
    }

    [Fact]
    public async Task Add_UsesResolverAndPersistsSemanticActionKind()
    {
        int invocations = 0;
        var store = new RecordingStore();
        var center = new FocusNotificationCenter(
            store,
            actionKind =>
                actionKind
                    == FocusNotificationActionKind.OpenPomodoro
                    ? () => invocations++
                    : null);
        var notification = new FocusToastNotification(
            "pomodoro",
            "专注完成",
            "休息一下",
            "\uE823",
            FocusToastKind.Success,
            "查看专注",
            Action: null,
            ActionKind:
                FocusNotificationActionKind.OpenPomodoro);

        center.Add(notification);
        center.Invoke(center.Items[0]);
        await center.CompleteAsync();

        Assert.Equal(1, invocations);
        Assert.Equal(
            FocusNotificationActionKind.OpenPomodoro,
            store.Saves.Last()[0].ActionKind);
    }

    [Fact]
    public void Constructor_UnknownActionKindCannotBecomeExecutable()
    {
        FocusNotificationSnapshot snapshot =
            Snapshot("unknown") with
            {
                ActionLabel = "危险动作",
                ActionKind =
                    (FocusNotificationActionKind)999
            };
        int resolverCalls = 0;
        var center = new FocusNotificationCenter(
            new RecordingStore(snapshot),
            _ =>
            {
                resolverCalls++;
                return () => { };
            });

        FocusNotificationItem item = Assert.Single(center.Items);
        Assert.Equal(
            FocusNotificationActionKind.None,
            item.ActionKind);
        Assert.False(item.HasAction);
        Assert.True(item.IsExpiredAction);
        Assert.Equal(0, resolverCalls);
    }

    [Fact]
    public void Constructor_NormalizesAndBoundsUntrustedHistory()
    {
        FocusNotificationSnapshot[] snapshots =
            Enumerable.Range(0, FocusNotificationCenter.MaximumItems + 5)
                .Select(index =>
                    new FocusNotificationSnapshot(
                        index == 1 ? "item-0" : $"item-{index}",
                        index == 2 ? string.Empty : $"标题 {index}",
                        "消息",
                        "\uE7E7",
                        (FocusToastKind)999,
                        null,
                        DateTimeOffset.Now,
                        true))
                .ToArray();
        var center = new FocusNotificationCenter(
            new RecordingStore(snapshots));

        Assert.True(
            center.Items.Count
            <= FocusNotificationCenter.MaximumItems);
        Assert.Equal(
            center.Items.Count,
            center.Items.Select(item => item.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.DoesNotContain(
            center.Items,
            item => string.IsNullOrWhiteSpace(item.Title));
        Assert.All(
            center.Items,
            item => Assert.Equal(
                FocusToastKind.Information,
                item.Kind));
    }

    [Fact]
    public async Task CompleteAsync_DrainsLatestCoalescedSnapshot()
    {
        var store = new RecordingStore();
        var center = new FocusNotificationCenter(store);

        center.Add(Create("first", "第一条"));
        center.Add(Create("second", "第二条"));
        center.MarkAllRead();
        await center.CompleteAsync();

        IReadOnlyList<FocusNotificationSnapshot> saved =
            Assert.IsAssignableFrom<
                IReadOnlyList<FocusNotificationSnapshot>>(
                store.Saves.Last());
        Assert.Equal(
            new[] { "second", "first" },
            saved.Select(item => item.Key));
        Assert.All(saved, item => Assert.False(item.IsUnread));
    }

    [Fact]
    public async Task FlushAsync_DrainsCurrentWorkAndKeepsAcceptingSaves()
    {
        var store = new RecordingStore();
        var center = new FocusNotificationCenter(store);
        center.Add(Create("before-update", "更新前"));

        await center.FlushAsync();
        center.Add(Create("install-failed", "安装启动失败"));
        await center.CompleteAsync();

        Assert.Contains(
            store.Saves,
            saved => saved.Any(item => item.Key == "before-update"));
        Assert.Contains(
            store.Saves.Last(),
            item => item.Key == "install-failed");
    }

    [Fact]
    public async Task SaveFailure_IsReportedWithoutEscapingMutation()
    {
        var store = new RecordingStore
        {
            SaveException = new IOException("磁盘已满")
        };
        var center = new FocusNotificationCenter(store);
        int statusChanges = 0;
        center.PersistenceStatusChanged +=
            (_, _) => statusChanges++;

        center.Add(Create("warning", "仍保留在内存"));
        await center.CompleteAsync();

        Assert.Single(center.Items);
        Assert.Contains("磁盘已满", center.LastPersistenceError);
        Assert.Equal(1, statusChanges);
    }

    [Fact]
    public void JsonStore_CorruptionIsArchivedAndReturnsWarning()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FocusPanel-notification-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "history.json");
            File.WriteAllText(path, "{not-json");
            var store = new JsonFocusNotificationStore(path);

            FocusNotificationLoadResult result = store.Load();

            Assert.Empty(result.Items);
            Assert.False(string.IsNullOrWhiteSpace(result.Warning));
            Assert.False(File.Exists(path));
            Assert.Single(
                Directory.GetFiles(
                    directory,
                    "panel-notifications.corrupt-*.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void JsonStore_SaveAndLoadRoundTripsSnapshot()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FocusPanel-notification-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "history.json");
            var store = new JsonFocusNotificationStore(path);
            FocusNotificationSnapshot expected = Snapshot("roundtrip");

            store.Save(new[] { expected });
            FocusNotificationLoadResult loaded = store.Load();

            Assert.Equal(expected, Assert.Single(loaded.Items));
            Assert.Empty(
                Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FocusToastNotification Create(
        string key,
        string message,
        Action? action = null) =>
        new(
            key,
            "FocusPanel",
            message,
            "\uE7E7",
            ActionLabel: action == null ? null : "执行",
            Action: action);

    private static FocusNotificationCenter CreateCenter() =>
        new(new RecordingStore());

    private static FocusNotificationSnapshot Snapshot(
        string key) =>
        new(
            key,
            "FocusPanel",
            "消息",
            "\uE7E7",
            FocusToastKind.Information,
            null,
            new DateTimeOffset(
                2026,
                8,
                8,
                8,
                30,
                0,
                TimeSpan.Zero),
            true);

    private sealed class RecordingStore
        : IFocusNotificationStore
    {
        private readonly IReadOnlyList<FocusNotificationSnapshot>
            _loaded;

        internal RecordingStore(
            params FocusNotificationSnapshot[] loaded)
        {
            _loaded = loaded;
        }

        internal List<IReadOnlyList<FocusNotificationSnapshot>>
            Saves { get; } = new();

        internal Exception? SaveException { get; set; }

        public FocusNotificationLoadResult Load() =>
            new(_loaded);

        public void Save(
            IReadOnlyList<FocusNotificationSnapshot> items)
        {
            if (SaveException != null)
                throw SaveException;

            lock (Saves)
            {
                Saves.Add(items.ToArray());
            }
        }
    }
}
