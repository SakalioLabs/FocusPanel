using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal static class PanelNotificationFilterPolicy
{
    internal static IReadOnlyList<FocusNotificationItem> Apply(
        IEnumerable<FocusNotificationItem> notifications,
        bool unreadOnly)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        return unreadOnly
            ? notifications
                .Where(item => item.IsUnread)
                .ToArray()
            : notifications.ToArray();
    }
}
