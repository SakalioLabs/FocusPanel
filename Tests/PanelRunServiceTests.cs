using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelRunServiceTests
{
    [Theory]
    [InlineData(">notepad.exe", "notepad.exe", "")]
    [InlineData("> notepad.exe readme.txt", "notepad.exe", "readme.txt")]
    [InlineData(
        "> \"C:\\Program Files\\Demo\\Demo.exe\" --profile Work",
        "C:\\Program Files\\Demo\\Demo.exe",
        "--profile Work")]
    [InlineData(">https://example.com", "https://example.com", "")]
    [InlineData("> C:\\Temp", "C:\\Temp", "")]
    public void ParserSeparatesTargetAndArguments(
        string query,
        string expectedFileName,
        string expectedArguments)
    {
        Assert.True(
            PanelRunCommandParser.TryParse(
                query,
                out PanelRunCommand command));
        Assert.Equal(
            expectedFileName,
            command.FileName);
        Assert.Equal(
            expectedArguments,
            command.Arguments);
        Assert.StartsWith(
            "run:",
            command.StableKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notepad.exe")]
    [InlineData(">")]
    [InlineData(">   ")]
    [InlineData(">\"\"")]
    [InlineData(">\"unterminated")]
    public void ParserRejectsIncompleteOrImplicitCommands(
        string? query)
    {
        Assert.False(
            PanelRunCommandParser.TryParse(
                query,
                out _));
    }

    [Theory]
    [InlineData(">")]
    [InlineData("  > notepad")]
    public void DraftRequiresExplicitPrefix(
        string query)
    {
        Assert.True(
            PanelRunCommandParser.IsDraft(
                query));
        Assert.False(
            PanelRunCommandParser.IsDraft(
                "notepad"));
    }

    [Fact]
    public void RequestExpandsEnvironmentWithoutUsingCmd()
    {
        ProcessStartInfo request =
            PanelRunService.BuildRequest(
                new PanelRunCommand(
                    "%TOOLS%\\Demo.exe",
                    "--data %DATA%"),
                value => value
                    .Replace(
                        "%TOOLS%",
                        @"D:\Tools",
                        StringComparison.Ordinal)
                    .Replace(
                        "%DATA%",
                        @"D:\Data",
                        StringComparison.Ordinal));

        Assert.Equal(
            @"D:\Tools\Demo.exe",
            request.FileName);
        Assert.Equal(
            @"--data D:\Data",
            request.Arguments);
        Assert.True(request.UseShellExecute);
        Assert.NotEqual(
            "cmd.exe",
            request.FileName);
    }

    [Fact]
    public async Task ServiceStartsOnlyInjectedRequest()
    {
        ProcessStartInfo? observed = null;
        var service = new PanelRunService(
            value => value,
            request =>
            {
                observed = request;
                return true;
            });

        PanelRunResult result =
            await service.RunAsync(
                new PanelRunCommand(
                    "demo.exe",
                    "--safe"));

        Assert.Equal(
            PanelRunStatus.Started,
            result.Status);
        Assert.NotNull(observed);
        Assert.Equal(
            "demo.exe",
            observed.FileName);
        Assert.Equal(
            "--safe",
            observed.Arguments);
    }

    [Fact]
    public async Task StartFailureIsReturnedWithoutThrowing()
    {
        var service = new PanelRunService(
            value => value,
            _ => throw new InvalidOperationException(
                "blocked"));

        PanelRunResult result =
            await service.RunAsync(
                new PanelRunCommand(
                    "missing.exe",
                    string.Empty));

        Assert.Equal(
            PanelRunStatus.Failed,
            result.Status);
        Assert.Equal("blocked", result.Error);
    }
}
