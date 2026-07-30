using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ElevatedAppLaunchServiceTests
{
    [Fact]
    public void ExecutableBuildsPublicRunAsRequest()
    {
        AppLaunchItem app = Demo(
            AppLaunchKind.Executable,
            @"C:\Apps\Editor.exe",
            "--profile Work");

        Assert.True(
            ElevatedAppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));
        Assert.NotNull(request);
        Assert.Equal(
            @"C:\Apps\Editor.exe",
            request.FileName);
        Assert.Equal("runas", request.Verb);
        Assert.Equal(
            "--profile Work",
            request.Arguments);
        Assert.True(request.UseShellExecute);
    }

    [Fact]
    public void ShortcutBuildsRunAsShellRequest()
    {
        AppLaunchItem app = Demo(
            AppLaunchKind.Shortcut,
            @"C:\Start Menu\Editor.lnk");

        Assert.True(
            ElevatedAppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));
        Assert.Equal("runas", request!.Verb);
        Assert.Equal(
            app.LaunchTarget,
            request.FileName);
    }

    [Theory]
    [InlineData(
        AppLaunchKind.ShellApp,
        "Contoso.App_123!App")]
    [InlineData(AppLaunchKind.Executable, "")]
    public void UnreliableTargetsAreRejected(
        AppLaunchKind kind,
        string target)
    {
        Assert.False(
            ElevatedAppLaunchRequestBuilder.TryBuild(
                Demo(kind, target),
                out ProcessStartInfo? request));
        Assert.Null(request);
    }

    [Fact]
    public void ExecutionUsesReplaceableBoundaryWithoutUac()
    {
        ProcessStartInfo? observed = null;
        var service =
            new ElevatedAppLaunchService(
                request => observed = request);

        ElevatedAppLaunchStatus status =
            service.Launch(
                Demo(
                    AppLaunchKind.Executable,
                    "Editor.exe"));

        Assert.Equal(
            ElevatedAppLaunchStatus.Started,
            status);
        Assert.Equal("runas", observed!.Verb);
    }

    [Fact]
    public void UacCancellationIsNotReportedAsFailure()
    {
        var service =
            new ElevatedAppLaunchService(
                _ => throw new Win32Exception(
                    1223));

        Assert.Equal(
            ElevatedAppLaunchStatus.Cancelled,
            service.Launch(
                Demo(
                    AppLaunchKind.Executable,
                    "Editor.exe")));
    }

    [Fact]
    public async Task CoordinatorRunsAwayFromUiThread()
    {
        int caller =
            System.Environment
                .CurrentManagedThreadId;
        int worker = caller;
        var coordinator =
            new ElevatedAppLaunchCoordinator(
                _ =>
                {
                    worker =
                        System.Environment
                            .CurrentManagedThreadId;
                    return ElevatedAppLaunchStatus
                        .Started;
                });

        ElevatedAppLaunchCompletion completion =
            await coordinator.LaunchAsync(
                Demo(
                    AppLaunchKind.Executable,
                    "Editor.exe"));

        Assert.Equal(
            ElevatedAppLaunchStatus.Started,
            completion.Status);
        Assert.NotEqual(caller, worker);
        Assert.True(
            coordinator.IsCurrent(
                completion.Revision));
    }

    private static AppLaunchItem Demo(
        AppLaunchKind kind,
        string target,
        string? arguments = null) =>
        new()
        {
            DisplayName = "编辑器",
            LaunchKind = kind,
            LaunchTarget = target,
            Arguments = arguments
        };
}
