using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowStateActionPolicyTests
{
    [Fact]
    public void NormalWindow_CanMinimizeOrMaximize()
    {
        Assert.Equal(
            new[]
            {
                WindowStateAction.Minimize,
                WindowStateAction.Maximize
            },
            WindowStateActionPolicy.GetActions(
                TrackedWindowState.Normal));
    }

    [Fact]
    public void MinimizedWindow_CanRestoreOrMaximize()
    {
        Assert.Equal(
            new[]
            {
                WindowStateAction.Restore,
                WindowStateAction.Maximize
            },
            WindowStateActionPolicy.GetActions(
                TrackedWindowState.Minimized));
    }

    [Fact]
    public void MaximizedWindow_CanRestoreOrMinimize()
    {
        Assert.Equal(
            new[]
            {
                WindowStateAction.Restore,
                WindowStateAction.Minimize
            },
            WindowStateActionPolicy.GetActions(
                TrackedWindowState.Maximized));
    }
}
