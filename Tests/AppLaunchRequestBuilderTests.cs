using System;
using System.Diagnostics;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppLaunchRequestBuilderTests
{
    [Fact]
    public void Executable_PreservesTargetAndArguments()
    {
        var app = Create(
            AppLaunchKind.Executable,
            @"C:\Apps\Demo.exe",
            "--profile \"Work\"");

        Assert.True(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));

        Assert.NotNull(request);
        Assert.Equal(@"C:\Apps\Demo.exe", request.FileName);
        Assert.Equal(
            "--profile \"Work\"",
            request.Arguments);
        Assert.True(request.UseShellExecute);
        Assert.Empty(request.ArgumentList);
    }

    [Fact]
    public void Shortcut_UsesShortcutAsShellTarget()
    {
        var app = Create(
            AppLaunchKind.Shortcut,
            @"C:\Start Menu\Demo.lnk");

        Assert.True(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));

        Assert.NotNull(request);
        Assert.Equal(
            @"C:\Start Menu\Demo.lnk",
            request.FileName);
        Assert.True(request.UseShellExecute);
    }

    [Fact]
    public void ShellAppAumid_UsesAppsFolderNamespace()
    {
        var app = Create(
            AppLaunchKind.ShellApp,
            "Contoso.Package_123!App");

        Assert.True(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));

        Assert.NotNull(request);
        Assert.Equal("explorer.exe", request.FileName);
        Assert.Equal(
            new[]
            {
                @"shell:AppsFolder\Contoso.Package_123!App"
            },
            request.ArgumentList);
        Assert.True(request.UseShellExecute);
    }

    [Fact]
    public void ShellAppAbsolutePath_RemainsDirectTarget()
    {
        var app = Create(
            AppLaunchKind.ShellApp,
            @"C:\Tools\ShellListed.exe");

        Assert.True(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));

        Assert.NotNull(request);
        Assert.Equal(
            @"C:\Tools\ShellListed.exe",
            request.FileName);
        Assert.Empty(request.ArgumentList);
    }

    [Fact]
    public void ExistingShellNamespace_RemainsDirectTarget()
    {
        var app = Create(
            AppLaunchKind.ShellApp,
            "shell:AppsFolder\\Contoso.Package_123!App");

        Assert.True(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));

        Assert.NotNull(request);
        Assert.Equal(
            "shell:AppsFolder\\Contoso.Package_123!App",
            request.FileName);
        Assert.Empty(request.ArgumentList);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTarget_IsRejected(string target)
    {
        var app = Create(
            AppLaunchKind.Executable,
            target);

        Assert.False(
            AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? request));
        Assert.Null(request);
    }

    [Fact]
    public void Execution_SucceedsWithoutStartingRealProcess()
    {
        var request = new ProcessStartInfo("demo.exe");
        ProcessStartInfo? observed = null;

        bool result = AppLaunchExecution.TryStart(
            request,
            value => observed = value);

        Assert.True(result);
        Assert.Same(request, observed);
    }

    [Fact]
    public void Execution_ConvertsStartFailureToFalse()
    {
        var request = new ProcessStartInfo("missing.exe");

        bool result = AppLaunchExecution.TryStart(
            request,
            _ => throw new InvalidOperationException());

        Assert.False(result);
    }

    private static AppLaunchItem Create(
        AppLaunchKind kind,
        string target,
        string? arguments = null) =>
        new()
        {
            DisplayName = "Demo",
            LaunchKind = kind,
            LaunchTarget = target,
            Arguments = arguments
        };
}
