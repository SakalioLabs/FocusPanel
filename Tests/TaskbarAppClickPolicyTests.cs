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
                canLaunchNewInstance: true));
    }

    [Fact]
    public void ShiftLeftClick_LaunchesNewInstanceWhenTargetIsReliable()
    {
        Assert.Equal(
            TaskbarAppClickAction.LaunchNewInstance,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: false,
                canLaunchNewInstance: true));
    }

    [Fact]
    public void ShiftLeftClick_FallsBackToNormalActionWithoutLaunchTarget()
    {
        Assert.Equal(
            TaskbarAppClickAction.ActivateOrShowWindows,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: false,
                canLaunchNewInstance: false));
    }

    [Fact]
    public void ControlShiftLeftClick_RequestsAdministratorLaunch()
    {
        Assert.Equal(
            TaskbarAppClickAction.LaunchElevated,
            TaskbarAppClickPolicy.FromLeftClick(
                shiftPressed: true,
                controlPressed: true,
                canLaunchNewInstance: true));
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
