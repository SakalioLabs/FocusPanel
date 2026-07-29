using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DatabaseStartupCoordinatorTests
{
    [Fact]
    public async Task PrepareAsync_ReturnsImmediatelyAndRunsOnWorker()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int callerThread =
            Environment.CurrentManagedThreadId;
        int workerThread = callerThread;
        bool? receivedRestore = null;
        var coordinator =
            new DatabaseStartupCoordinator(
                restoreRequested =>
                {
                    workerThread =
                        Environment
                            .CurrentManagedThreadId;
                    receivedRestore =
                        restoreRequested;
                    started.Set();
                    release.Wait();
                    return new DatabaseStartupCompletion(
                        true,
                        null);
                });

        var stopwatch = Stopwatch.StartNew();
        Task<DatabaseStartupCompletion> work =
            coordinator.PrepareAsync(
                restoreRequested: true);
        stopwatch.Stop();

        try
        {
            Assert.True(stopwatch.Elapsed
                < TimeSpan.FromSeconds(1));
            Assert.True(started.Wait(
                TimeSpan.FromSeconds(2)));
            Assert.False(work.IsCompleted);
            Assert.NotEqual(
                callerThread,
                workerThread);
            Assert.True(receivedRestore);
        }
        finally
        {
            release.Set();
        }

        DatabaseStartupCompletion completion =
            await work.WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.True(completion.Succeeded);
        Assert.Null(completion.Notice);
    }

    [Fact]
    public async Task PrepareAsync_PropagatesBoundaryFailure()
    {
        var coordinator =
            new DatabaseStartupCoordinator(
                _ => throw new IOException(
                    "disk unavailable"));

        IOException error =
            await Assert.ThrowsAsync<IOException>(
                () => coordinator.PrepareAsync(
                    restoreRequested: false));

        Assert.Equal(
            "disk unavailable",
            error.Message);
    }

    [Fact]
    public void AppStartup_AwaitsDatabaseBeforeCreatingShell()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "App.xaml.cs"));
        string coordinator = File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "DatabaseStartupCoordinator.cs"));

        int awaitDatabase = app.IndexOf(
            "await _databaseStartup.PrepareAsync(",
            StringComparison.Ordinal);
        int createShell = app.IndexOf(
            "var mainWindow = new MainWindow();",
            StringComparison.Ordinal);

        Assert.Contains(
            "protected override async void OnStartup(",
            app);
        Assert.True(awaitDatabase >= 0);
        Assert.True(createShell > awaitDatabase);
        Assert.Contains(
            "Task.Run(",
            coordinator);
        Assert.Contains(
            "TaskbarController.RestoreOrphanedSession();",
            app[..awaitDatabase]);
    }

    private static string FindRepositoryRoot()
    {
        string? current =
            AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(
                    Path.Combine(
                        current,
                        "FocusPanel.csproj")))
            {
                return current;
            }

            current =
                Directory.GetParent(current)
                    ?.FullName;
        }

        throw new DirectoryNotFoundException(
            "FocusPanel repository root not found.");
    }
}
