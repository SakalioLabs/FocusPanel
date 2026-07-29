using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowSnapshotApplyPolicyTests
{
    [Fact]
    public void CurrentSnapshot_AppliesWhileTrackerIsActive()
    {
        Assert.True(
            WindowSnapshotApplyPolicy.CanApply(
                snapshotRevision: 4,
                currentRevision: 4,
                isTrackingActive: true,
                isDisposed: false,
                isCancellationRequested: false));
    }

    [Theory]
    [InlineData(3, 4, true, false, false)]
    [InlineData(4, 4, false, false, false)]
    [InlineData(4, 4, true, true, false)]
    [InlineData(4, 4, true, false, true)]
    public void StaleOrInactiveSnapshot_IsRejected(
        long snapshotRevision,
        long currentRevision,
        bool isTrackingActive,
        bool isDisposed,
        bool isCancellationRequested)
    {
        Assert.False(
            WindowSnapshotApplyPolicy.CanApply(
                snapshotRevision,
                currentRevision,
                isTrackingActive,
                isDisposed,
                isCancellationRequested));
    }
}
