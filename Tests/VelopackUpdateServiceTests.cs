using System.Threading.Tasks;
using FocusPanel.Services;
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

        Assert.StartsWith("0.9.31", service.CurrentVersion);
    }

    [Fact]
    public void UpdateSource_IsAlwaysGitHubReleases()
    {
        using var service = new VelopackUpdateService();

        Assert.Equal("GitHub Releases", service.SourceDescription);
        Assert.Equal(
            "https://github.com/SakalioLabs/FocusPanel",
            VelopackUpdateService.RepositoryUrl);
    }
}
