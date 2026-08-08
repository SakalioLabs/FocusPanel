using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class WindowFocusShortcutTargetPolicy
{
    internal static TaskbarAppItem? Select(
        IEnumerable<TaskbarAppItem>? applications,
        IntPtr lastActiveWindowHandle)
    {
        TaskbarAppItem[] candidates =
            applications?
                .Where(application =>
                    application != null
                    && application.Windows.Count > 0)
                .ToArray()
            ?? Array.Empty<TaskbarAppItem>();
        return candidates.FirstOrDefault(
                   application =>
                       application.IsActive)
               ?? candidates.FirstOrDefault(
                   application =>
                       lastActiveWindowHandle
                           != IntPtr.Zero
                       && application.Windows.Any(
                           window =>
                               window.Handle
                               == lastActiveWindowHandle));
    }
}
