using System.Threading.Tasks;
using FocusPanel.Services;
using Velopack.Sources;
using Xunit;

namespace FocusPanel.Tests;

public sealed class VelopackUpdateServiceTests
{
    [Fact]
    public async Task DevelopmentBuild_DoesNotContactOrMutateUpdateFeed()
    {
        using var service = new VelopackUpdateService();

        Assert.False(service.CanUpdate);
        Assert.Null(await service.CheckForUpdateAsync());
    }

    [Fact]
    public void CurrentVersion_ComesFromApplicationAssembly()
    {
        using var service = new VelopackUpdateService();

        Assert.StartsWith("0.10.9", service.CurrentVersion);
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
}
