using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowFocusShortcutTargetPolicyTests
{
    [Fact]
    public void Select_PrefersCurrentlyActiveApplication()
    {
        TaskbarAppItem inactive = App(
            "inactive",
            1,
            isActive: false);
        TaskbarAppItem active = App(
            "active",
            2,
            isActive: true);

        TaskbarAppItem? selected =
            WindowFocusShortcutTargetPolicy.Select(
                new[] { inactive, active },
                new IntPtr(1));

        Assert.Same(active, selected);
    }

    [Fact]
    public void Select_FallsBackToLastExternalWindowWhenPanelIsActive()
    {
        TaskbarAppItem first = App(
            "first",
            1,
            isActive: false);
        TaskbarAppItem last = App(
            "last",
            2,
            isActive: false);

        TaskbarAppItem? selected =
            WindowFocusShortcutTargetPolicy.Select(
                new[] { first, last },
                new IntPtr(2));

        Assert.Same(last, selected);
    }

    [Fact]
    public void Select_IgnoresPinnedAndBackgroundOnlyApplications()
    {
        var pinned = new TaskbarAppItem
        {
            IdentityKey = "pinned",
            DisplayName = "固定但未运行"
        };
        var background = new TaskbarAppItem
        {
            IdentityKey = "background",
            DisplayName = "纯后台",
            RunningTask = new WindowTaskItem
            {
                IdentityKey = "background",
                DisplayName = "纯后台"
            }
        };

        TaskbarAppItem? selected =
            WindowFocusShortcutTargetPolicy.Select(
                new[] { pinned, background },
                IntPtr.Zero);

        Assert.Null(selected);
    }

    private static TaskbarAppItem App(
        string identity,
        int handle,
        bool isActive) =>
        new()
        {
            IdentityKey = identity,
            DisplayName = identity,
            RunningTask = new WindowTaskItem
            {
                IdentityKey = identity,
                DisplayName = identity,
                IsActive = isActive,
                Windows = new[]
                {
                    new WindowReference(
                        new IntPtr(handle),
                        identity,
                        isActive)
                }
            }
        };
}
