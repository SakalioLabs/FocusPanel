using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class EdgeHotZoneMonitorTests
{
    private static readonly Rectangle Screen =
        new(0, 0, 1920, 1080);

    [Fact]
    public void Start_DoesNotWaitForBlockedBackgroundSample()
    {
        using var sampleStarted =
            new ManualResetEventSlim();
        using var releaseSample =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int sampleThread = callingThread;
        using var monitor =
            CreateMonitor(
                isSuppressed: () =>
                {
                    sampleThread =
                        Environment
                            .CurrentManagedThreadId;
                    sampleStarted.Set();
                    releaseSample.Wait(
                        TimeSpan.FromSeconds(5));
                    return false;
                });

        var watch = Stopwatch.StartNew();
        monitor.Start();
        watch.Stop();

        try
        {
            Assert.True(
                watch.Elapsed
                < TimeSpan.FromSeconds(1),
                $"Start 阻塞了 {watch.ElapsedMilliseconds}ms。");
            Assert.True(
                sampleStarted.Wait(
                    TimeSpan.FromSeconds(2)));
            Assert.NotEqual(
                callingThread,
                sampleThread);
        }
        finally
        {
            monitor.Stop();
            releaseSample.Set();
        }
    }

    [Fact]
    public void Stop_DropsACompletedSampleFromPreviousGeneration()
    {
        using var sampleStarted =
            new ManualResetEventSlim();
        using var releaseSample =
            new ManualResetEventSlim();
        var queuedUiActions =
            new ConcurrentQueue<Action>();
        int availabilityEvents = 0;
        bool? lastAvailability = null;
        int openEvents = 0;
        using var monitor =
            CreateMonitor(
                isSuppressed: () =>
                {
                    sampleStarted.Set();
                    releaseSample.Wait(
                        TimeSpan.FromSeconds(5));
                    return false;
                },
                postToUi:
                    queuedUiActions.Enqueue);
        monitor.AvailabilityChanged +=
            available =>
            {
                lastAvailability = available;
                availabilityEvents++;
            };
        monitor.OpenRequested +=
            (_, _) => openEvents++;

        monitor.Start();
        Assert.True(
            sampleStarted.Wait(
                TimeSpan.FromSeconds(2)));
        monitor.Stop();
        releaseSample.Set();
        Thread.Sleep(80);
        while (queuedUiActions.TryDequeue(
                   out Action? action))
        {
            action();
        }

        Assert.Equal(1, availabilityEvents);
        Assert.False(lastAvailability);
        Assert.Equal(0, openEvents);
    }

    [Fact]
    public void Dwell_OnlyPostsAvailabilityChangeAndOpenRequest()
    {
        using var opened =
            new ManualResetEventSlim();
        int availabilityEvents = 0;
        bool? lastAvailability = null;
        using var monitor =
            CreateMonitor(
                pollInterval:
                    TimeSpan.FromMilliseconds(10));
        monitor.AvailabilityChanged +=
            available =>
            {
                lastAvailability = available;
                Interlocked.Increment(
                    ref availabilityEvents);
            };
        monitor.OpenRequested +=
            (_, _) => opened.Set();

        monitor.Start();
        try
        {
            Assert.True(
                opened.Wait(
                    TimeSpan.FromSeconds(2)));
            Assert.True(lastAvailability);
            Assert.Equal(
                1,
                Volatile.Read(
                    ref availabilityEvents));
        }
        finally
        {
            monitor.Stop();
        }
    }

    private static EdgeHotZoneMonitor
        CreateMonitor(
            Func<bool>? isSuppressed = null,
            Action<Action>? postToUi = null,
            TimeSpan? pollInterval = null) =>
            new(
                () => Screen,
                () => new Point(
                    Screen.Right - 1,
                    Screen.Top + 100),
                isSuppressed
                    ?? (() => false),
                postToUi
                    ?? (action => action()),
                () => Stopwatch.GetTimestamp()
                    * 1000L
                    / Stopwatch.Frequency,
                pollInterval
                    ?? TimeSpan
                        .FromMilliseconds(30));
}
