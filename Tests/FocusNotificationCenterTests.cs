using System;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusNotificationCenterTests
{
    [Fact]
    public void Add_PrependsNotificationAndTracksUnreadCount()
    {
        var center = new FocusNotificationCenter();

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
        var center = new FocusNotificationCenter();
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
        var center = new FocusNotificationCenter();
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
    public void Invoke_MarksItemReadAndRunsItsAction()
    {
        var center = new FocusNotificationCenter();
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
        var center = new FocusNotificationCenter();

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
}
