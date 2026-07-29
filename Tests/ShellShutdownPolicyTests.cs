using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellShutdownPolicyTests
{
    [Theory]
    [InlineData(
        false,
        false,
        false,
        (int)ShellClosingAction.HideToTray)]
    [InlineData(
        true,
        false,
        false,
        (int)ShellClosingAction.BeginAsyncShutdown)]
    [InlineData(
        true,
        true,
        false,
        (int)ShellClosingAction.WaitForAsyncShutdown)]
    [InlineData(
        true,
        true,
        true,
        (int)ShellClosingAction.AllowClose)]
    public void Decide_SeparatesTrayCloseFromTwoPhaseExit(
        bool isExitRequested,
        bool shutdownStarted,
        bool shutdownCompleted,
        int expected)
    {
        Assert.Equal(
            (ShellClosingAction)expected,
            ShellShutdownPolicy.Decide(
                isExitRequested,
                shutdownStarted,
                shutdownCompleted));
    }
}
