using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Velopack.Sources;
using Xunit;

namespace FocusPanel.Tests;

public sealed class VelopackUpdateServiceTests
{
    [Fact]
    public async Task Construction_DoesNotWaitForSlowInstallationLocator()
    {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int factoryThread = callingThread;
        var watch = Stopwatch.StartNew();
        using var service =
            new VelopackUpdateService(
                () =>
                {
                    factoryThread =
                        Environment
                            .CurrentManagedThreadId;
                    started.Set();
                    release.Wait(
                        TimeSpan.FromSeconds(5));
                    return null;
                });
        watch.Stop();

        try
        {
            Assert.True(
                watch.Elapsed
                < TimeSpan.FromSeconds(1),
                $"构造函数阻塞了 {watch.ElapsedMilliseconds}ms。");
            Assert.True(
                started.Wait(
                    TimeSpan.FromSeconds(2)));
            Assert.NotEqual(
                callingThread,
                factoryThread);
            Assert.False(service.CanUpdate);
        }
        finally
        {
            release.Set();
        }

        Assert.Null(
            await service.CheckForUpdateAsync());
    }

    [Fact]
    public async Task DevelopmentBuild_DoesNotContactOrMutateUpdateFeed()
    {
        using var service = new VelopackUpdateService();

        Assert.False(service.CanUpdate);
        Assert.Null(await service.CheckForUpdateAsync());
    }

    [Fact]
    public async Task FirstCheck_AwaitsSharedInitializationAndPublishesCapability()
    {
        var expected =
            new AppUpdateInfo(
                "9.8.7",
                "测试更新",
                123);
        var boundary =
            new FakeUpdateBoundary(
                expected);
        int factoryCalls = 0;
        using var service =
            new VelopackUpdateService(
                () =>
                {
                    Interlocked.Increment(
                        ref factoryCalls);
                    return boundary;
                });

        AppUpdateInfo? actual =
            await service
                .CheckForUpdateAsync();

        Assert.Same(expected, actual);
        Assert.True(service.CanUpdate);
        Assert.Equal(
            "9.8.6",
            service.CurrentVersion);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(
            1,
            boundary.CheckCount);
    }

    [Fact]
    public void CurrentVersion_ComesFromApplicationAssembly()
    {
        using var service = new VelopackUpdateService();

        Assert.StartsWith("0.11.90", service.CurrentVersion);
    }

    [Fact]
    public void UpdateSource_UsesLatestReleaseStaticFeedWithoutApiEnumeration()
    {
        using var service = new VelopackUpdateService();

        Assert.Equal(
            "GitHub Releases · 静态清单",
            service.SourceDescription);
        Assert.Equal(
            "https://github.com/SakalioLabs/FocusPanel",
            VelopackUpdateService.RepositoryUrl);
        Assert.Equal(
            "https://github.com/SakalioLabs/FocusPanel/releases/latest/download",
            VelopackUpdateService.StaticFeedUrl);
        Assert.Equal(
            "https://github.com/SakalioLabs/FocusPanel/releases/latest",
            VelopackUpdateService.DownloadPageUrl);

        var source = Assert.IsType<SimpleWebSource>(
            VelopackUpdateService.CreateUpdateSource());
        Assert.Equal(
            new System.Uri(VelopackUpdateService.StaticFeedUrl),
            source.BaseUri);
    }

    private sealed class FakeUpdateBoundary :
        IVelopackUpdateBoundary
    {
        private readonly AppUpdateInfo?
            _update;

        public FakeUpdateBoundary(
            AppUpdateInfo? update)
        {
            _update = update;
        }

        public string? CurrentVersion =>
            "9.8.6";

        public bool CanUpdate => true;
        public int CheckCount { get; private set; }

        public Task<AppUpdateInfo?>
            CheckForUpdateAsync()
        {
            CheckCount++;
            return Task.FromResult(_update);
        }

        public Task DownloadUpdateAsync(
            IProgress<int>? progress,
            CancellationToken cancellationToken) =>
                Task.CompletedTask;

        public void ApplyAndRestart()
        {
        }
    }
}
