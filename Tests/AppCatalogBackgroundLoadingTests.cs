using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppCatalogBackgroundLoadingTests
{
    [Fact]
    public void Construction_DoesNotWaitForSlowStartMenuEnumeration()
    {
        var source = new BlockingCatalogSource();
        var watch = Stopwatch.StartNew();
        using var service = CreateService(
            source,
            new NullIconSource());
        watch.Stop();

        try
        {
            Assert.True(
                watch.Elapsed < TimeSpan.FromSeconds(1),
                $"构造函数阻塞了 {watch.ElapsedMilliseconds}ms。");
            Assert.True(
                service.IsIndexing);
            Assert.True(
                source.Entered.Wait(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            source.Release.Set();
        }
    }

    [Fact]
    public void Search_ReturnsBeforeSlowIconAndUpdatesItemLater()
    {
        var source = new ImmediateCatalogSource();
        var icons = new BlockingIconSource();
        using var service = CreateService(source, icons);
        Assert.True(
            SpinWait.SpinUntil(
                () => !service.IsIndexing,
                TimeSpan.FromSeconds(3)));

        Stopwatch watch = Stopwatch.StartNew();
        AppLaunchItem result =
            Assert.Single(service.Search("Demo"));
        watch.Stop();

        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(1),
            $"搜索被图标加载阻塞了 {watch.ElapsedMilliseconds}ms。");
        Assert.Null(result.Icon);
        Assert.True(
            icons.Entered.Wait(TimeSpan.FromSeconds(3)));

        icons.Release.Set();
        Assert.True(
            SpinWait.SpinUntil(
                () => result.Icon != null,
                TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Dispose_SuppressesLateCatalogNotification()
    {
        var source = new BlockingCatalogSource();
        var service = CreateService(
            source,
            new NullIconSource());
        int notifications = 0;
        service.CatalogChanged +=
            (_, _) => Interlocked.Increment(
                ref notifications);
        Assert.True(
            source.Entered.Wait(TimeSpan.FromSeconds(3)));

        service.Dispose();
        source.Release.Set();
        Assert.True(
            source.Finished.Wait(TimeSpan.FromSeconds(3)));
        Thread.Sleep(50);

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Refresh_DoesNotLetStaleIndexReplaceNewerResult()
    {
        var source = new SupersedingCatalogSource();
        using var service = CreateService(
            source,
            new NullIconSource());
        Assert.True(
            source.FirstEntered.Wait(
                TimeSpan.FromSeconds(3)));

        service.Refresh();
        Assert.True(
            SpinWait.SpinUntil(
                () => !service.IsIndexing,
                TimeSpan.FromSeconds(3)));
        Assert.Single(service.Search("Fresh"));

        source.ReleaseFirst.Set();
        Assert.True(
            source.FirstFinished.Wait(
                TimeSpan.FromSeconds(3)));
        Thread.Sleep(50);

        Assert.Single(service.Search("Fresh"));
        Assert.Empty(service.Search("Stale"));
    }

    [Fact]
    public void GetPinned_DoesNotResolveShortcutIdentityOnCaller()
    {
        var source = new BlockingCatalogSource();
        var identity = new CountingIdentityResolver();
        using var service = new AppCatalogService(
            identity,
            source,
            new NullIconSource(),
            () => new[]
            {
                new PinnedApp
                {
                    DisplayName = "Pinned",
                    LaunchKind = AppLaunchKind.Shortcut,
                    LaunchTarget = @"C:\Pinned.lnk",
                    OrderIndex = 0
                }
            });

        try
        {
            Assert.True(
                source.Entered.Wait(
                    TimeSpan.FromSeconds(3)));
            AppLaunchItem pinned =
                Assert.Single(service.GetPinned());

            Assert.Equal(0, identity.ResolveLaunchCalls);
            Assert.StartsWith(
                "launch:",
                pinned.IdentityKey);
        }
        finally
        {
            source.Release.Set();
        }
    }

    [Fact]
    public void Construction_DoesNotWaitForSlowPinnedStorage()
    {
        using var loaderEntered =
            new ManualResetEventSlim();
        using var releaseLoader =
            new ManualResetEventSlim();
        var watch = Stopwatch.StartNew();
        using var service = new AppCatalogService(
            new FakeIdentityResolver(),
            new ImmediateCatalogSource(),
            new NullIconSource(),
            () =>
            {
                loaderEntered.Set();
                releaseLoader.Wait(
                    TimeSpan.FromSeconds(5));
                return Array.Empty<PinnedApp>();
            });
        watch.Stop();

        try
        {
            Assert.True(
                watch.Elapsed
                    < TimeSpan.FromSeconds(1),
                $"构造函数等待固定项存储 {watch.ElapsedMilliseconds}ms。");
            Assert.True(
                loaderEntered.Wait(
                    TimeSpan.FromSeconds(3)));
            Stopwatch getPinnedDuration =
                Stopwatch.StartNew();
            Assert.Empty(service.GetPinned());
            getPinnedDuration.Stop();
            Assert.True(
                getPinnedDuration.Elapsed
                    < TimeSpan.FromMilliseconds(500),
                $"GetPinned 等待固定项存储 {getPinnedDuration.ElapsedMilliseconds}ms。");
        }
        finally
        {
            releaseLoader.Set();
        }
    }

    [Fact]
    public void RepeatedGetPinned_UsesOneBackgroundStorageSnapshot()
    {
        var source = new BlockingCatalogSource();
        int loadCount = 0;
        using var service = new AppCatalogService(
            new FakeIdentityResolver(),
            source,
            new NullIconSource(),
            () =>
            {
                Interlocked.Increment(
                    ref loadCount);
                return new[]
                {
                    Pinned("Pinned")
                };
            });

        try
        {
            Assert.True(
                source.Entered.Wait(
                    TimeSpan.FromSeconds(3)));

            Assert.Single(service.GetPinned());
            Assert.Single(service.GetPinned());
            Assert.Single(service.GetPinned());
            Assert.Equal(1, loadCount);
        }
        finally
        {
            source.Release.Set();
        }
    }

    [Fact]
    public void SupersededPinnedLoad_CannotReplaceNewerSnapshot()
    {
        using var firstLoaderEntered =
            new ManualResetEventSlim();
        using var releaseFirstLoader =
            new ManualResetEventSlim();
        int loadCount = 0;
        using var service = new AppCatalogService(
            new FakeIdentityResolver(),
            new ImmediateCatalogSource(),
            new NullIconSource(),
            () =>
            {
                if (Interlocked.Increment(
                        ref loadCount) == 1)
                {
                    firstLoaderEntered.Set();
                    releaseFirstLoader.Wait(
                        TimeSpan.FromSeconds(5));
                    return new[]
                    {
                        Pinned("Stale")
                    };
                }

                return new[]
                {
                    Pinned("Fresh")
                };
            });

        try
        {
            Assert.True(
                firstLoaderEntered.Wait(
                    TimeSpan.FromSeconds(3)));
            service.Refresh();
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                        service.GetPinned()
                            .SingleOrDefault()
                            ?.DisplayName
                        == "Fresh",
                    TimeSpan.FromSeconds(3)));

            releaseFirstLoader.Set();
            Thread.Sleep(50);

            Assert.Equal(
                "Fresh",
                Assert.Single(
                        service.GetPinned())
                    .DisplayName);
        }
        finally
        {
            releaseFirstLoader.Set();
        }
    }

    [Fact]
    public void FailedPinnedReload_PreservesLastValidSnapshot()
    {
        int loadCount = 0;
        using var service = new AppCatalogService(
            new FakeIdentityResolver(),
            new ImmediateCatalogSource(),
            new NullIconSource(),
            () =>
            {
                if (Interlocked.Increment(
                        ref loadCount) == 1)
                {
                    return new[]
                    {
                        Pinned("Stable")
                    };
                }

                throw new InvalidOperationException(
                    "database is busy");
            });
        Assert.True(
            SpinWait.SpinUntil(
                () => !service.IsIndexing,
                TimeSpan.FromSeconds(3)));
        Assert.Equal(
            "Stable",
            Assert.Single(
                    service.GetPinned())
                .DisplayName);

        service.Refresh();
        Assert.True(
            SpinWait.SpinUntil(
                () => !service.IsIndexing,
                TimeSpan.FromSeconds(3)));

        Assert.Equal(
            "Stable",
            Assert.Single(
                    service.GetPinned())
                .DisplayName);
    }

    private static AppCatalogService CreateService(
        IAppCatalogSource source,
        IAppIconSource icons) =>
        new(
            new FakeIdentityResolver(),
            source,
            icons,
            () => Array.Empty<PinnedApp>());

    private sealed class FakeIdentityResolver :
        IAppIdentityResolver
    {
        public ResolvedAppIdentity ResolveLaunch(
            AppLaunchItem app) =>
            new(
                $"launch:{app.LaunchTarget}",
                null,
                app.LaunchTarget);

        public ResolvedAppIdentity ResolveWindow(
            IntPtr window,
            uint processId,
            string? executablePath) =>
            throw new NotSupportedException();
    }

    private sealed class CountingIdentityResolver :
        IAppIdentityResolver
    {
        private int _resolveLaunchCalls;
        internal int ResolveLaunchCalls =>
            Volatile.Read(ref _resolveLaunchCalls);

        public ResolvedAppIdentity ResolveLaunch(
            AppLaunchItem app)
        {
            Interlocked.Increment(
                ref _resolveLaunchCalls);
            return new ResolvedAppIdentity(
                $"resolved:{app.LaunchTarget}",
                null,
                app.LaunchTarget);
        }

        public ResolvedAppIdentity ResolveWindow(
            IntPtr window,
            uint processId,
            string? executablePath) =>
            throw new NotSupportedException();
    }

    private sealed class ImmediateCatalogSource :
        IAppCatalogSource
    {
        public IEnumerable<AppLaunchItem>
            EnumerateStartMenuApps()
        {
            yield return Demo();
        }

        public IEnumerable<AppLaunchItem>
            EnumerateShellApps() =>
            Enumerable.Empty<AppLaunchItem>();
    }

    private sealed class BlockingCatalogSource :
        IAppCatalogSource
    {
        internal ManualResetEventSlim Entered { get; } =
            new(false);
        internal ManualResetEventSlim Release { get; } =
            new(false);
        internal ManualResetEventSlim Finished { get; } =
            new(false);

        public IEnumerable<AppLaunchItem>
            EnumerateStartMenuApps()
        {
            Entered.Set();
            try
            {
                Release.Wait(TimeSpan.FromSeconds(5));
                yield return Demo();
            }
            finally
            {
                Finished.Set();
            }
        }

        public IEnumerable<AppLaunchItem>
            EnumerateShellApps() =>
            Enumerable.Empty<AppLaunchItem>();
    }

    private sealed class SupersedingCatalogSource :
        IAppCatalogSource
    {
        private int _enumerationCount;
        internal ManualResetEventSlim FirstEntered { get; } =
            new(false);
        internal ManualResetEventSlim ReleaseFirst { get; } =
            new(false);
        internal ManualResetEventSlim FirstFinished { get; } =
            new(false);

        public IEnumerable<AppLaunchItem>
            EnumerateStartMenuApps()
        {
            if (Interlocked.Increment(
                    ref _enumerationCount) == 1)
            {
                FirstEntered.Set();
                try
                {
                    ReleaseFirst.Wait(
                        TimeSpan.FromSeconds(5));
                    yield return Demo("Stale");
                }
                finally
                {
                    FirstFinished.Set();
                }
                yield break;
            }

            yield return Demo("Fresh");
        }

        public IEnumerable<AppLaunchItem>
            EnumerateShellApps() =>
            Enumerable.Empty<AppLaunchItem>();
    }

    private sealed class NullIconSource : IAppIconSource
    {
        public ImageSource? Load(string iconKey) => null;
    }

    private sealed class BlockingIconSource : IAppIconSource
    {
        internal ManualResetEventSlim Entered { get; } =
            new(false);
        internal ManualResetEventSlim Release { get; } =
            new(false);

        public ImageSource? Load(string iconKey)
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
            var image = new DrawingImage();
            image.Freeze();
            return image;
        }
    }

    private static AppLaunchItem Demo(
        string displayName = "Demo") => new()
    {
        DisplayName = displayName,
        LaunchKind = AppLaunchKind.Executable,
        LaunchTarget = $@"C:\{displayName}.exe",
        IconKey = $@"C:\{displayName}.exe"
    };

    private static PinnedApp Pinned(
        string displayName) =>
        new()
        {
            DisplayName = displayName,
            LaunchKind = AppLaunchKind.Executable,
            LaunchTarget =
                $@"C:\{displayName}.exe",
            OrderIndex = 0
        };
}
