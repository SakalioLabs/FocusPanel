using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusToastQueueTests
{
    [Fact]
    public void Enqueue_FirstNotificationBecomesCurrent()
    {
        var queue = new FocusToastQueue();
        FocusToastNotification notification =
            Create("update", "v0.9.82");

        bool shouldPresent = queue.Enqueue(notification);

        Assert.True(shouldPresent);
        Assert.Same(notification, queue.Current);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void Enqueue_QueuesDifferentKeysInStableOrder()
    {
        var queue = new FocusToastQueue();
        FocusToastNotification first =
            Create("update", "更新");
        FocusToastNotification second =
            Create("pomodoro", "专注");
        FocusToastNotification third =
            Create("warning", "警告");

        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(third);

        Assert.Same(second, queue.CompleteCurrent());
        Assert.Same(third, queue.CompleteCurrent());
        Assert.Null(queue.CompleteCurrent());
    }

    [Fact]
    public void Enqueue_DuplicateCurrentKeyDoesNotRepeat()
    {
        var queue = new FocusToastQueue();
        queue.Enqueue(Create("update", "旧版本"));

        bool shouldPresent =
            queue.Enqueue(Create("UPDATE", "新版本"));

        Assert.False(shouldPresent);
        Assert.Equal("旧版本", queue.Current?.Message);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void Enqueue_ReplacesPendingDuplicateWithoutChangingOrder()
    {
        var queue = new FocusToastQueue();
        queue.Enqueue(Create("current", "当前"));
        queue.Enqueue(Create("update", "旧更新"));
        queue.Enqueue(Create("pomodoro", "专注"));

        queue.Enqueue(Create("UPDATE", "新更新"));

        Assert.Equal(2, queue.PendingCount);
        Assert.Equal(
            "新更新",
            queue.CompleteCurrent()?.Message);
        Assert.Equal(
            "专注",
            queue.CompleteCurrent()?.Message);
    }

    [Fact]
    public void Clear_RemovesCurrentAndPendingNotifications()
    {
        var queue = new FocusToastQueue();
        queue.Enqueue(Create("current", "当前"));
        queue.Enqueue(Create("pending", "等待"));

        queue.Clear();

        Assert.Null(queue.Current);
        Assert.Equal(0, queue.PendingCount);
    }

    private static FocusToastNotification Create(
        string key,
        string message) =>
        new(
            key,
            "FocusPanel",
            message,
            "\uE946");
}
