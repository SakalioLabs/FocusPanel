using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CoalescingBackgroundRefreshTests
{
    [Fact]
    public async Task Request_ReturnsWhileCaptureIsBlocked()
    {
        using var captureStarted = new ManualResetEventSlim();
        using var releaseCapture = new ManualResetEventSlim();
        using var refresh =
            new CoalescingBackgroundRefresh<int>(
                () =>
                {
                    captureStarted.Set();
                    releaseCapture.Wait(
                        TimeSpan.FromSeconds(2));
                    return 1;
                },
                (_, _) => Task.CompletedTask);

        Stopwatch requestDuration =
            Stopwatch.StartNew();
        refresh.Request();
        requestDuration.Stop();

        Assert.True(
            captureStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.True(
            requestDuration.Elapsed
                < TimeSpan.FromMilliseconds(500),
            $"Request blocked for {requestDuration.Elapsed}.");
        releaseCapture.Set();
        await refresh.WhenIdleAsync().WaitAsync(
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task BurstDuringCapture_ProducesOneTrailingRefresh()
    {
        using var firstCaptureStarted =
            new ManualResetEventSlim();
        using var releaseFirstCapture =
            new ManualResetEventSlim();
        var applied = new List<int>();
        int captureCount = 0;
        int activeCaptures = 0;
        int maximumActiveCaptures = 0;
        using var refresh =
            new CoalescingBackgroundRefresh<int>(
                () =>
                {
                    int active = Interlocked.Increment(
                        ref activeCaptures);
                    UpdateMaximum(
                        ref maximumActiveCaptures,
                        active);
                    try
                    {
                        int current =
                            Interlocked.Increment(
                                ref captureCount);
                        if (current == 1)
                        {
                            firstCaptureStarted.Set();
                            releaseFirstCapture.Wait(
                                TimeSpan.FromSeconds(2));
                        }

                        return current;
                    }
                    finally
                    {
                        Interlocked.Decrement(
                            ref activeCaptures);
                    }
                },
                (snapshot, _) =>
                {
                    applied.Add(snapshot);
                    return Task.CompletedTask;
                });

        refresh.Request();
        Assert.True(
            firstCaptureStarted.Wait(
                TimeSpan.FromSeconds(2)));
        refresh.Request();
        refresh.Request();
        refresh.Request();
        releaseFirstCapture.Set();

        await refresh.WhenIdleAsync().WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, captureCount);
        Assert.Equal(
            new[] { 1, 2 },
            applied);
        Assert.Equal(1, maximumActiveCaptures);
    }

    [Fact]
    public async Task CaptureFailure_DoesNotDiscardPendingRefresh()
    {
        using var firstCaptureStarted =
            new ManualResetEventSlim();
        using var releaseFailure =
            new ManualResetEventSlim();
        int captureCount = 0;
        int applied = 0;
        int failures = 0;
        using var refresh =
            new CoalescingBackgroundRefresh<int>(
                () =>
                {
                    int current =
                        Interlocked.Increment(
                            ref captureCount);
                    if (current == 1)
                    {
                        firstCaptureStarted.Set();
                        releaseFailure.Wait(
                            TimeSpan.FromSeconds(2));
                        throw new InvalidOperationException(
                            "device enumeration failed");
                    }

                    return current;
                },
                (snapshot, _) =>
                {
                    applied = snapshot;
                    return Task.CompletedTask;
                },
                _ => failures++);

        refresh.Request();
        Assert.True(
            firstCaptureStarted.Wait(
                TimeSpan.FromSeconds(2)));
        refresh.Request();
        releaseFailure.Set();

        await refresh.WhenIdleAsync().WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, captureCount);
        Assert.Equal(2, applied);
        Assert.Equal(1, failures);
    }

    [Fact]
    public async Task DiagnosticFailure_DoesNotStopTrailingRefresh()
    {
        int captureCount = 0;
        int applied = 0;
        using var firstCaptureStarted =
            new ManualResetEventSlim();
        using var releaseFailure =
            new ManualResetEventSlim();
        using var refresh =
            new CoalescingBackgroundRefresh<int>(
                () =>
                {
                    int current =
                        Interlocked.Increment(
                            ref captureCount);
                    if (current == 1)
                    {
                        firstCaptureStarted.Set();
                        releaseFailure.Wait(
                            TimeSpan.FromSeconds(2));
                        throw new InvalidOperationException(
                            "capture failed");
                    }

                    return current;
                },
                (snapshot, _) =>
                {
                    applied = snapshot;
                    return Task.CompletedTask;
                },
                _ => throw new InvalidOperationException(
                    "diagnostics failed"));

        refresh.Request();
        Assert.True(
            firstCaptureStarted.Wait(
                TimeSpan.FromSeconds(2)));
        refresh.Request();
        releaseFailure.Set();

        await refresh.WhenIdleAsync().WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(2, applied);
    }

    [Fact]
    public async Task Dispose_CancelsInFlightApplyAndIgnoresNewRequests()
    {
        using var captureStarted =
            new ManualResetEventSlim();
        using var releaseCapture =
            new ManualResetEventSlim();
        int captureCount = 0;
        int applyCount = 0;
        var refresh =
            new CoalescingBackgroundRefresh<int>(
                () =>
                {
                    captureStarted.Set();
                    releaseCapture.Wait(
                        TimeSpan.FromSeconds(2));
                    return Interlocked.Increment(
                        ref captureCount);
                },
                (_, _) =>
                {
                    applyCount++;
                    return Task.CompletedTask;
                });

        refresh.Request();
        Assert.True(
            captureStarted.Wait(
                TimeSpan.FromSeconds(2)));
        refresh.Dispose();
        refresh.Request();
        releaseCapture.Set();

        await refresh.WhenIdleAsync().WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureCount);
        Assert.Equal(0, applyCount);
    }

    private static void UpdateMaximum(
        ref int target,
        int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (candidate <= current)
                return;
        }
        while (Interlocked.CompareExchange(
                   ref target,
                   candidate,
                   current) != current);
    }
}
