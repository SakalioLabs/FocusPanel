using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    WindowPreviewActionPolicyTests
{
    [Theory]
    [InlineData(
        TrackedWindowState.Normal,
        WindowStateAction.Maximize)]
    [InlineData(
        TrackedWindowState.Minimized,
        WindowStateAction.Maximize)]
    [InlineData(
        TrackedWindowState.Maximized,
        WindowStateAction.Restore)]
    internal void ResizeButton_UsesExpectedAction(
        TrackedWindowState state,
        WindowStateAction expected)
    {
        Assert.Equal(
            expected,
            WindowPreviewActionPolicy
                .GetResizeAction(state));
    }
}
