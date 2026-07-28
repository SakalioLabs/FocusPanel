using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class RestoreRestartCoordinatorTests
{
    [Fact]
    public void WaitsForCurrentInstanceBeforeStartingRestore()
    {
        var boundary = new FakeRestoreRestartBoundary();

        int exitCode = RestoreRestartCoordinator.Run(
            parentProcessId: 42,
            executablePath: @"C:\FocusPanel.exe",
            boundary);

        Assert.Equal(0, exitCode);
        Assert.Equal(42, boundary.WaitedParentProcessId);
        Assert.Equal(
            @"C:\FocusPanel.exe",
            boundary.StartedExecutablePath);
        Assert.True(boundary.WaitCompletedBeforeStart);
    }

    [Fact]
    public void ParentTimeoutNeverStartsCompetingInstance()
    {
        var boundary = new FakeRestoreRestartBoundary
        {
            ParentExited = false
        };

        int exitCode = RestoreRestartCoordinator.Run(
            parentProcessId: 42,
            executablePath: @"C:\FocusPanel.exe",
            boundary);

        Assert.Equal(3, exitCode);
        Assert.Null(boundary.StartedExecutablePath);
    }

    [Theory]
    [InlineData(0, @"C:\FocusPanel.exe")]
    [InlineData(-1, @"C:\FocusPanel.exe")]
    [InlineData(42, null)]
    [InlineData(42, " ")]
    public void InvalidHandoffDoesNotStartRestore(
        int parentProcessId,
        string? executablePath)
    {
        var boundary = new FakeRestoreRestartBoundary();

        int exitCode = RestoreRestartCoordinator.Run(
            parentProcessId,
            executablePath,
            boundary);

        Assert.Equal(2, exitCode);
        Assert.Null(boundary.StartedExecutablePath);
    }

    [Fact]
    public void LaunchFailureReturnsNonZeroExitCode()
    {
        var boundary = new FakeRestoreRestartBoundary
        {
            StartSucceeded = false
        };

        int exitCode = RestoreRestartCoordinator.Run(
            parentProcessId: 42,
            executablePath: @"C:\FocusPanel.exe",
            boundary);

        Assert.Equal(4, exitCode);
    }

    private sealed class FakeRestoreRestartBoundary
        : IRestoreRestartBoundary
    {
        public bool ParentExited { get; set; } = true;
        public bool StartSucceeded { get; set; } = true;
        public int? WaitedParentProcessId { get; private set; }
        public string? StartedExecutablePath { get; private set; }
        public bool WaitCompletedBeforeStart { get; private set; }

        public bool WaitForParentExit(
            int parentProcessId,
            TimeSpan timeout)
        {
            WaitedParentProcessId = parentProcessId;
            Assert.Equal(
                TimeSpan.FromSeconds(30),
                timeout);
            WaitCompletedBeforeStart = ParentExited;
            return ParentExited;
        }

        public bool StartRestoreProcess(
            string executablePath)
        {
            Assert.True(WaitCompletedBeforeStart);
            StartedExecutablePath = executablePath;
            return StartSucceeded;
        }
    }
}
