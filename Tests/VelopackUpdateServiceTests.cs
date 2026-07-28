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

        Assert.StartsWith("0.9.25", service.CurrentVersion);
    }

    [Theory]
    [InlineData("http://192.168.1.10:8088", "http://192.168.1.10:8088/")]
    [InlineData("https://updates.example.test/focus", "https://updates.example.test/focus/")]
    public void LanHttpSource_IsNormalized(string input, string expected)
    {
        bool succeeded = AppUpdateSourcePolicy.TryNormalize(
            new AppUpdateSourceConfiguration(AppUpdateSourceKind.Lan, input),
            out AppUpdateSourceConfiguration normalized,
            out string? error);

        Assert.True(succeeded, error);
        Assert.Equal(AppUpdateSourceKind.Lan, normalized.Kind);
        Assert.Equal(expected, normalized.Location);
    }

    [Fact]
    public void LanUncSource_IsAcceptedWithoutConvertingToHttp()
    {
        bool succeeded = AppUpdateSourcePolicy.TryNormalize(
            new AppUpdateSourceConfiguration(
                AppUpdateSourceKind.Lan,
                @"\\update-host\FocusPanelUpdates\"),
            out AppUpdateSourceConfiguration normalized,
            out string? error);

        Assert.True(succeeded, error);
        Assert.Equal(@"\\update-host\FocusPanelUpdates", normalized.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("updates")]
    [InlineData("ftp://update-host/FocusPanel")]
    public void InvalidLanSource_IsRejected(string input)
    {
        bool succeeded = AppUpdateSourcePolicy.TryNormalize(
            new AppUpdateSourceConfiguration(AppUpdateSourceKind.Lan, input),
            out _,
            out string? error);

        Assert.False(succeeded);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void GitHubSource_IgnoresStaleLanLocation()
    {
        bool succeeded = AppUpdateSourcePolicy.TryNormalize(
            new AppUpdateSourceConfiguration(AppUpdateSourceKind.GitHub, "stale"),
            out AppUpdateSourceConfiguration normalized,
            out string? error);

        Assert.True(succeeded, error);
        Assert.Equal(VelopackUpdateService.RepositoryUrl, normalized.Location);
    }
}
