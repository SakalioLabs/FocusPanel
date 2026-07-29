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
    public void Construction_DoesNotCreateRecoveryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");

        try
        {
            using var controller =
                new TaskbarController(
                    new FakeTaskbarNativeApi(),
                    new FakeWatchdogLauncher(),
                    sessionFile);

            Assert.False(
                Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

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
            Assert.True(
                native.TaskbarSurfaceSuppressed);
            Assert.Equal(1080, native.WorkArea.Bottom);
            Assert.Equal((uint)2, native.AppBarState);
            Assert.True(File.Exists(sessionFile));
            Assert.Contains(
                "\"UsesEmptyWindowRegion\":true",
                File.ReadAllText(sessionFile));

            controller.Restore();
            int workAreaWritesAfterFirstRestore = native.WorkAreaWriteCount;
            controller.Restore();

            Assert.False(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.False(
                native.TaskbarSurfaceSuppressed);
            Assert.Equal(1040, native.WorkArea.Bottom);
            Assert.Equal((uint)3, native.AppBarState);
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
    public void NativeRevealEdgeReleaseFailure_RollsBackInsteadOfEnteringPartialReplacement()
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
    public void SurfaceSuppressionFailure_RollsBackBeforeReplacementStarts()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native = new FakeTaskbarNativeApi
        {
            SurfaceSuppressionWritesSucceed =
                false
        };

        try
        {
            using var controller =
                new TaskbarController(
                    native,
                    new FakeWatchdogLauncher(),
                    sessionFile);

            Assert.False(
                controller.TryEnableReplacement(
                    out string? error));
            Assert.Contains(
                "显示与命中区域",
                error);
            Assert.False(
                controller.IsReplacementEnabled);
            Assert.False(
                native.TaskbarSurfaceSuppressed);
            Assert.True(native.Visible);
            Assert.Equal(
                1040,
                native.WorkArea.Bottom);
            Assert.False(
                File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    true);
            }
        }
    }

    [Fact]
    public void WorkAreaReleaseFailure_RestoresOriginalStateAndKeepsTaskbarVisible()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native = new FakeTaskbarNativeApi
        {
            SetWorkAreaSucceeds = false
        };

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);

            Assert.False(
                controller.TryEnableReplacement(
                    out string? error));
            Assert.False(
                string.IsNullOrWhiteSpace(
                    error));
            Assert.True(native.Visible);
            Assert.Equal(
                1040,
                native.WorkArea.Bottom);
            Assert.Equal(
                (uint)3,
                native.AppBarState);
            Assert.False(
                File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void WorkAreaChange_StopsReplacementWithoutRewriteLoop()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller = new TaskbarController(
                native,
                new FakeWatchdogLauncher(),
                sessionFile);
            TaskbarReplacementStoppedEvent? stopped = null;
            controller.ReplacementStopped +=
                value => stopped = value;

            Assert.True(
                controller.TryEnableReplacement(
                    out _));
            int writesAfterEnable =
                native.WorkAreaWriteCount;
            native.OverrideWorkArea(
                bottom: 1040);

            controller.RunGuardOnceForTests();
            Assert.True(
                controller.IsReplacementEnabled);
            Assert.Equal(
                writesAfterEnable,
                native.WorkAreaWriteCount);

            controller.RunGuardOnceForTests();
            Assert.False(
                controller.IsReplacementEnabled);
            Assert.NotNull(stopped);
            Assert.True(native.Visible);
            Assert.Equal(
                writesAfterEnable + 1,
                native.WorkAreaWriteCount);
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
            int surfaceWrites =
                native.SurfaceSuppressionWriteCount;

            controller.RunGuardOnceForTests();
            controller.RunGuardOnceForTests();

            Assert.Equal(workAreaWrites, native.WorkAreaWriteCount);
            Assert.Equal(visibilityWrites, native.TaskbarVisibilityWriteCount);
            Assert.Equal(appBarWrites, native.AppBarStateWriteCount);
            Assert.Equal(
                surfaceWrites,
                native.SurfaceSuppressionWriteCount);
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
    public void SurfaceRestoreFailure_KeepsSessionForWatchdogRetry()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

        try
        {
            using var controller =
                new TaskbarController(
                    native,
                    new FakeWatchdogLauncher(),
                    sessionFile);

            Assert.True(
                controller.TryEnableReplacement(
                    out _));
            native.SurfaceSuppressionWritesSucceed =
                false;

            controller.Restore();

            Assert.True(
                File.Exists(sessionFile));
            Assert.True(
                native.TaskbarSurfaceSuppressed);

            TaskbarController.RestoreSessionFile(
                sessionFile,
                native);

            Assert.False(
                native.TaskbarSurfaceSuppressed);
            Assert.False(
                File.Exists(sessionFile));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    true);
            }
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
            native.AppBarState = 3;
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
    public void VisibleTaskbarHost_WithEmptySurface_RemainsSafelySuppressed()
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
            Assert.Equal(1, native.HideWriteCount);

            native.Visible = true;
            controller.RunGuardOnceForTests();
            controller.RunGuardOnceForTests();

            Assert.True(controller.IsReplacementEnabled);
            Assert.True(native.Visible);
            Assert.Equal(1, native.HideWriteCount);
            Assert.True(
                native.TaskbarSurfaceSuppressed);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LostTaskbarSurfaceSuppression_StopsWithoutRewriteLoop()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel.Tests",
            Guid.NewGuid().ToString("N"));
        string sessionFile = Path.Combine(
            directory,
            "taskbar-session.json");
        var native = new FakeTaskbarNativeApi();

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
            int writes =
                native.SurfaceSuppressionWriteCount;
            native.TaskbarSurfaceSuppressed =
                false;

            controller.RunGuardOnceForTests();

            Assert.False(
                controller.IsReplacementEnabled);
            Assert.NotNull(stopped);
            Assert.Equal(
                TaskbarReplacementStopReason
                    .WindowsTaskbarReappeared,
                stopped.Reason);
            Assert.Contains(
                "显示与命中区域",
                stopped.Message);
            Assert.Equal(
                writes + 1,
                native.SurfaceSuppressionWriteCount);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    true);
            }
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
        public uint AppBarState { get; set; } = 3;
        public int WorkAreaWriteCount { get; private set; }
        public int TaskbarVisibilityWriteCount { get; private set; }
        public int HideWriteCount { get; private set; }
        public int AppBarStateWriteCount { get; private set; }
        public int AppBarStateReadCount { get; private set; }
        public bool TaskbarSurfaceSuppressed
        {
            get;
            set;
        }
        public int SurfaceSuppressionWriteCount
        {
            get;
            private set;
        }
        public bool SurfaceSuppressionWritesSucceed
        {
            get;
            set;
        } = true;
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
        public TaskbarController.NativeRect PrimaryBounds { get; } = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
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

        public bool IsTaskbarSurfaceSuppressed(
            IntPtr taskbar) =>
            TaskbarSurfaceSuppressed;

        public bool SetTaskbarSurfaceSuppressed(
            IntPtr taskbar,
            bool suppressed)
        {
            SurfaceSuppressionWriteCount++;
            if (!SurfaceSuppressionWritesSucceed)
            {
                SurfaceSuppressionWritesSucceed =
                    true;
                return false;
            }

            TaskbarSurfaceSuppressed =
                suppressed;
            return true;
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

        public bool TryGetPrimaryMonitorInfo(
            IntPtr taskbar,
            out TaskbarController.NativeRect workArea,
            out TaskbarController.NativeRect bounds)
        {
            workArea = WorkArea;
            bounds = PrimaryBounds;
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

        public void OverrideWorkArea(
            int bottom)
        {
            WorkArea =
                new TaskbarController.NativeRect
                {
                    Left = WorkArea.Left,
                    Top = WorkArea.Top,
                    Right = WorkArea.Right,
                    Bottom = bottom
                };
        }
    }
}
