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
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.Equal((uint)3, native.AppBarState);
            Assert.True(File.Exists(sessionFile));

            controller.Restore();
            int workAreaWritesAfterFirstRestore = native.WorkAreaWriteCount;
            controller.Restore();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.Equal((uint)2, native.AppBarState);
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
    public void AutoHideFailure_RollsBackInsteadOfEnteringPartialReplacement()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi { SetAppBarStateSucceeds = false };

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
    public void VisibleTaskbarHideFailure_RollsBackAndReportsFailure()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi
        {
            HideSucceeds = false
        };

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
            int appBarWrites = native.AppBarStateWriteCount;

            controller.RunGuardOnceForTests();
            controller.RunGuardOnceForTests();

            Assert.Equal(workAreaWrites, native.WorkAreaWriteCount);
            Assert.Equal(visibilityWrites, native.TaskbarVisibilityWriteCount);
            Assert.Equal(appBarWrites, native.AppBarStateWriteCount);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RestoreVerificationFailure_KeepsSessionForWatchdogRetry()
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
            native.Visible = false;
            native.VisibilityWritesSucceed = false;

            controller.Restore();

            Assert.True(File.Exists(sessionFile));
            Assert.False(native.Visible);
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
            native.ThrowTimeoutOnAppBarRead = true;

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
            native.BlockNextAppBarRead = true;
            Task firstGuard = Task.Run(controller.RunGuardOnceForTests);
            Assert.True(native.AppBarReadEntered.Wait(TimeSpan.FromSeconds(2)));
            int readsWhileBlocked = native.AppBarStateReadCount;

            controller.RunGuardOnceForTests();

            Assert.Equal(readsWhileBlocked, native.AppBarStateReadCount);
            native.ReleaseAppBarRead.Set();
            await firstGuard;
            Assert.True(controller.IsReplacementEnabled);
        }
        finally
        {
            native.ReleaseAppBarRead.Set();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task GuardResultFromRestoredSession_IsDiscarded()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native =
            new FakeTaskbarNativeApi();

        try
        {
            using var controller =
                new TaskbarController(
                    native,
                    new FakeWatchdogLauncher(),
                    sessionFile);
            TaskbarReplacementStoppedEvent? stopped =
                null;
            controller.ReplacementStopped +=
                value => stopped = value;

            Assert.True(
                controller.TryEnableReplacement(
                    out _));
            native.BlockNextAppBarRead = true;
            Task guard =
                Task.Run(
                    controller.RunGuardOnceForTests);
            Assert.True(
                native.AppBarReadEntered.Wait(
                    TimeSpan.FromSeconds(2)));

            Task restore =
                Task.Run(controller.Restore);
            await Task.Delay(50);
            native.ReleaseAppBarRead.Set();
            await Task.WhenAll(
                guard,
                restore);

            Assert.False(
                controller.IsReplacementEnabled);
            Assert.Null(stopped);
            Assert.True(native.Visible);
        }
        finally
        {
            native.ReleaseAppBarRead.Set();
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    true);
            }
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
            TaskbarReplacementStoppedEvent? stopped = null;
            controller.ReplacementStopped += value => stopped = value;

            Assert.True(controller.TryEnableReplacement(out _));
            native.AppBarState = 2;
            native.SetAppBarStateSucceeds = false;

            controller.RunGuardOnceForTests();

            Assert.True(controller.IsReplacementEnabled);
            Assert.Null(stopped);

            controller.RunGuardOnceForTests();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.NotNull(stopped);
            Assert.False(string.IsNullOrWhiteSpace(stopped.Message));
            Assert.False(File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void NativeTaskbarReappearing_StopsReplacementWithoutRehideLoop()
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
            TaskbarReplacementStoppedEvent? stopped = null;
            controller.ReplacementStopped += value => stopped = value;

            Assert.True(controller.TryEnableReplacement(out _));
            Assert.Equal(1, native.HideWriteCount);

            native.Visible = true;
            controller.RunGuardOnceForTests();

            Assert.True(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1, native.HideWriteCount);
            Assert.Null(stopped);

            controller.RunGuardOnceForTests();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1, native.HideWriteCount);
            Assert.NotNull(stopped);
            Assert.Equal(TaskbarReplacementStopReason.WindowsTaskbarReappeared, stopped.Reason);
            Assert.Contains("重新显示", stopped.Message);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ExplorerHostChange_ReportsTypedReasonAndDoesNotRehide()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FocusPanel.Tests", Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(native, new FakeWatchdogLauncher(), sessionFile);
            TaskbarReplacementStoppedEvent? stopped = null;
            controller.ReplacementStopped += value => stopped = value;

            Assert.True(controller.TryEnableReplacement(out _));
            native.TaskbarHandle = new IntPtr(2);
            controller.RunGuardOnceForTests();

            Assert.True(controller.IsReplacementEnabled);
            Assert.Null(stopped);

            controller.RunGuardOnceForTests();

            Assert.NotNull(stopped);
            Assert.Equal(TaskbarReplacementStopReason.ExplorerHostChanged, stopped.Reason);
            Assert.Equal(1, native.HideWriteCount);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TransientTaskbarVisibility_DoesNotStopReplacement()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native =
            new FakeTaskbarNativeApi();

        try
        {
            using var controller =
                new TaskbarController(
                    native,
                    new FakeWatchdogLauncher(),
                    sessionFile);
            TaskbarReplacementStoppedEvent? stopped =
                null;
            controller.ReplacementStopped +=
                value => stopped = value;

            Assert.True(
                controller.TryEnableReplacement(
                    out _));
            int visibilityWrites =
                native.TaskbarVisibilityWriteCount;

            native.Visible = true;
            controller.RunGuardOnceForTests();
            native.Visible = false;
            controller.RunGuardOnceForTests();

            Assert.True(
                controller.IsReplacementEnabled);
            Assert.Null(stopped);
            Assert.Equal(
                visibilityWrites,
                native.TaskbarVisibilityWriteCount);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EmergencyDisableMarker_ReportsTypedReason()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FocusPanel.Tests", Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(directory, "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(native, new FakeWatchdogLauncher(), sessionFile);
            TaskbarReplacementStoppedEvent? stopped = null;
            controller.ReplacementStopped += value => stopped = value;

            Assert.True(controller.TryEnableReplacement(out _));
            File.WriteAllText(sessionFile + ".disabled", "disabled");
            controller.RunGuardOnceForTests();

            Assert.NotNull(stopped);
            Assert.Equal(TaskbarReplacementStopReason.EmergencyRestore, stopped.Reason);
            Assert.Equal(1, native.HideWriteCount);
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
        public IntPtr TaskbarHandle { get; set; } = new(1);
        public bool Visible { get; set; } = true;
        public uint AppBarState { get; set; } = 2;
        public int WorkAreaWriteCount { get; private set; }
        public int TaskbarVisibilityWriteCount { get; private set; }
        public int HideWriteCount { get; private set; }
        public int AppBarStateWriteCount { get; private set; }
        public int AppBarStateReadCount { get; private set; }
        public bool SetWorkAreaSucceeds { get; set; } = true;
        public bool SetAppBarStateSucceeds { get; set; } = true;
        public bool ShowSucceeds { get; set; } = true;
        public bool HideSucceeds { get; set; } = true;
        public bool VisibilityWritesSucceed { get; set; } = true;
        public bool ThrowTimeoutOnAppBarRead { get; set; }
        public bool BlockNextAppBarRead { get; set; }
        public ManualResetEventSlim AppBarReadEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseAppBarRead { get; } = new(false);
        public TaskbarController.NativeRect WorkArea { get; private set; } = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1040
        };

        public IntPtr FindPrimaryTaskbar() => TaskbarHandle;

        public bool IsWindowVisible(IntPtr taskbar) => Visible;

        public uint GetAppBarState(IntPtr taskbar)
        {
            AppBarStateReadCount++;
            if (ThrowTimeoutOnAppBarRead)
            {
                ThrowTimeoutOnAppBarRead = false;
                throw new TimeoutException("模拟任务栏状态锁超时。");
            }

            if (BlockNextAppBarRead)
            {
                BlockNextAppBarRead = false;
                AppBarReadEntered.Set();
                ReleaseAppBarRead.Wait(TimeSpan.FromSeconds(5));
            }

            return AppBarState;
        }

        public void SetAppBarState(IntPtr taskbar, uint state)
        {
            AppBarStateWriteCount++;
            if (SetAppBarStateSucceeds)
                AppBarState = state;
        }

        public bool SetTaskbarVisible(IntPtr taskbar, bool visible)
        {
            TaskbarVisibilityWriteCount++;
            if (!visible)
                HideWriteCount++;
            if (!VisibilityWritesSucceed)
                return false;
            if (visible && !ShowSucceeds)
                return false;
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
