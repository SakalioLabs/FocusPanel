using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowTrackingActivityPolicyTests
{
    [Fact]
    public void HiddenShellIgnoresWindowEvents()
    {
        Assert.False(
            WindowTrackingActivityPolicy.ShouldProcessWindowEvent(
                isTrackingActive: false));
        Assert.True(
            WindowTrackingActivityPolicy.ShouldProcessWindowEvent(
                isTrackingActive: true));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void SnapshotRefreshesOnlyWhenTrackingResumes(
        bool wasActive,
        bool isActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            WindowTrackingActivityPolicy.ShouldRefreshAfterActivityChange(
                wasActive,
                isActive));
    }
}
