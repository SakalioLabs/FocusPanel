using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusDialogInteractionLeaseTests
{
    [Fact]
    public void EnterAndDispose_BalanceHostInteraction()
    {
        var host = new RecordingHost();

        using (FocusDialogInteractionLease.Enter(host))
        {
            Assert.Equal(1, host.BeginCount);
            Assert.Equal(0, host.EndCount);
        }

        Assert.Equal(1, host.BeginCount);
        Assert.Equal(1, host.EndCount);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var host = new RecordingHost();
        FocusDialogInteractionLease lease =
            FocusDialogInteractionLease.Enter(host);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, host.BeginCount);
        Assert.Equal(1, host.EndCount);
    }

    [Fact]
    public void MissingHost_RemainsSafe()
    {
        FocusDialogInteractionLease lease =
            FocusDialogInteractionLease.Enter(null);

        lease.Dispose();
    }

    private sealed class RecordingHost
        : IFocusDialogInteractionHost
    {
        internal int BeginCount { get; private set; }
        internal int EndCount { get; private set; }

        public void BeginTransientInteraction() =>
            BeginCount++;

        public void EndTransientInteraction() =>
            EndCount++;
    }
}
