using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace FocusPanel.Services;

public sealed class TaskbarController : ITaskbarController
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const uint SpiSetWorkArea = 0x002F;
    private const uint SpifSendChange = 0x0002;
    private const uint AbmGetState = 0x00000004;
    private const uint AbmSetState = 0x0000000A;
    private const uint AbsAutoHide = 0x00000001;
    private const string MutationMutexName = @"Local\FocusPanel.TaskbarMutation";
    private static readonly JsonSerializerOptions SessionJsonOptions = new()
    {
        IncludeFields = true
    };

    private readonly string _sessionFile;
    private readonly string _disabledFile;
    private readonly ITaskbarNativeApi _native;
    private readonly ITaskbarWatchdogLauncher _watchdogLauncher;
    private readonly TaskbarGuardConfirmation
        _guardConfirmation = new();
    private Timer? _guardTimer;
    private TaskbarSessionState? _state;
    private IntPtr _activeTaskbarHandle;
    private string? _lastApplyError;
    private TaskbarReplacementStopReason _lastStopReason = TaskbarReplacementStopReason.Unknown;
    private int _restoring;
    private int _guardRunning;
    private int _replacementGeneration;

    public event Action<TaskbarReplacementStoppedEvent>? ReplacementStopped;

    public TaskbarController()
        : this(
            new WindowsTaskbarNativeApi(),
            new WatchdogLauncher(),
            GetDefaultSessionFile())
    {
    }

    internal TaskbarController(
        ITaskbarNativeApi native,
        ITaskbarWatchdogLauncher watchdogLauncher,
        string sessionFile)
    {
        _native = native;
        _watchdogLauncher = watchdogLauncher;
        _sessionFile = sessionFile;
        _ = Path.GetDirectoryName(_sessionFile)
            ?? throw new ArgumentException(
                "恢复会话路径必须包含目录。",
                nameof(sessionFile));
        _disabledFile = _sessionFile + ".disabled";
    }

    public bool IsReplacementEnabled { get; private set; }

    public bool TryEnableReplacement(out string? error)
    {
        error = null;
        if (IsReplacementEnabled)
            return true;

        IntPtr taskbar = _native.FindPrimaryTaskbar();
        if (!TaskbarSafetyPolicy.TryValidatePrerequisites(
                taskbar != IntPtr.Zero,
                out error))
        {
            return false;
        }

        uint appBarState = _native.GetAppBarState(taskbar);
        _state = new TaskbarSessionState
        {
            TaskbarWasVisible = _native.IsWindowVisible(taskbar),
            OriginalAppBarState = appBarState,
            UsesNativeAutoHide = true,
            CreatedAt = DateTimeOffset.Now
        };

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    _sessionFile)!);
            File.Delete(_disabledFile);
            File.WriteAllText(_sessionFile, JsonSerializer.Serialize(_state, SessionJsonOptions));
        }
        catch (Exception ex)
        {
            error = $"无法创建恢复会话：{ex.Message}";
            return false;
        }

        if (!_watchdogLauncher.TryStart(Environment.ProcessId, _sessionFile, out error))
        {
            Restore();
            return false;
        }

        bool replacementApplied;
        try
        {
            replacementApplied = ApplyReplacement();
        }
        catch (Exception ex)
        {
            _lastApplyError = $"无法取得任务栏状态锁：{ex.Message}";
            replacementApplied = false;
        }

        if (!replacementApplied)
        {
            error = $"{_lastApplyError ?? "Windows 拒绝了任务栏替代操作"}，已立即恢复系统任务栏。";
            Restore();
            return false;
        }

        Interlocked.Increment(
            ref _replacementGeneration);
        IsReplacementEnabled = true;
        _guardConfirmation.ObserveValid();
        _guardTimer = new Timer(
            static state => ((TaskbarController)state!).GuardReplacementSafely(),
            this,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
        return true;
    }

    public void Restore()
    {
        if (Interlocked.Exchange(ref _restoring, 1) != 0)
            return;

        try
        {
            Interlocked.Increment(
                ref _replacementGeneration);
            _guardTimer?.Dispose();
            _guardTimer = null;
            if (_state != null || File.Exists(_sessionFile))
                RestoreSessionFile(_sessionFile, _native);
            IsReplacementEnabled = false;
            _state = null;
            _activeTaskbarHandle = IntPtr.Zero;
            _guardConfirmation.ObserveValid();
        }
        finally
        {
            Interlocked.Exchange(ref _restoring, 0);
        }
    }

    public static void RestoreOrphanedSession()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string sessionFile = Path.Combine(localData, "FocusPanel", "taskbar-session.json");
        if (File.Exists(sessionFile))
            RestoreSessionFile(sessionFile);
    }

    internal static void RestoreSessionFile(string sessionFile)
        => RestoreSessionFile(sessionFile, new WindowsTaskbarNativeApi());

    internal static void RestoreSessionFile(string sessionFile, ITaskbarNativeApi native)
    {
        IDisposable? mutation = null;
        try
        {
            mutation = AcquireMutationMutex();
        }
        catch
        {
            // A concurrent controller or watchdog owns the mutation. Keep the
            // session file so the watchdog can retry instead of crashing here.
            return;
        }

        using (mutation)
        {
            RestoreSessionFileCore(sessionFile, native);
        }
    }

    private static void RestoreSessionFileCore(string sessionFile, ITaskbarNativeApi native)
    {
        TaskbarSessionState? state = null;
        try
        {
            if (File.Exists(sessionFile))
                state = JsonSerializer.Deserialize<TaskbarSessionState>(
                    File.ReadAllText(sessionFile),
                    SessionJsonOptions);
        }
        catch
        {
            // A missing or damaged state file must not prevent the fail-safe from showing the taskbar.
        }

        bool workAreaRestored = true;
        IntPtr taskbar = native.FindPrimaryTaskbar();
        if (state != null && !state.UsesNativeAutoHide)
            workAreaRestored = native.SetWorkArea(state.OriginalWorkArea);

        bool visibilityRestored = taskbar != IntPtr.Zero;
        if (taskbar != IntPtr.Zero)
        {
            if (state != null)
                native.SetAppBarState(taskbar, state.OriginalAppBarState);

            bool shouldBeVisible = state == null || state.TaskbarWasVisible;
            visibilityRestored = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (native.SetTaskbarVisible(taskbar, shouldBeVisible)
                    && native.IsWindowVisible(taskbar) == shouldBeVisible)
                {
                    visibilityRestored = true;
                    break;
                }
            }
        }

        if (!workAreaRestored || !visibilityRestored)
            return;

        try
        {
            File.Delete(sessionFile);
            File.Delete(sessionFile + ".ready");
        }
        catch
        {
            // Recovery is successful even if cleanup is blocked by antivirus or another process.
        }
    }

    private bool ApplyReplacement()
    {
        using var mutation = AcquireMutationMutex();
        if (_state == null)
        {
            _lastApplyError = "任务栏恢复会话尚未建立";
            return false;
        }

        IntPtr taskbar = _native.FindPrimaryTaskbar();
        if (taskbar == IntPtr.Zero)
        {
            _lastApplyError = "没有找到主屏 Shell_TrayWnd";
            return false;
        }

        if (File.Exists(_disabledFile))
        {
            _lastApplyError = "本次替代会话已经被紧急停用";
            return false;
        }

        // Ask Explorer to release the work area through its documented AppBar
        // state first. Hide Shell_TrayWnd exactly once after that; the guard is
        // read-only and never fights Explorer with repeated visibility or
        // SPI_SETWORKAREA writes.
        uint desiredState = _state.OriginalAppBarState | AbsAutoHide;
        if (_native.GetAppBarState(taskbar) != desiredState)
            _native.SetAppBarState(taskbar, desiredState);

        if ((_native.GetAppBarState(taskbar) & AbsAutoHide) == 0)
        {
            _lastApplyError = "Windows 拒绝启用原生任务栏自动隐藏";
            return false;
        }

        if (_native.IsWindowVisible(taskbar)
            && (!_native.SetTaskbarVisible(taskbar, false)
                || _native.IsWindowVisible(taskbar)))
        {
            _lastApplyError = "Windows 拒绝隐藏原生任务栏窗口";
            return false;
        }

        _activeTaskbarHandle = taskbar;
        _lastApplyError = null;
        return true;
    }

    private static IDisposable AcquireMutationMutex()
    {
        var mutex = new Mutex(false, MutationMutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(3)))
                    throw new TimeoutException("等待任务栏状态锁超时。");
            }
            catch (AbandonedMutexException)
            {
                // The previous owner crashed; this process now owns the mutex.
            }

            return new MutexLease(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private sealed class MutexLease : IDisposable
    {
        private Mutex? _mutex;

        public MutexLease(Mutex mutex) => _mutex = mutex;

        public void Dispose()
        {
            Mutex? mutex = Interlocked.Exchange(ref _mutex, null);
            if (mutex == null)
                return;
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    private void GuardReplacementSafely()
    {
        if (!IsReplacementEnabled
            || Interlocked.CompareExchange(ref _guardRunning, 1, 0) != 0)
        {
            return;
        }

        int generation = Volatile.Read(
            ref _replacementGeneration);
        try
        {
            if (File.Exists(_disabledFile))
            {
                if (!IsCurrentReplacement(
                        generation))
                {
                    return;
                }

                StopReplacementFromGuard(
                    TaskbarReplacementStopReason.EmergencyRestore,
                    "任务栏替代模式已通过紧急快捷键关闭。");
                return;
            }

            bool valid;
            try
            {
                valid = ValidateReplacement();
            }
            catch (TimeoutException)
            {
                // Another recovery operation owns the cross-process lock.
                // Skipping one guard tick is safe; throwing here used to
                // terminate the entire application from the timer thread.
                return;
            }
            catch (Exception ex)
            {
                _lastApplyError = $"任务栏守护检测异常：{ex.Message}";
                _lastStopReason = TaskbarReplacementStopReason.Unknown;
                valid = false;
            }

            if (!IsCurrentReplacement(
                    generation))
            {
                return;
            }

            if (!valid)
            {
                if (!_guardConfirmation.ObserveInvalid(
                        _lastStopReason))
                {
                    return;
                }

                StopReplacementFromGuard(
                    _lastStopReason,
                    $"{_lastApplyError ?? "任务栏替代状态失效"}，已恢复 Windows 任务栏。");
            }
            else
            {
                _guardConfirmation.ObserveValid();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _guardRunning, 0);
        }
    }

    private bool IsCurrentReplacement(
        int generation) =>
        IsReplacementEnabled
        && generation == Volatile.Read(
            ref _replacementGeneration);

    private bool ValidateReplacement()
    {
        using var mutation = AcquireMutationMutex();
        IntPtr taskbar = _native.FindPrimaryTaskbar();
        if (taskbar == IntPtr.Zero
            || (_activeTaskbarHandle != IntPtr.Zero
                && taskbar != _activeTaskbarHandle))
        {
            _lastApplyError =
                taskbar == IntPtr.Zero
                    ? "Explorer 任务栏宿主暂时不可用"
                    : "Explorer 已重新创建任务栏宿主";
            _lastStopReason = TaskbarReplacementStopReason.ExplorerHostChanged;
            return false;
        }

        if (_native.IsWindowVisible(taskbar))
        {
            _lastApplyError = "Windows 已重新显示原生任务栏";
            _lastStopReason = TaskbarReplacementStopReason.WindowsTaskbarReappeared;
            return false;
        }

        if ((_native.GetAppBarState(taskbar) & AbsAutoHide) == 0)
        {
            _lastApplyError = "Windows 已取消任务栏自动隐藏状态";
            _lastStopReason = TaskbarReplacementStopReason.Unknown;
            return false;
        }

        _lastApplyError = null;
        _lastStopReason = TaskbarReplacementStopReason.Unknown;
        return true;
    }

    private void StopReplacementFromGuard(
        TaskbarReplacementStopReason reason,
        string error)
    {
        Restore();
        try
        {
            ReplacementStopped?.Invoke(new TaskbarReplacementStoppedEvent(reason, error));
        }
        catch
        {
            // A status subscriber must never turn the fail-safe path into
            // another unhandled timer-thread exception.
        }
    }

    internal void RunGuardOnceForTests() => GuardReplacementSafely();

    public void Dispose() => Restore();

    private static string GetDefaultSessionFile()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localData, "FocusPanel", "taskbar-session.json");
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rect;
        public IntPtr LParam;

        public static AppBarData Create(IntPtr hwnd) => new()
        {
            CbSize = (uint)Marshal.SizeOf<AppBarData>(),
            HWnd = hwnd
        };
    }

    private sealed class TaskbarSessionState
    {
        public bool TaskbarWasVisible { get; set; }
        public NativeRect OriginalWorkArea { get; set; }
        public uint OriginalAppBarState { get; set; }
        public NativeRect PrimaryBounds { get; set; }
        public bool UsesNativeAutoHide { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class WatchdogLauncher : ITaskbarWatchdogLauncher
    {
        public bool TryStart(int parentProcessId, string sessionFile, out string? error)
            => TaskbarWatchdog.TryStart(parentProcessId, sessionFile, out error);
    }

    private sealed class WindowsTaskbarNativeApi : ITaskbarNativeApi
    {
        public IntPtr FindPrimaryTaskbar() => NativeMethods.FindWindow("Shell_TrayWnd", null);

        public bool IsWindowVisible(IntPtr taskbar) => NativeMethods.IsWindowVisible(taskbar);

        public uint GetAppBarState(IntPtr taskbar)
        {
            var data = AppBarData.Create(taskbar);
            return (uint)NativeMethods.SHAppBarMessage(AbmGetState, ref data);
        }

        public void SetAppBarState(IntPtr taskbar, uint state)
        {
            var data = AppBarData.Create(taskbar);
            data.LParam = new IntPtr(state);
            NativeMethods.SHAppBarMessage(AbmSetState, ref data);
        }

        public bool SetTaskbarVisible(IntPtr taskbar, bool visible)
        {
            NativeMethods.ShowWindow(taskbar, visible ? SwShow : SwHide);
            NativeMethods.SetWindowPos(
                taskbar,
                visible ? new IntPtr(-1) : new IntPtr(1),
                0,
                0,
                0,
                0,
                SwpNoMove
                | SwpNoSize
                | SwpNoActivate
                | (visible ? SwpShowWindow : SwpHideWindow));
            return NativeMethods.IsWindowVisible(taskbar) == visible;
        }

        public bool SetWorkArea(NativeRect workArea)
            => NativeMethods.SetSystemParametersInfo(
                SpiSetWorkArea,
                0,
                ref workArea,
                SpifSendChange);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSystemParametersInfo(uint uiAction, uint uiParam, ref NativeRect pvParam, uint fWinIni);

        [DllImport("shell32.dll")]
        internal static extern UIntPtr SHAppBarMessage(uint dwMessage, ref AppBarData pData);
    }
}
