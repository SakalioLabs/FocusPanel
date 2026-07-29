using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarGuardConfirmationTests
{
    [Fact]
    public void FirstInvalidObservation_DoesNotStop()
    {
        var confirmation =
            new TaskbarGuardConfirmation();

        Assert.False(
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .WindowsTaskbarReappeared));
    }

    [Fact]
    public void TwoMatchingInvalidObservations_ConfirmStop()
    {
        var confirmation =
            new TaskbarGuardConfirmation();

        confirmation.ObserveInvalid(
            TaskbarReplacementStopReason
                .WindowsTaskbarReappeared);
        bool confirmed =
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .WindowsTaskbarReappeared);

        Assert.True(confirmed);
    }

    [Fact]
    public void ValidObservation_ClearsPendingFailure()
    {
        var confirmation =
            new TaskbarGuardConfirmation();
        confirmation.ObserveInvalid(
            TaskbarReplacementStopReason
                .WindowsTaskbarReappeared);
        confirmation.ObserveValid();

        Assert.False(
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .WindowsTaskbarReappeared));
    }

    [Fact]
    public void DifferentFailureReason_StartsNewConfirmation()
    {
        var confirmation =
            new TaskbarGuardConfirmation();
        confirmation.ObserveInvalid(
            TaskbarReplacementStopReason
                .WindowsTaskbarReappeared);

        Assert.False(
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .ExplorerHostChanged));
        Assert.True(
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .ExplorerHostChanged));
    }

    [Fact]
    public void SingleObservationMode_IsSupportedForExplicitPolicies()
    {
        var confirmation =
            new TaskbarGuardConfirmation(
                requiredObservations: 1);

        Assert.True(
            confirmation.ObserveInvalid(
                TaskbarReplacementStopReason
                    .Unknown));
    }
}
