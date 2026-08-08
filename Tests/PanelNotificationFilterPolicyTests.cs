using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelNotificationFilterPolicyTests
{
    [Fact]
    public void Apply_UnreadOnlyReturnsUnreadItemsInOriginalOrder()
    {
        var center = new FocusNotificationCenter(
            new TransientFocusNotificationStore());
        center.Add(Create("old"));
        center.Add(Create("new"));
        center.MarkRead(center.Items[1]);

        var filtered = PanelNotificationFilterPolicy.Apply(
            center.Items,
            unreadOnly: true);

        Assert.Equal(
            new[] { "new" },
            filtered.Select(item => item.Key));
    }

    [Fact]
    public void Apply_AllReturnsSnapshotWithoutChangingReadState()
    {
        var center = new FocusNotificationCenter(
            new TransientFocusNotificationStore());
        center.Add(Create("first"));
        center.Add(Create("second"));

        var filtered = PanelNotificationFilterPolicy.Apply(
            center.Items,
            unreadOnly: false);

        Assert.Equal(2, filtered.Count);
        Assert.Equal(2, center.UnreadCount);
    }

    private static FocusToastNotification Create(string key) =>
        new(
            key,
            "FocusPanel",
            key,
            "\uE7E7");
}
