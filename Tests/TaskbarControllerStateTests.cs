using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarControllerStateTests
{
    [Fact]
    public void EnableAndRestore_UsesFakeBoundaryAndRestoresOriginalStateOnce()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();
        var watchdog = new FakeWatchdogLauncher();

        try
        {
            using var controller = new TaskbarController(native, watchdog, sessionFile);

            Assert.True(controller.TryEnableReplacement(out string? error));
            Assert.Null(error);
            Assert.True(controller.IsReplacementEnabled);
            Assert.False(native.Visible);
            Assert.Equal(1080, native.WorkArea.Bottom);
            Assert.True(File.Exists(sessionFile));

            controller.Restore();
            int workAreaWritesAfterFirstRestore = native.WorkAreaWriteCount;
            controller.Restore();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.Equal((uint)7, native.AppBarState);
            Assert.Equal(workAreaWritesAfterFirstRestore, native.WorkAreaWriteCount);
            Assert.False(File.Exists(sessionFile));
            Assert.Equal(1, watchdog.StartCount);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Restore_PreservesOriginallyHiddenTaskbar()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi { Visible = false };

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.True(controller.TryEnableReplacement(out _));
            controller.Restore();

            Assert.False(native.Visible);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void WorkAreaFailure_RollsBackInsteadOfEnteringPartialReplacement()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi { SetWorkAreaSucceeds = false };

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.False(controller.TryEnableReplacement(out string? error));
            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.False(File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TaskbarHideFailure_RollsBackAndReportsFailure()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi { HideSucceeds = false };

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.False(controller.TryEnableReplacement(out string? error));
            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.False(File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void StableReplacement_GuardDoesNotRewriteWorkAreaOrTaskbar()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.True(controller.TryEnableReplacement(out _));
            int workAreaWrites = native.WorkAreaWriteCount;
            int visibilityWrites = native.TaskbarVisibilityWriteCount;

            controller.RunGuardOnceForTests();
            controller.RunGuardOnceForTests();

            Assert.Equal(workAreaWrites, native.WorkAreaWriteCount);
            Assert.Equal(visibilityWrites, native.TaskbarVisibilityWriteCount);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void GuardLockTimeout_IsContainedAndDoesNotTerminateReplacement()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.True(controller.TryEnableReplacement(out _));
            native.ThrowTimeoutOnWorkAreaRead = true;

            Exception? exception = Record.Exception(controller.RunGuardOnceForTests);

            Assert.Null(exception);
            Assert.True(controller.IsReplacementEnabled);
            Assert.False(native.Visible);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ConcurrentGuardTick_IsSkippedInsteadOfOverlapping()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.True(controller.TryEnableReplacement(out _));
            native.BlockNextWorkAreaRead = true;
            Task firstGuard = Task.Run(controller.RunGuardOnceForTests);
            Assert.True(native.WorkAreaReadEntered.Wait(TimeSpan.FromSeconds(2)));
            int readsWhileBlocked = native.WorkAreaReadCount;

            controller.RunGuardOnceForTests();

            Assert.Equal(readsWhileBlocked, native.WorkAreaReadCount);
            native.ReleaseWorkAreaRead.Set();
            await firstGuard;
            Assert.True(controller.IsReplacementEnabled);
        }
        finally
        {
            native.ReleaseWorkAreaRead.Set();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void GuardFailure_RestoresTaskbarAndReportsReplacementStopped()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);
            string? stoppedReason = null;
            controller.ReplacementStopped += reason => stoppedReason = reason;

            Assert.True(controller.TryEnableReplacement(out _));
            native.Visible = true;
            native.HideSucceeds = false;

            controller.RunGuardOnceForTests();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.False(string.IsNullOrWhiteSpace(stoppedReason));
            Assert.False(File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private sealed class FakeWatchdogLauncher : ITaskbarWatchdogLauncher
    {
        public int StartCount { get; private set; }

        public bool TryStart(int parentProcessId, string sessionFile, out string? error)
        {
            StartCount++;
            error = null;
            return true;
        }
    }

    private sealed class FakeTaskbarNativeApi : ITaskbarNativeApi
    {
        public bool Visible { get; set; } = true;
        public uint AppBarState { get; private set; } = 7;
        public int WorkAreaWriteCount { get; private set; }
        public int TaskbarVisibilityWriteCount { get; private set; }
        public bool SetWorkAreaSucceeds { get; set; } = true;
        public bool HideSucceeds { get; set; } = true;
        public bool ThrowTimeoutOnWorkAreaRead { get; set; }
        public bool BlockNextWorkAreaRead { get; set; }
        public int WorkAreaReadCount { get; private set; }
        public ManualResetEventSlim WorkAreaReadEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseWorkAreaRead { get; } = new(false);
        public TaskbarController.NativeRect WorkArea { get; private set; } = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1040
        };

        public IntPtr FindPrimaryTaskbar() => new(1);

        public bool IsWindowVisible(IntPtr taskbar) => Visible;

        public bool TryGetPrimaryBounds(out TaskbarController.NativeRect bounds)
        {
            bounds = new TaskbarController.NativeRect
            {
                Left = 0,
                Top = 0,
                Right = 1920,
                Bottom = 1080
            };
            return true;
        }

        public bool TryGetWorkArea(out TaskbarController.NativeRect workArea)
        {
            WorkAreaReadCount++;
            if (ThrowTimeoutOnWorkAreaRead)
            {
                ThrowTimeoutOnWorkAreaRead = false;
                throw new TimeoutException("模拟任务栏状态锁超时。");
            }

            if (BlockNextWorkAreaRead)
            {
                BlockNextWorkAreaRead = false;
                WorkAreaReadEntered.Set();
                ReleaseWorkAreaRead.Wait(TimeSpan.FromSeconds(5));
            }

            workArea = WorkArea;
            return true;
        }

        public uint GetAppBarState(IntPtr taskbar) => AppBarState;

        public void SetAppBarState(IntPtr taskbar, uint state) => AppBarState = state;

        public bool SetTaskbarVisible(IntPtr taskbar, bool visible)
        {
            TaskbarVisibilityWriteCount++;
            if (!visible && !HideSucceeds)
                return false;
            Visible = visible;
            return true;
        }

        public bool SetWorkArea(TaskbarController.NativeRect workArea)
        {
            if (!SetWorkAreaSucceeds)
            {
                SetWorkAreaSucceeds = true;
                return false;
            }

            WorkArea = workArea;
            WorkAreaWriteCount++;
            return true;
        }
    }
}
