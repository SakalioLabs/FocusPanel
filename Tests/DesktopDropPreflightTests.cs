using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopDropPreflightTests
{
    private const string UserDesktop =
        @"C:\Users\Test\Desktop";
    private const string CommonDesktop =
        @"C:\Users\Public\Desktop";

    [Fact]
    public async Task Resolve_ReturnsTaskWhileExistenceCheckIsBlocked()
    {
        int callerThread =
            Environment.CurrentManagedThreadId;
        int workerThread = callerThread;
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        using var release =
            new ManualResetEventSlim(false);
        var preflight =
            new DesktopDropPreflight(
                path => path,
                _ =>
                {
                    workerThread =
                        Environment
                            .CurrentManagedThreadId;
                    entered.TrySetResult(true);
                    release.Wait();
                    return true;
                });

        Task<DesktopDropPreflightResult> task =
            preflight.ResolveAsync(
                new[]
                {
                    UserDesktop
                    + @"\report.docx"
                },
                UserDesktop,
                CommonDesktop);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.False(task.IsCompleted);
        Assert.NotEqual(
            callerThread,
            workerThread);

        release.Set();
        await task.WaitAsync(
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Resolve_PreservesOrderAndSkipsCanonicalDuplicates()
    {
        var preflight =
            new DesktopDropPreflight(
                path => path,
                _ => true);
        string userItem =
            UserDesktop + @"\report.docx";
        string commonItem =
            CommonDesktop + @"\Browser.lnk";

        DesktopDropPreflightResult result =
            await preflight.ResolveAsync(
                new[]
                {
                    userItem,
                    userItem.ToUpperInvariant(),
                    commonItem
                },
                UserDesktop,
                CommonDesktop);

        Assert.Equal(
            2,
            result.Candidates.Count);
        Assert.Equal(
            userItem,
            result.Candidates[0].FullPath);
        Assert.Equal(
            DesktopDropLocation.UserDesktop,
            result.Candidates[0].Location);
        Assert.Equal(
            DesktopDropLocation.CommonDesktop,
            result.Candidates[1].Location);
        Assert.Equal(
            1,
            result.SkippedDuplicates);
    }

    [Fact]
    public async Task Resolve_CountsOutsideMissingAndInvalidSeparately()
    {
        var existing =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                UserDesktop
                + @"\valid.txt",
                @"C:\Users\Test\Downloads\outside.txt"
            };
        var preflight =
            new DesktopDropPreflight(
                path =>
                    path == "invalid"
                        ? throw new ArgumentException(
                            "bad path")
                        : path,
                existing.Contains);

        DesktopDropPreflightResult result =
            await preflight.ResolveAsync(
                new[]
                {
                    UserDesktop + @"\valid.txt",
                    @"C:\Users\Test\Downloads\outside.txt",
                    UserDesktop + @"\missing.txt",
                    "invalid",
                    ""
                },
                UserDesktop,
                CommonDesktop);

        Assert.Single(
            result.Candidates);
        Assert.Equal(
            1,
            result.OutsideDesktop);
        Assert.Equal(
            3,
            result.MissingOrInvalid);
        Assert.Equal(
            0,
            result.SkippedDuplicates);
    }

    [Fact]
    public async Task Resolve_ContainsPerItemExistenceFailure()
    {
        string failed =
            UserDesktop + @"\locked.txt";
        string valid =
            UserDesktop + @"\valid.txt";
        var preflight =
            new DesktopDropPreflight(
                path => path,
                path =>
                    path == failed
                        ? throw new UnauthorizedAccessException()
                        : true);

        DesktopDropPreflightResult result =
            await preflight.ResolveAsync(
                new[] { failed, valid },
                UserDesktop,
                CommonDesktop);

        Assert.Single(
            result.Candidates);
        Assert.Equal(
            valid,
            result.Candidates[0].FullPath);
        Assert.Equal(
            1,
            result.MissingOrInvalid);
    }
}
