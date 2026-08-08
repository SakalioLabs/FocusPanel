using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarRepairPolicyTests
{
    [Theory]
    [InlineData(
        TaskbarReplacementStopReason
            .WindowsTaskbarReappeared)]
    [InlineData(
        TaskbarReplacementStopReason
            .ExplorerHostChanged)]
    public void RecoverablePresentationFailures_AreEligible(
        TaskbarReplacementStopReason reason)
    {
        Assert.True(
            TaskbarRepairPolicy.IsRepairable(
                reason));
    }

    [Theory]
    [InlineData(
        TaskbarReplacementStopReason
            .EmergencyRestore)]
    [InlineData(
        TaskbarReplacementStopReason
            .StartupFailure)]
    [InlineData(
        TaskbarReplacementStopReason
            .Unknown)]
    public void SafetyAndLayoutFailures_AreNeverAutoRepaired(
        TaskbarReplacementStopReason reason)
    {
        Assert.False(
            TaskbarRepairPolicy.IsRepairable(
                reason));
    }
}
