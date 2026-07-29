using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppCatalogPinnedWriteTests
{
    [Fact]
    public async Task SetPinned_ReturnsWhileStorageIsBlocked()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        using var service = CreateService(
            setPinned: (app, pinned) =>
            {
                started.Set();
                release.Wait(
                    TimeSpan.FromSeconds(2));
                return Success(
                    pinned
                        ? new[] { Persisted(app, 0) }
                        : Array.Empty<PinnedApp>());
            });
        AppLaunchItem app = Demo("Editor");

        Stopwatch duration =
            Stopwatch.StartNew();
        Task<bool> pending =
            service.SetPinnedAsync(
                app,
                true);
        duration.Stop();

        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Assert.True(
            duration.Elapsed
                < TimeSpan.FromMilliseconds(500),
            $"Pin request blocked for {duration.Elapsed}.");
        release.Set();

        Assert.True(
            await pending.WaitAsync(
                TimeSpan.FromSeconds(2)));
        Assert.True(app.IsPinned);
        Assert.Single(service.GetPinned());
    }

    [Fact]
    public async Task PinAndMoveWrites_AreStrictlySerialized()
    {
        using var setStarted =
            new ManualResetEventSlim();
        using var releaseSet =
            new ManualResetEventSlim();
        using var moveStarted =
            new ManualResetEventSlim();
        AppLaunchItem app = Demo("Terminal");
        using var service = CreateService(
            setPinned: (_, _) =>
            {
                setStarted.Set();
                releaseSet.Wait(
                    TimeSpan.FromSeconds(2));
                return Success(
                    new[] { Persisted(app, 0) });
            },
            movePinned: _ =>
            {
                moveStarted.Set();
                return Success(
                    new[] { Persisted(app, 0) });
            });

        Task<bool> pin =
            service.SetPinnedAsync(
                app,
                true);
        Assert.True(
            setStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<bool> move =
            service.MovePinnedAsync(
                app,
                0);

        Assert.False(
            moveStarted.Wait(
                TimeSpan.FromMilliseconds(120)));
        releaseSet.Set();
        bool[] results =
            await Task.WhenAll(pin, move)
                .WaitAsync(
                    TimeSpan.FromSeconds(2));

        Assert.All(results, Assert.True);
        Assert.True(moveStarted.IsSet);
    }

    [Fact]
    public async Task FailedWrite_DoesNotMutatePinnedSnapshotAndGateRecovers()
    {
        int calls = 0;
        AppLaunchItem app = Demo("Browser");
        using var service = CreateService(
            setPinned: (_, _) =>
            {
                if (Interlocked.Increment(
                        ref calls) == 1)
                {
                    throw new InvalidOperationException(
                        "database busy");
                }

                return Success(
                    new[] { Persisted(app, 0) });
            });

        Assert.False(
            await service.SetPinnedAsync(
                app,
                true));
        Assert.Empty(service.GetPinned());
        Assert.False(app.IsPinned);

        Assert.True(
            await service.SetPinnedAsync(
                app,
                true));
        Assert.Single(service.GetPinned());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task MoveCommit_UsesPersistedStableOrder()
    {
        AppLaunchItem first = Demo("First");
        AppLaunchItem second = Demo("Second");
        using var service = CreateService(
            movePinned: _ =>
                Success(
                    new[]
                    {
                        Persisted(second, 0),
                        Persisted(first, 1)
                    }));

        Assert.True(
            await service.MovePinnedAsync(
                first,
                1));

        Assert.Equal(
            new[] { "Second", "First" },
            service.GetPinned()
                .Select(item =>
                    item.DisplayName));
    }

    [Fact]
    public async Task Dispose_WaitsForInFlightDatabaseWrite()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        AppLaunchItem app = Demo("Notes");
        var service = CreateService(
            setPinned: (_, _) =>
            {
                started.Set();
                release.Wait(
                    TimeSpan.FromSeconds(2));
                return Success(
                    new[] { Persisted(app, 0) });
            });

        Task<bool> write =
            service.SetPinnedAsync(
                app,
                true);
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));
        Task dispose =
            Task.Run(service.Dispose);

        await Task.Delay(80);
        Assert.False(dispose.IsCompleted);
        release.Set();
        await Task.WhenAll(write, dispose)
            .WaitAsync(
                TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotBlockCallerAndDrainsWrite()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        AppLaunchItem app = Demo("Calendar");
        var service = CreateService(
            setPinned: (_, _) =>
            {
                started.Set();
                release.Wait(
                    TimeSpan.FromSeconds(2));
                return Success(
                    new[] { Persisted(app, 0) });
            });

        Task<bool> write =
            service.SetPinnedAsync(
                app,
                true);
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(2)));

        Task dispose =
            service.DisposeAsync();

        Assert.False(dispose.IsCompleted);
        release.Set();
        await Task.WhenAll(
                write,
                dispose)
            .WaitAsync(
                TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RelativeAndStepMoves_KeepTypedIntent()
    {
        var requests =
            new List<PinnedAppMoveRequest>();
        AppLaunchItem source =
            Demo("Editor");
        AppLaunchItem target =
            Demo("Terminal");
        using var service = CreateService(
            movePinned: request =>
            {
                requests.Add(request);
                return Success(
                    new[]
                    {
                        Persisted(
                            source,
                            0)
                    });
            });

        Assert.True(
            await service
                .MovePinnedRelativeAsync(
                    source,
                    target,
                    TaskbarDropPlacement
                        .Before));
        Assert.True(
            await service
                .MovePinnedByOffsetAsync(
                    source,
                    -1));

        Assert.Equal(2, requests.Count);
        Assert.Same(
            target,
            requests[0].RelativeTarget);
        Assert.Equal(
            TaskbarDropPlacement.Before,
            requests[0].Placement);
        Assert.Null(requests[0].Offset);
        Assert.Equal(
            -1,
            requests[1].Offset);
        Assert.Null(
            requests[1].Placement);
    }

    [Fact]
    public async Task RelativeAndStepMoves_AreStrictlySerialized()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        int calls = 0;
        int active = 0;
        int maximumActive = 0;
        AppLaunchItem source =
            Demo("Editor");
        using var service = CreateService(
            movePinned: _ =>
            {
                int current =
                    Interlocked.Increment(
                        ref active);
                maximumActive = Math.Max(
                    maximumActive,
                    current);
                if (Interlocked.Increment(
                        ref calls) == 1)
                {
                    firstStarted.Set();
                    releaseFirst.Wait(
                        TimeSpan
                            .FromSeconds(2));
                }

                Interlocked.Decrement(
                    ref active);
                return Success(
                    new[]
                    {
                        Persisted(
                            source,
                            0)
                    });
            });

        Task<bool> relative =
            service.MovePinnedRelativeAsync(
                source,
                source,
                TaskbarDropPlacement.After);
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<bool> step =
            service.MovePinnedByOffsetAsync(
                source,
                1);

        await Task.Delay(100);
        Assert.Equal(1, calls);
        releaseFirst.Set();
        bool[] results =
            await Task.WhenAll(
                    relative,
                    step)
                .WaitAsync(
                    TimeSpan.FromSeconds(2));

        Assert.All(results, Assert.True);
        Assert.Equal(2, calls);
        Assert.Equal(1, maximumActive);
    }

    private static AppCatalogService CreateService(
        Func<AppLaunchItem, bool, PinnedAppMutationResult>?
            setPinned = null,
        Func<PinnedAppMoveRequest, PinnedAppMutationResult>?
            movePinned = null) =>
        new(
            new FakeIdentityResolver(),
            new EmptyCatalogSource(),
            new NullIconSource(),
            () => Array.Empty<PinnedApp>(),
            new PinnedAppPersistenceHandlers(
                setPinned
                ?? ((_, _) =>
                    Success(
                        Array.Empty<PinnedApp>())),
                movePinned
                ?? (_ =>
                    new PinnedAppMutationResult(
                        false,
                        Array.Empty<PinnedApp>()))));

    private static PinnedAppMutationResult Success(
        IReadOnlyList<PinnedApp> ordered) =>
        new(
            true,
            ordered);

    private static AppLaunchItem Demo(
        string name) =>
        new()
        {
            DisplayName = name,
            LaunchKind =
                AppLaunchKind.Executable,
            LaunchTarget =
                $@"C:\{name}.exe",
            IconKey =
                $@"C:\{name}.exe"
        };

    private static PinnedApp Persisted(
        AppLaunchItem app,
        int order) =>
        new()
        {
            DisplayName = app.DisplayName,
            LaunchKind = app.LaunchKind,
            LaunchTarget = app.LaunchTarget,
            Arguments = app.Arguments,
            IconKey = app.IconKey,
            OrderIndex = order,
            CreatedAt = DateTime.Now
        };

    private sealed class FakeIdentityResolver :
        IAppIdentityResolver
    {
        public ResolvedAppIdentity ResolveLaunch(
            AppLaunchItem app) =>
            new(
                $"exe:{app.LaunchTarget}",
                null,
                app.LaunchTarget);

        public ResolvedAppIdentity ResolveWindow(
            IntPtr window,
            uint processId,
            string? executablePath) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyCatalogSource :
        IAppCatalogSource
    {
        public IEnumerable<AppLaunchItem>
            EnumerateStartMenuApps() =>
            Enumerable.Empty<AppLaunchItem>();

        public IEnumerable<AppLaunchItem>
            EnumerateShellApps() =>
            Enumerable.Empty<AppLaunchItem>();
    }

    private sealed class NullIconSource :
        IAppIconSource
    {
        public ImageSource? Load(
            string iconKey) =>
            null;
    }
}
