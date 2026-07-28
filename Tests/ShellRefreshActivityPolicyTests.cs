using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellRefreshActivityPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void RefreshesSnapshotOnlyWhenShellBecomesVisible(
        bool wasVisible,
        bool isVisible,
        bool expected)
    {
        Assert.Equal(
            expected,
            ShellRefreshActivityPolicy.BecameVisible(
                wasVisible,
                isVisible));
    }

    [Theory]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(false, true, true, false, false, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, false, true, true, false, true)]
    [InlineData(true, true, true, true, true, true)]
    public void EnablesOnlyRefreshWorkVisibleToTheUser(
        bool shellVisible,
        bool statusCenterOpen,
        bool calendarOpen,
        bool expectedClock,
        bool expectedSystemStatus,
        bool expectedTaskSummary)
    {
        ShellRefreshActivity activity =
            ShellRefreshActivityPolicy.GetActivity(
                shellVisible,
                statusCenterOpen,
                calendarOpen);

        Assert.Equal(expectedClock, activity.Clock);
        Assert.Equal(expectedSystemStatus, activity.SystemStatus);
        Assert.Equal(expectedTaskSummary, activity.TaskSummary);
    }
}
