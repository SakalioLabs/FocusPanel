using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarAppClickPolicyTests
{
    [Fact]
    public void PlainLeftClick_KeepsNormalTaskbarAction()
    {
        Assert.Equal(
            TaskbarAppClickAction.ActivateOrShowWindows,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: false,
                controlPressed: false,
                canLaunchNewInstance: true,
                windowCount: 2));
    }

    [Fact]
    public void ShiftLeftClick_LaunchesNewInstanceWhenTargetIsReliable()
    {
        Assert.Equal(
            TaskbarAppClickAction.LaunchNewInstance,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: false,
                canLaunchNewInstance: true,
                windowCount: 2));
    }

    [Fact]
    public void ShiftLeftClick_FallsBackToNormalActionWithoutLaunchTarget()
    {
        Assert.Equal(
            TaskbarAppClickAction.ActivateOrShowWindows,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: false,
                canLaunchNewInstance: false,
                windowCount: 2));
    }

    [Fact]
    public void ControlShiftLeftClick_RequestsAdministratorLaunch()
    {
        Assert.Equal(
            TaskbarAppClickAction.LaunchElevated,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: true,
                canLaunchNewInstance: true,
                windowCount: 2));
    }

    [Fact]
    public void ControlLeftClick_CyclesMultiWindowApplication()
    {
        Assert.Equal(
            TaskbarAppClickAction.CycleWindows,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: false,
                controlPressed: true,
                canLaunchNewInstance: true,
                windowCount: 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ControlLeftClick_KeepsNormalActionWithoutMultipleWindows(
        int windowCount)
    {
        Assert.Equal(
            TaskbarAppClickAction.ActivateOrShowWindows,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: false,
                controlPressed: true,
                canLaunchNewInstance: true,
                windowCount));
    }

    [Fact]
    public void MiddleClick_LaunchesNewInstanceWhenTargetIsReliable()
    {
        Assert.Equal(
            TaskbarAppClickAction.LaunchNewInstance,
            TaskbarAppClickPolicy.FromMiddleClick(
                canLaunchNewInstance: true));
    }

    [Fact]
    public void MiddleClick_DoesNothingWithoutLaunchTarget()
    {
        Assert.Equal(
            TaskbarAppClickAction.None,
            TaskbarAppClickPolicy.FromMiddleClick(
                canLaunchNewInstance: false));
    }
}
