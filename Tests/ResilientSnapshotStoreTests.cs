using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ResilientSnapshotStoreTests
{
    [Fact]
    public void SuccessfulCapture_ReplacesSnapshot()
    {
        var store =
            new ResilientSnapshotStore<string>();

        bool refreshed = store.TryRefresh(
            () => new[] { "one", "two" },
            out Exception? failure);

        Assert.True(refreshed);
        Assert.Null(failure);
        Assert.Equal(
            new[] { "one", "two" },
            store.Current);
    }

    [Fact]
    public void FailedCapture_PreservesLastValidSnapshot()
    {
        var store =
            new ResilientSnapshotStore<string>();
        store.TryRefresh(
            () => new[] { "stable" },
            out _);

        bool refreshed = store.TryRefresh(
            () => throw new InvalidOperationException(
                "enumeration failed"),
            out Exception? failure);

        Assert.False(refreshed);
        Assert.IsType<InvalidOperationException>(
            failure);
        Assert.Equal(
            new[] { "stable" },
            store.Current);
    }

    [Fact]
    public void NullCapture_IsRejectedWithoutLosingSnapshot()
    {
        var store =
            new ResilientSnapshotStore<string>();
        store.TryRefresh(
            () => new[] { "stable" },
            out _);

        bool refreshed = store.TryRefresh(
            () => null!,
            out Exception? failure);

        Assert.False(refreshed);
        Assert.NotNull(failure);
        Assert.Equal(
            new[] { "stable" },
            store.Current);
    }

    [Fact]
    public void InitialFailure_LeavesEmptySnapshot()
    {
        var store =
            new ResilientSnapshotStore<string>();

        store.TryRefresh(
            () => throw new InvalidOperationException(),
            out _);

        Assert.Empty(store.Current);
    }
}
