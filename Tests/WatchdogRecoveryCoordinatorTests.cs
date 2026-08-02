using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WatchdogRecoveryCoordinatorTests
{
    [Fact]
    public void Restore_InvokesTaskbarAndDesktopRecovery()
    {
        string? restoredSession = null;
        int desktopRestoreCount = 0;

        WatchdogRecoveryCoordinator.Restore(
            "session.json",
            session => restoredSession = session,
            () =>
            {
                desktopRestoreCount++;
                return new DesktopCrashRecoveryResult(
                    true,
                    2,
                    0);
            });

        Assert.Equal(
            "session.json",
            restoredSession);
        Assert.Equal(1, desktopRestoreCount);
    }

    [Fact]
    public void Restore_TaskbarFailureStillRestoresDesktop()
    {
        int desktopRestoreCount = 0;

        WatchdogRecoveryCoordinator.Restore(
            "session.json",
            _ => throw new InvalidOperationException(
                "taskbar failure"),
            () =>
            {
                desktopRestoreCount++;
                return new DesktopCrashRecoveryResult(
                    true,
                    1,
                    0);
            });

        Assert.Equal(1, desktopRestoreCount);
    }

    [Fact]
    public void Restore_DesktopFailureDoesNotEscapeWatchdog()
    {
        bool taskbarRestored = false;

        Exception? error = Record.Exception(() =>
            WatchdogRecoveryCoordinator.Restore(
                "session.json",
                _ => taskbarRestored = true,
                () => throw new InvalidOperationException(
                    "desktop failure")));

        Assert.Null(error);
        Assert.True(taskbarRestored);
    }
}
