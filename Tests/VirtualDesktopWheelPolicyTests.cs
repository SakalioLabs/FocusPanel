using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    VirtualDesktopWheelPolicyTests
{
    [Fact]
    public void Direction_MapsToAdjacentDesktop()
    {
        Assert.Equal(
            VirtualDesktopWheelAction
                .Previous,
            VirtualDesktopWheelPolicy.GetAction(
                120,
                -1,
                1000));
        Assert.Equal(
            VirtualDesktopWheelAction.Next,
            VirtualDesktopWheelPolicy.GetAction(
                -120,
                -1,
                1000));
    }

    [Fact]
    public void ZeroDelta_IsIgnored()
    {
        Assert.Equal(
            VirtualDesktopWheelAction.Ignore,
            VirtualDesktopWheelPolicy.GetAction(
                0,
                -1,
                1000));
    }

    [Theory]
    [InlineData(1159, true)]
    [InlineData(1160, false)]
    public void RapidInput_IsThrottledAtBoundary(
        long currentTick,
        bool throttled)
    {
        VirtualDesktopWheelAction action =
            VirtualDesktopWheelPolicy.GetAction(
                -120,
                1000,
                currentTick);

        Assert.Equal(
            throttled
                ? VirtualDesktopWheelAction
                    .Throttled
                : VirtualDesktopWheelAction
                    .Next,
            action);
    }

    [Fact]
    public void ClockReset_DoesNotLockSwitching()
    {
        Assert.Equal(
            VirtualDesktopWheelAction.Previous,
            VirtualDesktopWheelPolicy.GetAction(
                120,
                5000,
                100));
    }
}
