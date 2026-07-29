using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.Views;

namespace FocusPanel.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const string FirstRunAcceptedKey = "Shell.FirstRunAccepted";
    private const string ReplacementEnabledKey = "Shell.ReplacementEnabled";
    private const string ThemeModeKey = "Shell.Theme";
    private const string FullscreenHotZoneKey = "Shell.DisableHotZoneInFullscreen";
    private static readonly CultureInfo ChineseCulture =
        CultureInfo.GetCultureInfo("zh-CN");

    private readonly IAppCatalogService _appCatalog;
    private readonly IWindowTracker _windowTracker;
    private readonly ISystemStatusService _systemStatus;
    private readonly IAppUpdateService _updateService;
    private readonly IDesktopItemVisibilityService _desktopVisibility;
    private readonly TaskbarAppComposer _taskbarComposer = new();
    private readonly TaskSummaryReader _taskSummaryReader = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _systemStatusTimer;
    private readonly DispatcherTimer _taskSummaryTimer;
    private readonly DispatcherTimer _updateCheckTimer;
    private readonly Dispatcher _uiDispatcher;
    private readonly CoalescingBackgroundRefresh<
        PendingSystemStatusSnapshot> _systemStatusRefresh;
    private readonly CoalescingBackgroundRefresh<
        TaskSummarySnapshot> _taskSummaryRefresh;
    private DashboardViewModel? _dashboardViewModel;
    private TasksViewModel? _tasksViewModel;
    private PomodoroViewModel? _pomodoroViewModel;
    private FileOrganizerViewModel? _fileOrganizerViewModel;
    private OkrViewModel? _okrViewModel;
    private AIAssistantViewModel? _aiAssistantViewModel;
    private bool _updatingAudioState;
    private long _audioStateRevision;
    private long _taskSummaryMonthTicks;
    private float _confirmedMasterVolume;
    private bool _confirmedMuted;
    private bool _updatingStartupState;
    private string? _lastNotifiedUpdateVersion;
    private bool _isShellVisible;
    private bool _isDisposed;
    private DateTime _calendarFocusMonth;
    private IReadOnlyDictionary<DateTime, CalendarFocusSummary>
        _calendarFocusByDate =
            new Dictionary<DateTime, CalendarFocusSummary>();

    [ObservableProperty]
    private string title = "FocusPanel";

    [ObservableProperty]
    private object currentViewModel;

    [ObservableProperty]
    private string currentSectionTitle = "桌面收纳";

    [ObservableProperty]
    private DateTime currentTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(DisplayedCalendarMonthTitle))]
    private DateTime displayedCalendarMonth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(SelectedCalendarDateTitle))]
    private DateTime selectedCalendarDate;

    [ObservableProperty]
    private string selectedDayFocusSummary =
        "该日没有完成的专注记录";

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    [NotifyPropertyChangedFor(
        nameof(IsAppSearchStatusVisible))]
    private bool isAppCatalogLoading;

    [ObservableProperty]
    private bool isSearchOpen;

    [ObservableProperty]
    private AppLaunchItem? selectedSearchResult;

    [ObservableProperty]
    private bool isCalendarOpen;

    [ObservableProperty]
    private bool isFocusCenterOpen;

    [ObservableProperty]
    private bool isStatusCenterOpen;

    [ObservableProperty]
    private bool isSettingsOpen;

    [ObservableProperty]
    private bool isPowerMenuOpen;

    [ObservableProperty]
    private bool isOnboardingVisible;

    [ObservableProperty]
    private bool isReplacementEnabled;

    [ObservableProperty]
    private string replacementStatus = "Windows 任务栏保持显示";

    [ObservableProperty]
    private string replacementError = string.Empty;

    [ObservableProperty]
    private TaskbarReplacementStopReason? replacementStopReason;

    [ObservableProperty]
    private bool hasReplacementWarning;

    [ObservableProperty]
    private string lastWorkspace = "Files";

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private string startupStatus = string.Empty;

    [ObservableProperty]
    private string themeMode = "System";

    [ObservableProperty]
    private bool disableHotZoneInFullscreen = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private float masterVolume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isAudioAvailable;

    [ObservableProperty]
    private string audioStatusText =
        "正在读取音频设备…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkGlyph))]
    [NotifyPropertyChangedFor(nameof(NetworkSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isNetworkAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkGlyph))]
    [NotifyPropertyChangedFor(nameof(NetworkSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private string networkDisplayName = "未连接";

    [ObservableProperty]
    private string networkDetail = "当前没有可用连接";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkGlyph))]
    private NetworkConnectionKind networkConnectionKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputLanguageDisplay))]
    [NotifyPropertyChangedFor(nameof(InputMethodDisplay))]
    [NotifyPropertyChangedFor(nameof(InputSwitcherLabel))]
    [NotifyPropertyChangedFor(nameof(InputSwitcherSummary))]
    private InputMethodStatusSnapshot inputMethodStatus =
        InputMethodStatusSnapshot.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryGlyph))]
    [NotifyPropertyChangedFor(nameof(BatteryValueText))]
    [NotifyPropertyChangedFor(nameof(BatterySummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool hasBattery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryGlyph))]
    [NotifyPropertyChangedFor(nameof(BatteryValueText))]
    [NotifyPropertyChangedFor(nameof(BatterySummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private int batteryPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryGlyph))]
    [NotifyPropertyChangedFor(nameof(BatteryValueText))]
    [NotifyPropertyChangedFor(nameof(BatterySummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isCharging;

    [ObservableProperty]
    private string systemActionMessage = string.Empty;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private string currentAppVersion = "0.0.0";

    [ObservableProperty]
    private string updateStatus = "尚未检查更新";

    [ObservableProperty]
    private int updateProgress;

    [ObservableProperty]
    private bool isUpdateBusy;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private string availableUpdateVersion = string.Empty;

    [ObservableProperty]
    private bool showsProtectedSystemFiles;

    public MainViewModel(
        IAppCatalogService appCatalog,
        IWindowTracker windowTracker,
        ISystemStatusService systemStatus,
        IAppUpdateService updateService)
    {
        _appCatalog = appCatalog;
        _windowTracker = windowTracker;
        _systemStatus = systemStatus;
        _updateService = updateService;
        _desktopVisibility = new WindowsDesktopItemVisibilityService();
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _systemStatusRefresh =
            new CoalescingBackgroundRefresh<
                PendingSystemStatusSnapshot>(
                CaptureSystemStatus,
                ApplySystemStatusAsync,
                ex => Debug.WriteLine(
                    $"系统状态刷新失败：{ex}"));
        _taskSummaryRefresh =
            new CoalescingBackgroundRefresh<
                TaskSummarySnapshot>(
                CaptureTaskSummary,
                ApplyTaskSummaryAsync,
                ex => Debug.WriteLine(
                    $"任务摘要刷新失败：{ex}"));
        IsAppCatalogLoading = _appCatalog.IsIndexing;

        CurrentTime = DateTime.Now;
        DisplayedCalendarMonth = new DateTime(
            CurrentTime.Year,
            CurrentTime.Month,
            1);
        SelectedCalendarDate = CurrentTime.Date;
        CurrentAppVersion = _updateService.CurrentVersion;
        UpdateStatus = _updateService.CanUpdate
            ? "将自动从 GitHub Releases 检查更新"
            : "当前为开发运行版；安装发布包后可一键更新";
        StartWithWindows = AutoStartupService.IsStartupEnabled();
        StartupStatus = StartWithWindows
            ? "已设置为随 Windows 启动"
            : "当前不会随 Windows 启动";
        bool firstRunAccepted = ReadBooleanConfig(FirstRunAcceptedKey);
        IsReplacementEnabled = ReadBooleanConfig(ReplacementEnabledKey);
        ReplacementStatus = IsReplacementEnabled
            ? "侧边任务栏运行中 · Windows 任务栏已完整隐藏"
            : "替代模式未启用，Windows 任务栏保持原设置";
        ThemeMode = ReadStringConfig(ThemeModeKey, "System");
        DisableHotZoneInFullscreen = ReadBooleanConfig(FullscreenHotZoneKey, true);
        ShowsProtectedSystemFiles = _desktopVisibility.ShowsProtectedSystemFiles;
        ThemeService.SetMode(ThemeMode);
        // Replacement is the product's primary mode. Never hide the taskbar without
        // a click, but keep the activation screen discoverable until it is enabled.
        IsOnboardingVisible = !firstRunAccepted || !IsReplacementEnabled;

        _fileOrganizerViewModel = new FileOrganizerViewModel();
        CurrentViewModel = _fileOrganizerViewModel;

        RefreshTaskbarApps();
        RefreshSearchResults();
        RequestSystemStatusRefresh();
        RequestTaskSummaryRefresh();

        _windowTracker.SnapshotChanged += OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged += OnCatalogChanged;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now;

        _systemStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _systemStatusTimer.Tick +=
            (_, _) => RequestSystemStatusRefresh();

        _taskSummaryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _taskSummaryTimer.Tick +=
            (_, _) => RequestTaskSummaryRefresh();

        _updateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateCheckTimer.Tick += async (_, _) => await CheckForUpdatesInBackgroundAsync();
        _updateCheckTimer.Start();
    }

    public ObservableCollection<AppLaunchItem> SearchResults { get; } = new();
    public ObservableCollection<TaskbarAppItem> TaskbarApps { get; } = new();
    public string AppSearchStatusText =>
        IsAppCatalogLoading
            ? "正在载入应用目录…"
            : "没有找到匹配的应用";
    public bool IsAppSearchStatusVisible =>
        SearchResults.Count == 0;
    public string AudioGlyph =>
        GetAudioPresentation().Glyph;
    public string AudioSummary =>
        GetAudioPresentation().Summary;
    public string AudioToggleLabel =>
        GetAudioPresentation().ToggleLabel;
    public string NetworkGlyph =>
        GetNetworkPresentation().Glyph;
    public string NetworkSummary =>
        GetNetworkPresentation().Summary;
    public string BatteryGlyph =>
        GetBatteryPresentation().Glyph;
    public string BatteryValueText =>
        GetBatteryPresentation().ValueText;
    public string BatterySummary =>
        GetBatteryPresentation().Summary;
    public string StatusCenterSummary =>
        SystemStatusSummaryComposer.Compose(
            NetworkSummary,
            AudioSummary,
            BatterySummary);
    public string StatusCenterAutomationName =>
        $"状态中心，{StatusCenterSummary}";
    public string InputLanguageDisplay =>
        InputMethodStatus.LanguageDisplay;
    public string InputMethodDisplay =>
        InputMethodStatus.MethodDisplay;
    public string InputSwitcherLabel =>
        InputMethodStatus.ButtonLabel;
    public string InputSwitcherSummary =>
        InputMethodStatus.Summary;
    public ObservableCollection<CalendarDayItem> CalendarDays
    {
        get;
    } = new();

    public string DisplayedCalendarMonthTitle =>
        DisplayedCalendarMonth.ToString("yyyy年 M月");

    public string SelectedCalendarDateTitle =>
        SelectedCalendarDate.ToString(
            "M月d日 dddd",
            ChineseCulture);

    public void SetShellVisible(bool isVisible)
    {
        bool becameVisible =
            ShellRefreshActivityPolicy.BecameVisible(
                _isShellVisible,
                isVisible);
        _isShellVisible = isVisible;
        _windowTracker.SetTrackingActive(isVisible);
        if (becameVisible)
        {
            CurrentTime = DateTime.Now;
            RequestSystemStatusRefresh();
        }
        UpdateRefreshActivity();
    }

    public event Action? RequestClose;
    public event Action? RequestEnableReplacement;
    public event Action? RequestDisableReplacement;
    public event Action? RequestApplyUpdate;
    public event Action<AppUpdateInfo>? UpdateAvailable;
    public event Action<string>? WorkspaceRequested;
    public event Action<int>? PomodoroCompleted;

    partial void OnSearchQueryChanged(string value)
    {
        IsSearchOpen = true;
        RefreshSearchResults();
    }

    partial void OnIsCalendarOpenChanged(bool value)
    {
        if (value && _isShellVisible)
            RequestTaskSummaryRefresh();
        UpdateRefreshActivity();
    }

    partial void OnIsStatusCenterOpenChanged(bool value)
    {
        if (value && _isShellVisible)
            RequestSystemStatusRefresh();
        UpdateRefreshActivity();
    }

    partial void OnMasterVolumeChanged(float value)
    {
        if (_updatingAudioState)
            return;

        TryApplyMasterVolume(value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_updatingAudioState)
            return;

        TryApplyMuted(value);
    }

    private bool TryApplyMasterVolume(float value)
    {
        Interlocked.Increment(
            ref _audioStateRevision);
        AudioControlResult<float> result =
            AudioControlPolicy.Apply(
                Math.Clamp(value, 0f, 1f),
                _confirmedMasterVolume,
                _systemStatus.TrySetMasterVolume);
        if (result.Succeeded)
        {
            _confirmedMasterVolume = result.EffectiveValue;
            IsAudioAvailable = true;
            AudioStatusText = string.Empty;
            SystemActionMessage = string.Empty;
            RestoreConfirmedAudioState(
                _confirmedMasterVolume,
                _confirmedMuted);
            return true;
        }

        RestoreConfirmedAudioState(
            result.EffectiveValue,
            _confirmedMuted);
        ReportAudioFailure(
            "无法调整音量。请检查默认音频输出设备，或使用 Win+A。");
        return false;
    }

    private bool TryApplyMuted(bool value)
    {
        Interlocked.Increment(
            ref _audioStateRevision);
        AudioControlResult<bool> result =
            AudioControlPolicy.Apply(
                value,
                _confirmedMuted,
                _systemStatus.TrySetMuted);
        if (result.Succeeded)
        {
            _confirmedMuted = result.EffectiveValue;
            IsAudioAvailable = true;
            AudioStatusText = string.Empty;
            SystemActionMessage = string.Empty;
            RestoreConfirmedAudioState(
                _confirmedMasterVolume,
                _confirmedMuted);
            return true;
        }

        RestoreConfirmedAudioState(
            _confirmedMasterVolume,
            result.EffectiveValue);
        ReportAudioFailure(
            "无法切换静音。请检查默认音频输出设备，或使用 Win+A。");
        return false;
    }

    partial void OnThemeModeChanged(string value)
    {
        string normalized = value is "Light" or "Dark" ? value : "System";
        if (normalized != value)
        {
            ThemeMode = normalized;
            return;
        }

        ThemeService.SetMode(normalized);
        SaveStringConfig(ThemeModeKey, normalized);
    }

    partial void OnDisableHotZoneInFullscreenChanged(bool value)
    {
        SaveBooleanConfig(FullscreenHotZoneKey, value);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_updatingStartupState)
            return;

        if (!IsReplacementEnabled)
        {
            StartupStatus = value
                ? "启用任务栏替代模式后生效"
                : "当前不会随 Windows 启动";
            return;
        }

        ApplyStartupPreference(value);
    }

    [RelayCommand]
    private void Navigate(string? destination)
    {
        CloseTransientPanels();
        switch (destination)
        {
            case "Dashboard":
                if (_dashboardViewModel == null)
                {
                    _dashboardViewModel =
                        new DashboardViewModel();
                    _dashboardViewModel.NavigationRequested +=
                        Dashboard_NavigationRequested;
                }
                CurrentViewModel = _dashboardViewModel;
                CurrentSectionTitle = "今日概览";
                _ = _dashboardViewModel.RefreshAsync();
                break;
            case "Tasks":
                _tasksViewModel ??= new TasksViewModel();
                CurrentViewModel = _tasksViewModel;
                CurrentSectionTitle = "任务";
                break;
            case "Pomodoro":
                if (_pomodoroViewModel == null)
                {
                    _pomodoroViewModel =
                        new PomodoroViewModel();
                    _pomodoroViewModel.SessionCompleted +=
                        PomodoroViewModel_SessionCompleted;
                }
                CurrentViewModel = _pomodoroViewModel;
                CurrentSectionTitle = "番茄钟";
                break;
            case "Files":
                _fileOrganizerViewModel ??= new FileOrganizerViewModel();
                CurrentViewModel = _fileOrganizerViewModel;
                CurrentSectionTitle = "桌面收纳";
                break;
            case "OKR":
                _okrViewModel ??= new OkrViewModel();
                CurrentViewModel = _okrViewModel;
                CurrentSectionTitle = "OKR";
                break;
            case "AI":
                _aiAssistantViewModel ??= new AIAssistantViewModel();
                CurrentViewModel = _aiAssistantViewModel;
                CurrentSectionTitle = "AI 助手";
                break;
            default:
                return;
        }

        LastWorkspace = destination;
        WorkspaceRequested?.Invoke(destination);
    }

    [RelayCommand]
    private void LaunchApp(AppLaunchItem? app)
    {
        if (app == null)
            return;
        if (TryLaunchApp(app))
            IsSearchOpen = false;
    }

    [RelayCommand]
    private void TogglePin(AppLaunchItem? app)
    {
        if (app == null)
            return;

        if (!TrySetPinned(app, !app.IsPinned))
            return;
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    public void MoveTaskbarApp(TaskbarAppItem source, TaskbarAppItem target)
    {
        if (ReferenceEquals(source, target))
            return;

        AppLaunchItem? launch = source.CreateLaunchItem();
        if (launch == null)
            return;
        if (!source.IsPinned
            && !TrySetPinned(launch, true))
        {
            return;
        }

        int targetIndex = TaskbarApps
            .TakeWhile(item => !ReferenceEquals(item, target))
            .Count(item => item.IsPinned);
        if (!target.IsPinned)
            targetIndex = TaskbarApps.Count(item => item.IsPinned) - 1;
        if (!TryMovePinned(
                launch,
                Math.Max(0, targetIndex)))
        {
            RefreshTaskbarApps();
            RefreshSearchResults();
            return;
        }
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    [RelayCommand]
    private void ActivateTaskbarApp(TaskbarAppItem? task)
    {
        if (task?.RunningTask != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.ActivateOrMinimize(
                        task.RunningTask)),
                $"无法切换“{task.DisplayName}”。窗口可能已经关闭，"
                + "或 Windows 暂时阻止了前台切换。");
            return;
        }
        AppLaunchItem? launch = task?.CreateLaunchItem();
        if (launch != null)
            TryLaunchApp(launch);
    }

    [RelayCommand]
    private void LaunchNewTaskbarApp(TaskbarAppItem? task)
    {
        AppLaunchItem? launch = task?.CreateLaunchItem();
        if (launch != null)
            TryLaunchApp(launch);
    }

    [RelayCommand]
    private void ToggleTaskbarPin(TaskbarAppItem? task)
    {
        if (task == null)
            return;

        if (task.IsPinned)
        {
            foreach (AppLaunchItem launch in task.PinnedLaunches)
            {
                if (!TrySetPinned(launch, false))
                    break;
            }
        }
        else
        {
            AppLaunchItem? launch = task.CreateLaunchItem();
            if (launch == null)
                return;
            TrySetPinned(launch, true);
        }
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    [RelayCommand]
    private void ActivateWindow(WindowReference? window)
    {
        if (window != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.Activate(
                        window.Handle)),
                $"无法切换到“{window.Title}”。窗口可能已经关闭，"
                + "或 Windows 暂时阻止了前台切换。");
        }
    }

    [RelayCommand]
    private void CloseWindow(WindowReference? window)
    {
        if (window != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.Close(
                        window.Handle)),
                $"无法关闭“{window.Title}”。窗口可能已经关闭，"
                + "或当前应用拒绝了关闭消息。");
        }
    }

    [RelayCommand]
    private void CloseTask(TaskbarAppItem? task)
    {
        if (task == null)
            return;

        bool succeeded = true;
        foreach (WindowReference window in task.Windows)
        {
            if (!SystemActionExecution.Try(
                    () => _windowTracker.Close(
                        window.Handle)))
            {
                succeeded = false;
            }
        }

        CompleteTaskbarWindowAction(
            succeeded,
            $"未能关闭“{task.DisplayName}”的全部窗口。"
            + "部分窗口可能已经关闭或拒绝了关闭消息。");
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        bool open = !IsSearchOpen;
        CloseTransientPanels();
        IsSearchOpen = open;
        if (IsSearchOpen)
            RefreshSearchResults();
    }

    [RelayCommand]
    private void ToggleCalendar()
    {
        bool open = !IsCalendarOpen;
        CloseTransientPanels();
        IsCalendarOpen = open;
    }

    [RelayCommand]
    private void ShowPreviousCalendarMonth()
    {
        DisplayedCalendarMonth =
            DisplayedCalendarMonth.AddMonths(-1);
        SelectCalendarDate(
            new DateTime(
                DisplayedCalendarMonth.Year,
                DisplayedCalendarMonth.Month,
                1));
        RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void ShowNextCalendarMonth()
    {
        DisplayedCalendarMonth =
            DisplayedCalendarMonth.AddMonths(1);
        SelectCalendarDate(
            new DateTime(
                DisplayedCalendarMonth.Year,
                DisplayedCalendarMonth.Month,
                1));
        RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void ShowTodayInCalendar()
    {
        DateTime today = DateTime.Today;
        DisplayedCalendarMonth =
            new DateTime(today.Year, today.Month, 1);
        SelectedCalendarDate = today;
        RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void SelectCalendarDate(CalendarDayItem? item)
    {
        if (item == null)
            return;

        SelectCalendarDate(item.Date);
        if (!item.IsCurrentMonth)
        {
            DisplayedCalendarMonth =
                new DateTime(
                    item.Date.Year,
                    item.Date.Month,
                    1);
            RequestTaskSummaryRefresh();
        }
    }

    [RelayCommand]
    private void ToggleFocusCenter()
    {
        bool open = !IsFocusCenterOpen;
        CloseTransientPanels();
        IsFocusCenterOpen = open;
    }

    [RelayCommand]
    private void ToggleStatusCenter()
    {
        bool open = !IsStatusCenterOpen;
        CloseTransientPanels();
        IsStatusCenterOpen = open;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowsProtectedSystemFiles = _desktopVisibility.ShowsProtectedSystemFiles;
        bool open = !IsSettingsOpen;
        CloseTransientPanels();
        IsSettingsOpen = open;
    }

    [RelayCommand]
    private void TogglePowerMenu()
    {
        bool open = !IsPowerMenuOpen;
        CloseTransientPanels();
        IsPowerMenuOpen = open;
    }

    [RelayCommand]
    private void ToggleMute() =>
        TryApplyMuted(!_confirmedMuted);

    public void AdjustMasterVolume(float step)
    {
        float requested = Math.Clamp(
            _confirmedMasterVolume + step,
            0f,
            1f);
        if (TryApplyMasterVolume(requested)
            && requested > 0
            && _confirmedMuted)
        {
            TryApplyMuted(false);
        }
    }

    [RelayCommand]
    private void OpenQuickSettings()
        => CompleteSystemAction(
            _systemStatus.OpenQuickSettings(),
            "无法唤起 Windows 快捷设置，请使用 Win+A。");

    [RelayCommand]
    private void OpenNotifications()
        => CompleteSystemAction(
            _systemStatus.OpenNotifications(),
            "无法唤起 Windows 通知中心，请使用 Win+N。");

    [RelayCommand]
    private void OpenInputSwitcher()
        => CompleteSystemAction(
            _systemStatus.OpenInputSwitcher(),
            "无法唤起输入法切换器，请使用 Win+Space。");

    [RelayCommand]
    private void OpenStartMenu()
        => CompleteSystemAction(
            _systemStatus.OpenStartMenu(),
            "无法唤起开始菜单，请按 Windows 键。");

    [RelayCommand]
    private void OpenTaskView()
        => CompleteSystemAction(
            _systemStatus.OpenTaskView(),
            "无法唤起任务视图，请使用 Win+Tab。");

    [RelayCommand]
    private void OpenWindowsSearch()
        => CompleteSystemAction(
            _systemStatus.OpenWindowsSearch(),
            "无法唤起 Windows 搜索，请使用 Win+S。");

    [RelayCommand]
    private void OpenWidgets()
        => CompleteSystemAction(
            _systemStatus.OpenWidgets(),
            "无法唤起 Windows 小组件，请使用 Win+W。");

    [RelayCommand]
    private void OpenRunDialog()
        => CompleteSystemAction(
            _systemStatus.OpenRunDialog(),
            "无法唤起运行对话框，请使用 Win+R。");

    [RelayCommand]
    private void OpenManagementTool(SystemManagementTool tool)
        => CompleteSystemAction(
            _systemStatus.OpenManagementTool(tool),
            "无法打开所选 Windows 管理工具。当前账户权限或系统版本可能不支持该入口。");

    [RelayCommand]
    private void OpenPowerSettings()
        => CompleteSystemAction(
            _systemStatus.OpenPowerSettings(),
            "无法打开 Windows 电源设置。");

    [RelayCommand]
    private void ShowDesktop()
        => CompleteSystemAction(
            _systemStatus.ShowDesktop(),
            "无法显示桌面，请使用 Win+D。");

    [RelayCommand]
    private void LockComputer()
        => CompleteSystemAction(
            _systemStatus.Lock(),
            "Windows 拒绝锁定当前会话，请使用 Win+L。");

    [RelayCommand]
    private void SleepComputer()
        => CompleteSystemAction(
            _systemStatus.Sleep(),
            "Windows 拒绝进入睡眠，当前电源策略可能不支持该操作。");

    [RelayCommand]
    private void RestartComputer()
    {
        if (FocusDialogService.Show("确定要立即重启电脑吗？", "重启电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            CompleteSystemAction(
                _systemStatus.Restart(),
                "无法启动系统重启，当前账户权限或系统策略可能阻止了操作。");
        }
    }

    [RelayCommand]
    private void ShutdownComputer()
    {
        if (FocusDialogService.Show("确定要立即关闭电脑吗？", "关闭电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            CompleteSystemAction(
                _systemStatus.Shutdown(),
                "无法启动系统关机，当前账户权限或系统策略可能阻止了操作。");
        }
    }

    [RelayCommand]
    private void EnableReplacement() => RequestEnableReplacement?.Invoke();

    [RelayCommand]
    private void DisableReplacement() => RequestDisableReplacement?.Invoke();

    [RelayCommand]
    private void SkipOnboarding()
    {
        IsOnboardingVisible = false;
        SaveBooleanConfig(FirstRunAcceptedKey, true);
        SaveBooleanConfig(ReplacementEnabledKey, false);
    }

    public void MarkReplacementEnabled(bool enabled)
        => MarkReplacementEnabled(enabled, null);

    public void MarkReplacementEnabled(bool enabled, string? error)
    {
        IsReplacementEnabled = enabled;
        IsOnboardingVisible = false;
        HasReplacementWarning = !enabled && !string.IsNullOrWhiteSpace(error);
        if (enabled || !HasReplacementWarning)
            ReplacementStopReason = null;
        ReplacementStatus = enabled
            ? "侧边任务栏运行中 · Windows 任务栏已完整隐藏"
            : HasReplacementWarning
                ? "Windows 任务栏已安全恢复"
                : "替代模式未启用，Windows 任务栏保持原设置";
        ReplacementError = error ?? string.Empty;
        SaveBooleanConfig(FirstRunAcceptedKey, true);
        SaveBooleanConfig(ReplacementEnabledKey, enabled);
        ApplyStartupPreference(enabled && StartWithWindows);
    }

    public void MarkReplacementStopped(TaskbarReplacementStopReason reason, string message)
    {
        ReplacementStopReason = reason;
        MarkReplacementEnabled(false, message);
    }

    [RelayCommand]
    private void OpenLastWorkspace() => Navigate(LastWorkspace);

    [RelayCommand]
    private void CloseApp() => RequestClose?.Invoke();

    [RelayCommand]
    private void MinimizeApp()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.CollapseSidebar();
    }

    [RelayCommand]
    private void ShowWindow()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.ShowFromTray();
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        var result = FocusDialogService.Show(
            "确定要从最新备份恢复数据库吗？\n任务、番茄钟、桌面收纳和 OKR 数据都会回到备份时的状态，应用将立即重启。",
            "恢复数据库",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            string executable = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法定位 FocusPanel 可执行文件。");
            string arguments =
                $"--restore-after-exit {Environment.ProcessId}";
            _ = Process.Start(
                new ProcessStartInfo(
                    executable,
                    arguments)
                {
                    UseShellExecute = true
                })
                ?? throw new InvalidOperationException(
                    "无法启动数据库恢复交接进程。");
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            FocusDialogService.Show($"无法重启应用：{ex.Message}", "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task CheckForUpdatesInBackgroundAsync()
    {
        if (!_updateService.CanUpdate || IsUpdateBusy)
            return;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        try
        {
            UpdateStatus = "正在从 GitHub Releases 自动检查更新…";
            AppUpdateInfo? update = await _updateService.CheckForUpdateAsync();
            if (update == null)
            {
                ApplyUpdateAvailability(null);
                UpdateStatus = $"已是最新版本 v{CurrentAppVersion}";
                return;
            }

            ApplyUpdateAvailability(update);
            UpdateStatus = $"GitHub 已发布 v{update.Version}，点击下方按钮安装";
            if (!string.Equals(
                    _lastNotifiedUpdateVersion,
                    update.Version,
                    StringComparison.OrdinalIgnoreCase))
            {
                _lastNotifiedUpdateVersion = update.Version;
                UpdateAvailable?.Invoke(update);
            }
        }
        catch
        {
            UpdateStatus = "GitHub 自动检查暂时失败，可稍后手动重试";
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckAndInstallUpdate()
    {
        if (IsUpdateBusy)
            return;

        if (!_updateService.CanUpdate)
        {
            UpdateStatus = "开发运行版不能原地更新，请先安装 Setup.exe 发布包。";
            return;
        }

        IsUpdateBusy = true;
        UpdateProgress = 0;
        try
        {
            UpdateStatus = "正在检查更新…";
            AppUpdateInfo? update = await _updateService.CheckForUpdateAsync();
            if (update == null)
            {
                ApplyUpdateAvailability(null);
                UpdateStatus = $"已是最新版本 v{CurrentAppVersion}";
                return;
            }

            ApplyUpdateAvailability(update);
            string sizeText = update.DownloadSize > 0
                ? $"{update.DownloadSize / 1024d / 1024d:F1} MB"
                : "未知大小";
            string notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "本次版本未提供更新说明。"
                : update.ReleaseNotes.Trim();
            if (notes.Length > 600)
                notes = notes[..600] + "…";

            MessageBoxResult result = FocusDialogService.Show(
                $"发现 FocusPanel v{update.Version}（{sizeText}）。\n\n{notes}\n\n"
                + "下载完成后应用会恢复系统任务栏、自动重启并安装更新。是否继续？",
                "安装 FocusPanel 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
            {
                UpdateStatus = $"已发现 v{update.Version}，等待安装";
                return;
            }

            UpdateStatus = $"正在下载 v{update.Version}…";
            var progress = new Progress<int>(value =>
            {
                UpdateProgress = value;
                UpdateStatus = $"正在下载 v{update.Version}… {value}%";
            });
            await _updateService.DownloadUpdateAsync(progress);

            UpdateProgress = 100;
            UpdateStatus = "下载完成，正在安全重启并安装…";
            RequestApplyUpdate?.Invoke();
        }
        catch (Exception ex)
        {
            string message = UpdateFailureMessage.Describe(ex);
            UpdateStatus = $"更新失败：{message}";
            FocusDialogService.Show(
                $"无法完成更新：{message}\n\n系统任务栏和现有数据不会被修改。",
                "FocusPanel 更新失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    [RelayCommand]
    private void OpenUpdateDownloadPage()
    {
        UpdateStatus = _updateService.OpenDownloadPage()
            ? "已在浏览器打开 FocusPanel 官方下载页"
            : "无法打开浏览器，请访问 GitHub 上的 SakalioLabs/FocusPanel Releases";
    }

    private void ApplyUpdateAvailability(AppUpdateInfo? update)
    {
        UpdateAvailabilityState state =
            UpdateAvailabilityPolicy.FromUpdate(update);
        IsUpdateAvailable = state.IsAvailable;
        AvailableUpdateVersion = state.Version;
    }

    private void RefreshTaskbarApps()
        => TaskbarAppCollectionSynchronizer.Synchronize(
            TaskbarApps,
            _taskbarComposer.Compose(_appCatalog.GetPinned(), _windowTracker.GetSnapshot()));

    private void RefreshSearchResults()
    {
        string? selectedIdentity = SelectedSearchResult?.IdentityKey;
        ReplaceCollection(SearchResults, _appCatalog.Search(SearchQuery));
        SelectedSearchResult = SearchResults.FirstOrDefault(
            item => !string.IsNullOrWhiteSpace(selectedIdentity)
                && string.Equals(
                    item.IdentityKey,
                    selectedIdentity,
                    StringComparison.OrdinalIgnoreCase))
            ?? SearchResults.FirstOrDefault();
        OnPropertyChanged(
            nameof(IsAppSearchStatusVisible));
        OnPropertyChanged(
            nameof(AppSearchStatusText));
    }

    private void OnWindowSnapshotChanged(object? sender, EventArgs e) => RefreshTaskbarApps();
    private void Dashboard_NavigationRequested(
        string destination) =>
        Navigate(destination);

    private void PomodoroViewModel_SessionCompleted(
        object? sender,
        PomodoroCompletedEventArgs e)
        => PomodoroCompleted?.Invoke(
            e.DurationMinutes);

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        IsAppCatalogLoading = _appCatalog.IsIndexing;
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    private void RequestSystemStatusRefresh()
    {
        if (!_isDisposed)
            _systemStatusRefresh.Request();
    }

    private PendingSystemStatusSnapshot
        CaptureSystemStatus()
    {
        long audioRevision = Volatile.Read(
            ref _audioStateRevision);
        return new PendingSystemStatusSnapshot(
            _systemStatus.GetStatusSnapshot(),
            audioRevision);
    }

    private async Task ApplySystemStatusAsync(
        PendingSystemStatusSnapshot pending,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (_isDisposed
                    || cancellationToken
                        .IsCancellationRequested)
                {
                    return;
                }

                ApplySystemStatus(pending);
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ApplySystemStatus(
        PendingSystemStatusSnapshot pending)
    {
        SystemStatusSnapshot snapshot =
            pending.Snapshot;
        AudioStatusSnapshot audio =
            snapshot.Audio;
        if (SystemStatusRefreshPolicy.ShouldApplyAudio(
                pending.AudioRevision,
                Volatile.Read(
                    ref _audioStateRevision)))
        {
            IsAudioAvailable = audio.IsAvailable;
            AudioStatusText = audio.IsAvailable
                ? string.Empty
                : "未检测到可用的音频输出设备";
            if (audio.IsAvailable)
            {
                RestoreConfirmedAudioState(
                    audio.MasterVolume,
                    audio.IsMuted);
            }
        }

        NetworkStatusSnapshot network =
            snapshot.Network;
        IsNetworkAvailable = network.IsAvailable;
        NetworkConnectionKind =
            network.ConnectionKind;
        NetworkDisplayName = network.DisplayName;
        NetworkDetail = network.Detail;
        InputMethodStatus =
            snapshot.InputMethod;
        BatteryStatusSnapshot battery =
            snapshot.Battery;
        HasBattery = battery.HasBattery;
        BatteryPercent = battery.Percent;
        IsCharging = battery.IsCharging;
    }

    private AudioStatusPresentation GetAudioPresentation()
        => AudioStatusPresentationComposer.Compose(
            IsAudioAvailable,
            MasterVolume,
            IsMuted);

    private BatteryStatusPresentation GetBatteryPresentation()
        => BatteryStatusPresentationComposer.Compose(
            HasBattery,
            BatteryPercent,
            IsCharging);

    private NetworkStatusPresentation GetNetworkPresentation()
        => NetworkStatusPresentationComposer.Compose(
            IsNetworkAvailable,
            NetworkConnectionKind,
            NetworkDisplayName);

    private void RestoreConfirmedAudioState(
        float volume,
        bool muted)
    {
        _confirmedMasterVolume = Math.Clamp(
            volume,
            0f,
            1f);
        _confirmedMuted = muted;
        _updatingAudioState = true;
        try
        {
            MasterVolume = _confirmedMasterVolume;
            IsMuted = _confirmedMuted;
        }
        finally
        {
            _updatingAudioState = false;
        }
    }

    private void ReportAudioFailure(string message)
    {
        IsAudioAvailable = false;
        AudioStatusText =
            "默认音频输出设备暂时不可用";
        SystemActionMessage = message;
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private void ApplyStartupPreference(bool enable)
    {
        if (AutoStartupService.TrySetStartup(
                enable,
                out string? error))
        {
            StartupStatus = enable
                ? "已设置为随 Windows 启动"
                : "当前不会随 Windows 启动";
            return;
        }

        StartupStatus = error
            ?? "无法更新 Windows 启动项。";
        _updatingStartupState = true;
        try
        {
            StartWithWindows =
                AutoStartupService.IsStartupEnabled();
        }
        finally
        {
            _updatingStartupState = false;
        }
    }

    private void RequestTaskSummaryRefresh()
    {
        if (_isDisposed)
            return;

        DateTime month =
            TaskSummarySnapshot.NormalizeMonth(
                DisplayedCalendarMonth);
        if (_calendarFocusMonth != month)
        {
            _calendarFocusMonth = month;
            _calendarFocusByDate =
                new Dictionary<
                    DateTime,
                    CalendarFocusSummary>();
            RefreshCalendarDays();
        }
        Interlocked.Exchange(
            ref _taskSummaryMonthTicks,
            month.Ticks);
        _taskSummaryRefresh.Request();
    }

    private TaskSummarySnapshot CaptureTaskSummary()
    {
        long monthTicks = Interlocked.Read(
            ref _taskSummaryMonthTicks);
        DateTime month = monthTicks > 0
            ? new DateTime(monthTicks)
            : DateTime.Today;
        return _taskSummaryReader.Read(month);
    }

    private async Task ApplyTaskSummaryAsync(
        TaskSummarySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (_isDisposed
                    || cancellationToken
                        .IsCancellationRequested)
                {
                    return;
                }

                ApplyTaskSummary(snapshot);
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ApplyTaskSummary(
        TaskSummarySnapshot snapshot)
    {
        TaskSummaryApplyDecision decision =
            TaskSummaryApplyPolicy.GetDecision(
                snapshot,
                DisplayedCalendarMonth);
        if (decision.ApplyOpenTaskCount)
            OpenTaskCount = snapshot.OpenTaskCount;
        if (!decision.ApplyCalendar)
            return;

        _calendarFocusByDate =
            snapshot.FocusByDate;
        _calendarFocusMonth =
            snapshot.DisplayedMonth;
        RefreshCalendarDays();
    }

    private void SelectCalendarDate(DateTime date)
    {
        SelectedCalendarDate = date.Date;
        RefreshCalendarDays();
    }

    private void RefreshCalendarDays()
    {
        ReplaceCollection(
            CalendarDays,
            CalendarMonthComposer.Compose(
                DisplayedCalendarMonth,
                SelectedCalendarDate,
                DateTime.Today,
                _calendarFocusByDate));
        if (_calendarFocusByDate.TryGetValue(
                SelectedCalendarDate.Date,
                out CalendarFocusSummary? focus)
            && focus.SessionCount > 0)
        {
            SelectedDayFocusSummary =
                $"完成 {focus.SessionCount} 次专注 · "
                + $"{focus.DurationMinutes} 分钟";
        }
        else
        {
            SelectedDayFocusSummary =
                "该日没有完成的专注记录";
        }
    }

    private void UpdateRefreshActivity()
    {
        ShellRefreshActivity activity =
            ShellRefreshActivityPolicy.GetActivity(
                _isShellVisible,
                IsStatusCenterOpen,
                IsCalendarOpen);
        SetTimerState(_clockTimer, activity.Clock);
        SetTimerState(_systemStatusTimer, activity.SystemStatus);
        SetTimerState(_taskSummaryTimer, activity.TaskSummary);
    }

    private static void SetTimerState(
        DispatcherTimer timer,
        bool shouldRun)
    {
        if (shouldRun)
        {
            if (!timer.IsEnabled)
                timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void CloseTransientPanels()
    {
        IsSearchOpen = false;
        IsCalendarOpen = false;
        IsFocusCenterOpen = false;
        IsStatusCenterOpen = false;
        IsSettingsOpen = false;
        IsPowerMenuOpen = false;
    }

    private void CompleteSystemAction(bool succeeded, string error)
    {
        SystemActionMessage = succeeded ? string.Empty : error;
        CloseTransientPanels();
        if (!succeeded)
            IsStatusCenterOpen = true;
    }

    private bool TryLaunchApp(AppLaunchItem app)
    {
        bool succeeded = SystemActionExecution.Try(
            () => _appCatalog.Launch(app));
        if (succeeded)
        {
            SystemActionMessage = string.Empty;
            return true;
        }

        SystemActionMessage =
            $"无法启动“{app.DisplayName}”。应用可能已卸载，"
            + "或固定目标已经移动；请在搜索中重新固定。";
        CloseTransientPanels();
        IsStatusCenterOpen = true;
        return false;
    }

    private bool TrySetPinned(
        AppLaunchItem app,
        bool pinned)
    {
        bool succeeded = SystemActionExecution.Try(
            () => _appCatalog.SetPinned(app, pinned));
        if (succeeded)
        {
            SystemActionMessage = string.Empty;
            return true;
        }

        ReportTaskbarActionFailure(
            pinned
                ? $"无法固定“{app.DisplayName}”。请稍后重试。"
                : $"无法取消固定“{app.DisplayName}”。请稍后重试。");
        return false;
    }

    private bool TryMovePinned(
        AppLaunchItem app,
        int newIndex)
    {
        bool succeeded = SystemActionExecution.Try(
            () => _appCatalog.MovePinned(
                app,
                newIndex));
        if (succeeded)
        {
            SystemActionMessage = string.Empty;
            return true;
        }

        ReportTaskbarActionFailure(
            $"无法保存“{app.DisplayName}”的新位置。"
            + "固定状态已经保留，请稍后重新排序。");
        return false;
    }

    private void CompleteTaskbarWindowAction(
        bool succeeded,
        string error)
    {
        if (succeeded)
        {
            SystemActionMessage = string.Empty;
            return;
        }

        ReportTaskbarActionFailure(error);
    }

    private void ReportTaskbarActionFailure(
        string message)
    {
        SystemActionMessage = message;
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> destination, System.Collections.Generic.IEnumerable<T> source)
    {
        destination.Clear();
        foreach (T item in source)
            destination.Add(item);
    }

    private static bool ReadBooleanConfig(string key, bool defaultValue = false)
    {
        try
        {
            using var context = new AppDbContext();
            return bool.TryParse(context.AppConfigs.Find(key)?.Value, out bool value) ? value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string ReadStringConfig(string key, string defaultValue)
    {
        try
        {
            using var context = new AppDbContext();
            string? value = context.AppConfigs.Find(key)?.Value;
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static void SaveBooleanConfig(string key, bool value)
        => SaveStringConfig(key, value.ToString());

    private static void SaveStringConfig(string key, string value)
    {
        using var context = new AppDbContext();
        var config = context.AppConfigs.Find(key);
        if (config == null)
        {
            context.AppConfigs.Add(new AppConfig { Key = key, Value = value.ToString() });
        }
        else
        {
            config.Value = value;
        }
        context.SaveChanges();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _clockTimer.Stop();
        _systemStatusTimer.Stop();
        _taskSummaryTimer.Stop();
        _updateCheckTimer.Stop();
        _systemStatusRefresh.Dispose();
        _taskSummaryRefresh.Dispose();
        _windowTracker.SnapshotChanged -= OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged -= OnCatalogChanged;
        _tasksViewModel?.Dispose();
        _okrViewModel?.Dispose();
        _aiAssistantViewModel?.Dispose();
        _fileOrganizerViewModel?.Dispose();
        if (_dashboardViewModel != null)
        {
            _dashboardViewModel.NavigationRequested -=
                Dashboard_NavigationRequested;
            _dashboardViewModel.Dispose();
        }
        if (_pomodoroViewModel != null)
        {
            _pomodoroViewModel.SessionCompleted -=
                PomodoroViewModel_SessionCompleted;
            _pomodoroViewModel.Dispose();
        }
    }

    private readonly record struct
        PendingSystemStatusSnapshot(
            SystemStatusSnapshot Snapshot,
            long AudioRevision);
}
