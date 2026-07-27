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

        Assert.StartsWith("0.9.20", service.CurrentVersion);
    }
}
