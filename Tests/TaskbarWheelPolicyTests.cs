using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarWheelPolicyTests
{
    [Theory]
    [InlineData(120, false, 1)]
    [InlineData(-120, false, 4)]
    [InlineData(120, true, 1)]
    public void Wheel_DefaultsToScrollingApplicationList(
        int delta,
        bool controlPressed,
        int windowCount)
    {
        Assert.Equal(
            TaskbarWheelAction.ScrollApps,
            TaskbarWheelPolicy.GetAction(
                delta,
                controlPressed,
                windowCount));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(-120)]
    public void ControlWheel_CyclesOnlyMultiWindowApplication(
        int delta)
    {
        Assert.Equal(
            TaskbarWheelAction.CycleWindows,
            TaskbarWheelPolicy.GetAction(
                delta,
                controlPressed: true,
                windowCount: 2));
    }

    [Fact]
    public void ZeroDelta_IsIgnored()
    {
        Assert.Equal(
            TaskbarWheelAction.Ignore,
            TaskbarWheelPolicy.GetAction(
                0,
                controlPressed: false,
                windowCount: 3));
    }
}
