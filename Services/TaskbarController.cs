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
    private const uint SpiGetWorkArea = 0x0030;
    private const uint SpiSetWorkArea = 0x002F;
    private const uint SpifSendChange = 0x0002;
    private const uint AbmGetState = 0x00000004;
    private const uint AbmSetState = 0x0000000A;
    private static readonly JsonSerializerOptions SessionJsonOptions = new()
    {
        IncludeFields = true
    };

    private readonly string _sessionFile;
    private readonly string _disabledFile;
    private readonly ITaskbarNativeApi _native;
    private readonly ITaskbarWatchdogLauncher _watchdogLauncher;
    private Timer? _guardTimer;
    private TaskbarSessionState? _state;
    private int _restoring;

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
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionFile)
            ?? throw new ArgumentException("恢复会话路径必须包含目录。", nameof(sessionFile)));
        _disabledFile = _sessionFile + ".disabled";
    }

    public bool IsReplacementEnabled { get; private set; }

    public bool TryEnableReplacement(out string? error)
    {
        error = null;
        if (IsReplacementEnabled)
            return true;

        IntPtr taskbar = _native.FindPrimaryTaskbar();
        bool primaryScreenRead = _native.TryGetPrimaryBounds(out NativeRect primaryBounds);
        bool workAreaRead = _native.TryGetWorkArea(out NativeRect workArea);
        if (!TaskbarSafetyPolicy.TryValidatePrerequisites(
                taskbar != IntPtr.Zero,
                primaryScreenRead,
                workAreaRead,
                out error))
        {
            return false;
        }

        uint appBarState = _native.GetAppBarState(taskbar);
        _state = new TaskbarSessionState
        {
            TaskbarWasVisible = _native.IsWindowVisible(taskbar),
            OriginalWorkArea = workArea,
            OriginalAppBarState = appBarState,
            PrimaryBounds = primaryBounds,
            CreatedAt = DateTimeOffset.Now
        };

        try
        {
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

        if (!ApplyReplacement())
        {
            error = "Windows 工作区没有成功切换，已立即恢复系统任务栏。";
            Restore();
            return false;
        }

        IsReplacementEnabled = true;
        _guardTimer = new Timer(_ => GuardReplacement(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return true;
    }

    public void Restore()
    {
        if (Interlocked.Exchange(ref _restoring, 1) != 0)
            return;

        try
        {
            _guardTimer?.Dispose();
            _guardTimer = null;
            if (_state != null || File.Exists(_sessionFile))
                RestoreSessionFile(_sessionFile, _native);
            IsReplacementEnabled = false;
            _state = null;
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

        IntPtr taskbar = native.FindPrimaryTaskbar();
        if (taskbar != IntPtr.Zero)
        {
            native.SetTaskbarVisible(taskbar, state == null || state.TaskbarWasVisible);
            if (state != null)
                native.SetAppBarState(taskbar, state.OriginalAppBarState);
        }

        if (state != null)
            native.SetWorkArea(state.OriginalWorkArea);

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
        if (_state == null)
            return false;

        IntPtr taskbar = _native.FindPrimaryTaskbar();
        if (taskbar == IntPtr.Zero)
            return false;

        _native.SetTaskbarVisible(taskbar, false);
        return _native.SetWorkArea(_state.PrimaryBounds);
    }

    private void GuardReplacement()
    {
        if (!IsReplacementEnabled)
            return;

        if (File.Exists(_disabledFile))
        {
            Restore();
            return;
        }

        if (!ApplyReplacement())
            Restore();
    }

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

        public static NativeRect FromRectangle(System.Drawing.Rectangle rectangle) => new()
        {
            Left = rectangle.Left,
            Top = rectangle.Top,
            Right = rectangle.Right,
            Bottom = rectangle.Bottom
        };
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

        public bool TryGetPrimaryBounds(out NativeRect bounds)
        {
            System.Windows.Forms.Screen? primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary == null)
            {
                bounds = default;
                return false;
            }

            bounds = NativeRect.FromRectangle(primary.Bounds);
            return true;
        }

        public bool TryGetWorkArea(out NativeRect workArea)
            => NativeMethods.GetSystemParametersInfo(
                SpiGetWorkArea,
                0,
                out workArea,
                0);

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

        public void SetTaskbarVisible(IntPtr taskbar, bool visible)
            => NativeMethods.ShowWindow(taskbar, visible ? SwShow : SwHide);

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

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSystemParametersInfo(uint uiAction, uint uiParam, out NativeRect pvParam, uint fWinIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSystemParametersInfo(uint uiAction, uint uiParam, ref NativeRect pvParam, uint fWinIni);

        [DllImport("shell32.dll")]
        internal static extern UIntPtr SHAppBarMessage(uint dwMessage, ref AppBarData pData);
    }
}
