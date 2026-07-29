using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppFileLaunchServiceTests
{
    [Fact]
    public void SelectPaths_DeduplicatesAndBoundsInput()
    {
        string[] paths =
            Enumerable.Range(0, 35)
                .Select(
                    index =>
                        $@"C:\Docs\{index}.txt")
                .Concat(
                    new[]
                    {
                        @"c:\docs\0.txt",
                        "relative.txt",
                        " "
                    })
                .ToArray();

        AppFileDropPathSelection selection =
            AppFileDropPolicy.SelectPaths(
                paths);

        Assert.Equal(
            paths.Length,
            selection.RequestedCount);
        Assert.Equal(
            AppFileDropPolicy.MaximumPathCount,
            selection.Paths.Count);
        Assert.Equal(
            5,
            selection.IgnoredCount);
        Assert.Equal(
            @"C:\Docs\0.txt",
            selection.Paths[0]);
    }

    [Fact]
    public void DesktopRequest_PreservesArgumentsAndQuotesPaths()
    {
        bool built =
            AppFileDropPolicy
                .TryBuildDesktopRequest(
                    DesktopApp(
                        arguments:
                            "--reuse-window"),
                    new[]
                    {
                        @"C:\My Docs\one.txt",
                        "C:\\Folders\\"
                    },
                    out ProcessStartInfo?
                        request);

        Assert.True(built);
        Assert.NotNull(request);
        Assert.Equal(
            @"C:\Apps\Editor.exe",
            request!.FileName);
        Assert.Equal(
            "--reuse-window "
            + "\"C:\\My Docs\\one.txt\" "
            + "\"C:\\Folders\\\\\"",
            request.Arguments);
        Assert.True(
            request.UseShellExecute);
    }

    [Fact]
    public void QuoteWindowsArgument_EscapesEmbeddedQuote()
    {
        Assert.Equal(
            "\"alpha\\\"beta\"",
            AppFileDropPolicy
                .QuoteWindowsArgument(
                    "alpha\"beta"));
    }

    [Fact]
    public async Task DesktopApp_StartsOnceWithAllFiles()
    {
        var native = new FakeNative();
        var service =
            new AppFileLaunchService(
                native);

        AppFileLaunchResult result =
            await service.OpenAsync(
                DesktopApp(),
                new[]
                {
                    @"C:\Docs\a.txt",
                    @"C:\Docs\b.txt"
                });

        Assert.True(
            result.IsCompleteSuccess);
        Assert.Equal(
            2,
            result.OpenedCount);
        Assert.Equal(
            1,
            native.StartCalls);
        Assert.Equal(
            0,
            native.PackagedCalls);
        Assert.Contains(
            "\"C:\\Docs\\a.txt\"",
            native.LastRequest!
                .Arguments);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task PackagedApp_UsesFileActivationContract()
    {
        var native = new FakeNative();
        var service =
            new AppFileLaunchService(
                native);

        AppFileLaunchResult result =
            await service.OpenAsync(
                new AppLaunchItem
                {
                    DisplayName = "照片",
                    LaunchKind =
                        AppLaunchKind.ShellApp,
                    LaunchTarget =
                        "Contoso.Photos_123!App"
                },
                new[]
                {
                    @"C:\Images\a.png",
                    @"C:\Images\b.png"
                });

        Assert.True(
            result.IsCompleteSuccess);
        Assert.Equal(
            1,
            native.PackagedCalls);
        Assert.Equal(
            "Contoso.Photos_123!App",
            native.LastApplicationUserModelId);
        Assert.Equal(
            2,
            native.LastPaths.Count);
        Assert.Equal(
            0,
            native.StartCalls);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task MissingItems_AreReportedWithoutBlockingValidOnes()
    {
        var native =
            new FakeNative
            {
                Exists = path =>
                    path.EndsWith(
                        "available.txt",
                        StringComparison
                            .OrdinalIgnoreCase)
            };
        var service =
            new AppFileLaunchService(
                native);

        AppFileLaunchResult result =
            await service.OpenAsync(
                DesktopApp(),
                new[]
                {
                    @"C:\Docs\available.txt",
                    @"C:\Docs\missing.txt"
                });

        Assert.True(
            result.LaunchSucceeded);
        Assert.False(
            result.IsCompleteSuccess);
        Assert.Equal(
            1,
            result.OpenedCount);
        Assert.Equal(
            1,
            result.IgnoredCount);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task EmptyOrUnavailableDrop_DoesNotLaunch()
    {
        var native =
            new FakeNative
            {
                Exists = _ => false
            };
        var service =
            new AppFileLaunchService(
                native);

        AppFileLaunchResult result =
            await service.OpenAsync(
                DesktopApp(),
                new[]
                {
                    @"C:\Docs\missing.txt"
                });

        Assert.False(
            result.LaunchSucceeded);
        Assert.Contains(
            "移动",
            result.FailureReason);
        Assert.Equal(
            0,
            native.StartCalls);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task NativeException_BecomesSafeFailure()
    {
        var native =
            new FakeNative
            {
                Start = _ =>
                    throw new
                        InvalidOperationException(
                            "boom")
            };
        var service =
            new AppFileLaunchService(
                native);

        AppFileLaunchResult result =
            await service.OpenAsync(
                DesktopApp(),
                new[]
                {
                    @"C:\Docs\a.txt"
                });

        Assert.False(
            result.LaunchSucceeded);
        Assert.Contains(
            "拒绝",
            result.FailureReason);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task Open_RunsOffCallingThread()
    {
        int callerThread =
            Environment
                .CurrentManagedThreadId;
        int nativeThread = callerThread;
        var native =
            new FakeNative
            {
                Start = _ =>
                {
                    nativeThread =
                        Environment
                            .CurrentManagedThreadId;
                    return true;
                }
            };
        var service =
            new AppFileLaunchService(
                native);

        await service.OpenAsync(
            DesktopApp(),
            new[]
            {
                @"C:\Docs\a.txt"
            });

        Assert.NotEqual(
            callerThread,
            nativeThread);
        await service.CompleteAsync();
    }

    [Fact]
    public async Task Complete_WaitsForInFlightAndRejectsNewDrop()
    {
        using var entered =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        var native =
            new FakeNative
            {
                Start = _ =>
                {
                    entered.Set();
                    release.Wait(
                        TimeSpan
                            .FromSeconds(5));
                    return true;
                }
            };
        var service =
            new AppFileLaunchService(
                native);
        Task<AppFileLaunchResult>
            inFlight =
                service.OpenAsync(
                    DesktopApp(),
                    new[]
                    {
                        @"C:\Docs\a.txt"
                    });
        Assert.True(
            entered.Wait(
                TimeSpan.FromSeconds(3)));

        Task completion =
            service.CompleteAsync();
        Assert.False(
            completion.IsCompleted);
        AppFileLaunchResult rejected =
            await service.OpenAsync(
                DesktopApp(),
                new[]
                {
                    @"C:\Docs\b.txt"
                });
        Assert.Contains(
            "退出",
            rejected.FailureReason);

        release.Set();
        Assert.True(
            (await inFlight)
                .LaunchSucceeded);
        await completion;
        Assert.Equal(
            1,
            native.StartCalls);
    }

    private static AppLaunchItem
        DesktopApp(
            string? arguments = null) =>
        new()
        {
            DisplayName = "编辑器",
            LaunchKind =
                AppLaunchKind.Executable,
            LaunchTarget =
                @"C:\Apps\Editor.exe",
            Arguments = arguments
        };

    private sealed class FakeNative :
        IAppFileLaunchNative
    {
        internal Func<string, bool>
            Exists
        {
            get;
            init;
        } = _ => true;

        internal Func<
            ProcessStartInfo,
            bool> Start
        {
            get;
            init;
        } = _ => true;

        internal int StartCalls
        {
            get;
            private set;
        }

        internal int PackagedCalls
        {
            get;
            private set;
        }

        internal ProcessStartInfo?
            LastRequest
        {
            get;
            private set;
        }

        internal string?
            LastApplicationUserModelId
        {
            get;
            private set;
        }

        internal IReadOnlyList<string>
            LastPaths
        {
            get;
            private set;
        } = Array.Empty<string>();

        public bool PathExists(
            string path) =>
            Exists(path);

        public bool TryStart(
            ProcessStartInfo request)
        {
            StartCalls++;
            LastRequest = request;
            return Start(request);
        }

        public bool TryActivatePackaged(
            string applicationUserModelId,
            IReadOnlyList<string> paths)
        {
            PackagedCalls++;
            LastApplicationUserModelId =
                applicationUserModelId;
            LastPaths =
                paths.ToArray();
            return true;
        }
    }
}
