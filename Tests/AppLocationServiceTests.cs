using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppLocationServiceTests
{
    [Fact]
    public void RunningExecutableTakesPriorityOverShortcut()
    {
        var launch = Create(
            AppLaunchKind.Shortcut,
            @"C:\Start Menu\Demo.lnk");

        Assert.True(
            AppLocationPolicy.TryResolve(
                launch,
                @"D:\Apps\Demo.exe",
                out AppLocationTarget target));
        Assert.Equal(
            @"D:\Apps\Demo.exe",
            target.Path);
        Assert.Equal(
            AppLocationKind.Executable,
            target.Kind);
        Assert.Equal(
            "打开程序位置",
            target.MenuLabel);
    }

    [Fact]
    public void ShortcutUsesItsOwnLocationWhenNotRunning()
    {
        Assert.True(
            AppLocationPolicy.TryResolve(
                Create(
                    AppLaunchKind.Shortcut,
                    @"C:\Start Menu\Demo.lnk"),
                null,
                out AppLocationTarget target));

        Assert.Equal(
            @"C:\Start Menu\Demo.lnk",
            target.Path);
        Assert.Equal(
            AppLocationKind.Shortcut,
            target.Kind);
        Assert.Equal(
            "打开快捷方式位置",
            target.MenuLabel);
    }

    [Theory]
    [InlineData(AppLaunchKind.Executable)]
    [InlineData(AppLaunchKind.Shortcut)]
    public void RelativeDesktopTargetsAreNotGuessed(
        AppLaunchKind kind)
    {
        Assert.False(
            AppLocationPolicy.TryResolve(
                Create(kind, "demo.exe"),
                null,
                out _));
    }

    [Fact]
    public void PackagedAppWithoutLocalPathIsUnsupported()
    {
        Assert.False(
            AppLocationPolicy.TryResolve(
                Create(
                    AppLaunchKind.ShellApp,
                    "Contoso.Package_123!App"),
                null,
                out _));
    }

    [Fact]
    public void ShellCatalogAbsolutePathCanBeLocated()
    {
        Assert.True(
            AppLocationPolicy.TryResolve(
                Create(
                    AppLaunchKind.ShellApp,
                    @"C:\Tools\Demo.exe"),
                null,
                out AppLocationTarget target));
        Assert.Equal(
            AppLocationKind.Executable,
            target.Kind);
    }

    [Fact]
    public void ExplorerRequestSelectsExactPath()
    {
        ProcessStartInfo request =
            AppLocationService
                .BuildExplorerRequest(
                    @"D:\Apps Folder\Demo.exe");

        Assert.Equal(
            "explorer.exe",
            request.FileName);
        Assert.Equal(
            "/select,\"D:\\Apps Folder\\Demo.exe\"",
            request.Arguments);
        Assert.True(request.UseShellExecute);
    }

    [Fact]
    public async Task ExistingTargetStartsExplorer()
    {
        ProcessStartInfo? observed = null;
        var service = new AppLocationService(
            _ => true,
            request =>
            {
                observed = request;
                return true;
            });

        AppLocationOpenResult result =
            await service.OpenAsync(
                new AppLocationTarget(
                    @"D:\Apps\Demo.exe",
                    AppLocationKind.Executable));

        Assert.Equal(
            AppLocationOpenStatus.Opened,
            result.Status);
        Assert.NotNull(observed);
        Assert.Equal(
            "/select,\"D:\\Apps\\Demo.exe\"",
            observed.Arguments);
    }

    [Fact]
    public async Task MissingTargetDoesNotOpenExplorer()
    {
        bool started = false;
        var service = new AppLocationService(
            _ => false,
            _ => started = true);

        AppLocationOpenResult result =
            await service.OpenAsync(
                new AppLocationTarget(
                    @"D:\Missing\Demo.exe",
                    AppLocationKind.Executable));

        Assert.Equal(
            AppLocationOpenStatus.Missing,
            result.Status);
        Assert.False(started);
    }

    [Fact]
    public async Task ExplorerFailureIsReturnedWithoutThrowing()
    {
        var service = new AppLocationService(
            _ => true,
            _ => throw new InvalidOperationException(
                "blocked"));

        AppLocationOpenResult result =
            await service.OpenAsync(
                new AppLocationTarget(
                    @"D:\Apps\Demo.exe",
                    AppLocationKind.Executable));

        Assert.Equal(
            AppLocationOpenStatus.Failed,
            result.Status);
        Assert.Equal("blocked", result.Error);
    }

    private static AppLaunchItem Create(
        AppLaunchKind kind,
        string target) =>
        new()
        {
            DisplayName = "Demo",
            LaunchKind = kind,
            LaunchTarget = target
        };
}
