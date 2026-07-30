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
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.Views;

namespace FocusPanel.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo ChineseCulture =
        CultureInfo.GetCultureInfo("zh-CN");

    private readonly IAppCatalogService _appCatalog;
    private readonly IWindowTracker _windowTracker;
    private readonly ISystemStatusService _systemStatus;
    private readonly IAppUpdateService _updateService;
    private readonly IDesktopItemVisibilityService _desktopVisibility;
    private readonly IShellPreferenceRepository
        _shellPreferences;
    private readonly AudioControlCoordinator
        _audioControl;
    private readonly AppLaunchCoordinator
        _appLaunch;
    private readonly SystemActionCoordinator
        _systemActions;
    private readonly ClipboardTextService
        _clipboard;
    private readonly AutoStartupCoordinator
        _autoStartup;
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
    private readonly CoalescingBackgroundRefresh<
        bool> _protectedVisibilityRefresh;
    private readonly Task
        _shellPreferencesInitialization;
    private readonly Task<FileOrganizerViewModel>
        _fileOrganizerInitialization;
    private readonly WorkspaceLoadingViewModel
        _fileOrganizerLoadingViewModel =
            new("正在准备桌面收纳…");
    private DashboardViewModel? _dashboardViewModel;
    private TasksViewModel? _tasksViewModel;
    private PomodoroViewModel? _pomodoroViewModel;
    private FileOrganizerViewModel? _fileOrganizerViewModel;
    private OkrViewModel? _okrViewModel;
    private AIAssistantViewModel? _aiAssistantViewModel;
    private bool _updatingAudioState;
    private long _audioStateRevision;
    private long _audioVolumeRevision;
    private long _audioMuteRevision;
    private volatile bool _volumeWritePending;
    private volatile bool _muteWritePending;
    private long _taskSummaryMonthTicks;
    private float _confirmedMasterVolume;
    private bool _confirmedMuted;
    private bool _updatingStartupState;
    private string? _lastNotifiedUpdateVersion;
    private bool _isShellVisible;
    private bool _isDisposed;
    private Task? _disposeTask;
    private bool _loadingShellPreferences;
    private bool _refreshingDisplayTargetOptions;
    private long _workspaceNavigationRevision;
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
    private ShellSearchResult? selectedSearchResult;

    [ObservableProperty]
    private string? activeTaskbarIdentity;

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
    [NotifyPropertyChangedFor(
        nameof(WorkspacePinActionText))]
    private bool isWorkspacePinned;

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
    private bool enableTaskbarSlotHotkeys;

    [ObservableProperty]
    private string summonShortcutText =
        "正在注册主动唤出快捷键…";

    [ObservableProperty]
    private string taskbarSlotShortcutText =
        "九槽位全局快速键已关闭";

    [ObservableProperty]
    private string displayTargetMode =
        ShellDisplayTarget.OutermostRightValue;

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
        : this(
            appCatalog,
            windowTracker,
            systemStatus,
            updateService,
            new ShellPreferenceRepository())
    {
    }

    internal MainViewModel(
        IAppCatalogService appCatalog,
        IWindowTracker windowTracker,
        ISystemStatusService systemStatus,
        IAppUpdateService updateService,
        IShellPreferenceRepository
            shellPreferences,
        SystemActionCoordinator? systemActions = null,
        AutoStartupCoordinator? autoStartup = null,
        IFileOrganizerViewModelFactory?
            fileOrganizerFactory = null,
        ClipboardTextService?
            clipboard = null)
    {
        _appCatalog = appCatalog;
        _windowTracker = windowTracker;
        _systemStatus = systemStatus;
        _updateService = updateService;
        _appLaunch =
            new AppLaunchCoordinator(
                _appCatalog.Launch);
        _systemActions =
            systemActions
            ?? new SystemActionCoordinator();
        _clipboard =
            clipboard
            ?? new ClipboardTextService();
        _autoStartup =
            autoStartup
            ?? new AutoStartupCoordinator();
        _audioControl =
            new AudioControlCoordinator(
                _systemStatus.TrySetMasterVolume,
                _systemStatus.TrySetMuted);
        _audioControl.Completed +=
            OnAudioControlCompleted;
        _shellPreferences =
            shellPreferences
            ?? throw new ArgumentNullException(
                nameof(shellPreferences));
        _shellPreferences.SaveFailed +=
            OnShellPreferenceSaveFailed;
        _desktopVisibility = new WindowsDesktopItemVisibilityService();
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        IFileOrganizerViewModelFactory
            organizerFactory =
                fileOrganizerFactory
                ?? new FileOrganizerViewModelFactory();
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
        _protectedVisibilityRefresh =
            new CoalescingBackgroundRefresh<bool>(
                () =>
                    _desktopVisibility
                        .ShowsProtectedSystemFiles,
                ApplyProtectedVisibilityAsync,
                ex => Debug.WriteLine(
                    "读取 Explorer 受保护文件设置失败："
                    + ex));
        IsAppCatalogLoading = _appCatalog.IsIndexing;

        CurrentTime = DateTime.Now;
        DisplayedCalendarMonth = new DateTime(
            CurrentTime.Year,
            CurrentTime.Month,
            1);
        SelectedCalendarDate = CurrentTime.Date;
        CurrentAppVersion = _updateService.CurrentVersion;
        UpdateStatus =
            "正在准备 GitHub Releases 更新服务…";
        StartupStatus =
            "正在读取 Windows 启动项…";
        _ = LoadStartupStateAsync();
        IsReplacementEnabled = false;
        ReplacementStatus =
            "正在读取任务栏替代设置…";
        IsOnboardingVisible = true;
        RefreshDisplayTargetOptions();
        _shellPreferencesInitialization =
            LoadShellPreferencesAsync();
        ShowsProtectedSystemFiles = false;
        RequestProtectedVisibilityRefresh();

        _fileOrganizerInitialization =
            organizerFactory.CreateAsync(
                _uiDispatcher);
        CurrentViewModel =
            _fileOrganizerLoadingViewModel;
        _ = LoadFileOrganizerWorkspaceAsync(
            _workspaceNavigationRevision);

        // Subscribe before reading the initial background snapshots. A capture
        // can finish while this constructor is still building the shell; the
        // UI-dispatched event must not be missed between the first read and the
        // subscription.
        _windowTracker.SnapshotChanged += OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged += OnCatalogChanged;
        RefreshTaskbarApps();
        RefreshSearchResults();
        RequestSystemStatusRefresh();
        RequestTaskSummaryRefresh();

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

    public ObservableCollection<ShellSearchResult> SearchResults { get; } = new();
    public ObservableCollection<TaskbarAppItem> TaskbarApps { get; } = new();
    public ObservableCollection<
        ShellDisplayTargetOption> DisplayTargetOptions
    {
        get;
    } = new();
    public string AppSearchStatusText =>
        IsAppCatalogLoading
            ? "正在载入应用目录…"
            : "没有找到匹配的应用、窗口、系统命令或计算结果";
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

    public string WorkspacePinActionText =>
        IsWorkspacePinned
            ? "取消固定工作区"
            : "固定工作区，不自动收起";
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
    public event Func<Task>? RequestApplyUpdate;
    public event Action<AppUpdateInfo>? UpdateAvailable;
    public event Action<string>? WorkspaceRequested;
    public event Action<int>? PomodoroCompleted;
    public event Action? DisplayTargetChanged;
    public event Action<bool>? WorkspacePinChanged;

    internal void SetSummonShortcutStatus(
        ShellHotkeyRegistration registration)
    {
        SummonShortcutText =
            registration.DisplayText;
    }

    internal void SetTaskbarSlotShortcutStatus(
        TaskbarSlotHotkeyRegistration
            registration)
    {
        TaskbarSlotShortcutText =
            registration.DisplayText;
    }

    internal void SetTaskbarSlotShortcutDisabled()
    {
        TaskbarSlotShortcutText =
            EnableTaskbarSlotHotkeys
                ? "快速应用快捷键将在任务栏接管成功后注册"
                : "九槽位全局快速键已关闭";
    }

    public void RefreshDisplayTargetOptions()
    {
        string selectedValue =
            DisplayTargetMode;
        IReadOnlyList<ShellDisplayTargetOption>
            options =
                ShellDisplayTarget.GetOptions(
                    selectedValue);
        _refreshingDisplayTargetOptions = true;
        try
        {
            DisplayTargetOptions.Clear();
            foreach (ShellDisplayTargetOption option
                     in options)
            {
                DisplayTargetOptions.Add(option);
            }

            if (!string.Equals(
                    DisplayTargetMode,
                    selectedValue,
                    StringComparison.Ordinal))
            {
                DisplayTargetMode =
                    selectedValue;
            }
        }
        finally
        {
            _refreshingDisplayTargetOptions =
                false;
        }
    }

    internal Task WaitForShellPreferencesAsync() =>
        _shellPreferencesInitialization;

    private async Task LoadShellPreferencesAsync()
    {
        ShellPreferenceSnapshot preferenceSnapshot =
            await _shellPreferences.LoadAsync();
        if (_isDisposed)
            return;

        _loadingShellPreferences = true;
        try
        {
            IsReplacementEnabled =
                preferenceSnapshot
                    .ReplacementEnabled;
            ReplacementStatus =
                IsReplacementEnabled
                    ? "侧边任务栏等待安全接管"
                    : "替代模式未启用，Windows 任务栏保持原设置";
            ThemeMode =
                preferenceSnapshot.ThemeMode;
            DisableHotZoneInFullscreen =
                preferenceSnapshot
                    .DisableHotZoneInFullscreen;
            EnableTaskbarSlotHotkeys =
                preferenceSnapshot
                    .EnableTaskbarSlotHotkeys;
            DisplayTargetMode =
                preferenceSnapshot
                    .DisplayTargetMode;
            IsOnboardingVisible =
                !preferenceSnapshot
                    .FirstRunAccepted
                || !IsReplacementEnabled;
        }
        finally
        {
            _loadingShellPreferences = false;
        }

        ThemeService.SetMode(
            ThemeMode);
    }

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

    partial void OnIsWorkspacePinnedChanged(
        bool value) =>
        WorkspacePinChanged?.Invoke(value);

    partial void OnMasterVolumeChanged(float value)
    {
        if (_updatingAudioState)
            return;

        QueueMasterVolume(value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_updatingAudioState)
            return;

        QueueMuted(value);
    }

    private void QueueMasterVolume(float value)
    {
        float normalized =
            Math.Clamp(
                value,
                0f,
                1f);
        long revision =
            Interlocked.Increment(
            ref _audioStateRevision);
        Volatile.Write(
            ref _audioVolumeRevision,
            revision);
        _volumeWritePending = true;
        if (_audioControl.QueueVolume(
                revision,
                normalized))
        {
            return;
        }

        _volumeWritePending = false;
        SetDisplayedVolume(
            _confirmedMasterVolume);
    }

    private void QueueMuted(bool value)
    {
        long revision =
            Interlocked.Increment(
            ref _audioStateRevision);
        Volatile.Write(
            ref _audioMuteRevision,
            revision);
        _muteWritePending = true;
        if (_audioControl.QueueMuted(
                revision,
                value))
        {
            return;
        }

        _muteWritePending = false;
        SetDisplayedMuted(
            _confirmedMuted);
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
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .ThemeModeKey,
                normalized);
        }
    }

    partial void OnDisableHotZoneInFullscreenChanged(bool value)
    {
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .FullscreenHotZoneKey,
                value.ToString());
        }
    }

    partial void OnEnableTaskbarSlotHotkeysChanged(
        bool value)
    {
        SetTaskbarSlotShortcutDisabled();
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .TaskbarSlotHotkeysKey,
                value.ToString());
        }
    }

    partial void OnDisplayTargetModeChanged(
        string value)
    {
        if (_refreshingDisplayTargetOptions)
            return;

        string normalized =
            ShellDisplayTarget.NormalizeValue(
                value);
        if (!string.Equals(
                normalized,
                value,
                StringComparison.Ordinal))
        {
            DisplayTargetMode = normalized;
            return;
        }

        DisplayTargetChanged?.Invoke();
        RefreshDisplayTargetOptions();
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .DisplayTargetModeKey,
                normalized);
        }
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
        if (destination is not (
                "Dashboard"
                or "Tasks"
                or "Pomodoro"
                or "Files"
                or "OKR"
                or "AI"))
        {
            return;
        }

        long navigationRevision =
            Interlocked.Increment(
                ref _workspaceNavigationRevision);
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
                    _pomodoroViewModel.SessionPersisted +=
                        PomodoroViewModel_SessionPersisted;
                }
                CurrentViewModel = _pomodoroViewModel;
                CurrentSectionTitle = "番茄钟";
                break;
            case "Files":
                CurrentViewModel =
                    _fileOrganizerViewModel != null
                        ? _fileOrganizerViewModel
                        : _fileOrganizerLoadingViewModel;
                CurrentSectionTitle = "桌面收纳";
                if (_fileOrganizerViewModel == null)
                {
                    _ = LoadFileOrganizerWorkspaceAsync(
                        navigationRevision);
                }
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
        }

        LastWorkspace = destination;
        WorkspaceRequested?.Invoke(destination);
    }

    private async Task LoadFileOrganizerWorkspaceAsync(
        long navigationRevision)
    {
        try
        {
            FileOrganizerViewModel viewModel =
                await _fileOrganizerInitialization;
            if (_isDisposed)
            {
                viewModel.Dispose();
                return;
            }

            _fileOrganizerViewModel ??=
                viewModel;
            if (WorkspaceLoadApplyPolicy.CanApply(
                    navigationRevision,
                    Volatile.Read(
                        ref _workspaceNavigationRevision),
                    _isDisposed))
            {
                CurrentViewModel =
                    _fileOrganizerViewModel;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "准备桌面收纳失败："
                + ex);
            if (WorkspaceLoadApplyPolicy.CanApply(
                    navigationRevision,
                    Volatile.Read(
                        ref _workspaceNavigationRevision),
                    _isDisposed))
            {
                _fileOrganizerLoadingViewModel.ShowError(
                    "桌面收纳暂时无法载入；"
                    + "请收起后重试启动 FocusPanel。");
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ExecuteSearchResult(
        ShellSearchResult? result)
    {
        if (!string.IsNullOrWhiteSpace(
                result?.CalculationResult))
        {
            bool copied =
                await _clipboard
                    .TrySetTextAsync(
                        result
                            .CalculationResult);
            if (_isDisposed)
                return;
            if (copied)
            {
                IsSearchOpen = false;
                return;
            }

            SystemActionMessage =
                "计算结果无法写入剪贴板。"
                + "剪贴板可能正被其他程序占用，请稍后重试。";
            CloseTransientPanels();
            IsStatusCenterOpen = true;
            return;
        }

        if (result?.ShellAction
            is WindowsShellAction shellAction)
        {
            await ExecuteShellSearchActionAsync(
                shellAction,
                result.DisplayName);
            return;
        }

        if (result?.ManagementTool
            is SystemManagementTool tool)
        {
            await RunSystemActionAsync(
                () => _systemStatus
                    .OpenManagementTool(
                        tool),
                $"无法打开“{result.DisplayName}”。"
                + "当前账户权限或系统版本可能不支持该入口。");
            return;
        }

        if (result?.Window is WindowReference window)
        {
            bool succeeded =
                SystemActionExecution.Try(
                    () => _windowTracker.Activate(
                        window.Handle));
            CompleteTaskbarWindowAction(
                succeeded,
                $"无法切换到“{window.Title}”。窗口可能已经关闭，"
                + "或 Windows 暂时阻止了前台切换。");
            if (succeeded)
                IsSearchOpen = false;
            return;
        }

        if (result?.Application is not AppLaunchItem app)
            return;
        if (await TryLaunchAppAsync(app))
            IsSearchOpen = false;
    }

    private async Task
        ExecuteShellSearchActionAsync(
            WindowsShellAction action,
            string displayName)
    {
        Func<bool> operation =
            action switch
            {
                WindowsShellAction.RunDialog =>
                    _systemStatus.OpenRunDialog,
                WindowsShellAction.QuickSettings =>
                    _systemStatus.OpenQuickSettings,
                WindowsShellAction.Notifications =>
                    _systemStatus.OpenNotifications,
                WindowsShellAction.InputSwitcher =>
                    _systemStatus.OpenInputSwitcher,
                WindowsShellAction.TaskView =>
                    _systemStatus.OpenTaskView,
                WindowsShellAction.Widgets =>
                    _systemStatus.OpenWidgets,
                WindowsShellAction.ShowDesktop =>
                    _systemStatus.ShowDesktop,
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        null)
            };

        await RunSystemActionAsync(
            operation,
            $"无法执行“{displayName}”。"
            + "Windows Shell 可能暂时不可用。");
    }

    [RelayCommand]
    private async Task ToggleSearchPin(
        ShellSearchResult? result)
    {
        if (result?.Application is not AppLaunchItem app)
            return;

        if (!await TrySetPinnedAsync(
                app,
                !app.IsPinned))
        {
            return;
        }
        if (_isDisposed)
            return;
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    internal async Task MoveTaskbarApp(
        TaskbarAppItem source,
        TaskbarAppItem target,
        TaskbarDropPlacement placement)
    {
        if (ReferenceEquals(source, target))
            return;

        AppLaunchItem? launch = source.CreateLaunchItem();
        if (launch == null)
            return;
        if (!source.IsPinned
            && !await TrySetPinnedAsync(
                launch,
                true))
        {
            return;
        }

        AppLaunchItem? relativeTarget =
            target.IsPinned
                ? target.CreateLaunchItem()
                : null;
        if (target.IsPinned
            && relativeTarget == null)
        {
            return;
        }
        if (!await TryMovePinnedRelativeAsync(
                launch,
                relativeTarget,
                placement))
        {
            RefreshTaskbarApps();
            RefreshSearchResults();
            return;
        }
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    [RelayCommand(
        CanExecute =
            nameof(CanMoveTaskbarAppUp))]
    private async Task MoveTaskbarAppUp(
        TaskbarAppItem? task) =>
        await MoveTaskbarAppByOffset(
            task,
            -1);

    private bool CanMoveTaskbarAppUp(
        TaskbarAppItem? task) =>
        CanMoveTaskbarAppByOffset(
            task,
            -1);

    [RelayCommand(
        CanExecute =
            nameof(CanMoveTaskbarAppDown))]
    private async Task MoveTaskbarAppDown(
        TaskbarAppItem? task) =>
        await MoveTaskbarAppByOffset(
            task,
            1);

    private bool CanMoveTaskbarAppDown(
        TaskbarAppItem? task) =>
        CanMoveTaskbarAppByOffset(
            task,
            1);

    private bool CanMoveTaskbarAppByOffset(
        TaskbarAppItem? task,
        int offset)
    {
        if (_isDisposed
            || task?.IsPinned != true)
        {
            return false;
        }

        int index = TaskbarApps
            .TakeWhile(item =>
                !ReferenceEquals(
                    item,
                    task))
            .Count(item =>
                item.IsPinned);
        int pinnedCount =
            TaskbarApps.Count(item =>
                item.IsPinned);
        return TaskbarPinnedStepPolicy
            .GetTargetIndex(
                index,
                pinnedCount,
                offset)
            .HasValue;
    }

    private async Task MoveTaskbarAppByOffset(
        TaskbarAppItem? task,
        int offset)
    {
        if (!CanMoveTaskbarAppByOffset(
                task,
                offset))
        {
            return;
        }

        AppLaunchItem? launch =
            task!.CreateLaunchItem();
        if (launch == null)
            return;
        if (!await TryMovePinnedByOffsetAsync(
                launch,
                offset))
        {
            RefreshTaskbarApps();
            RefreshSearchResults();
            return;
        }

        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ActivateTaskbarApp(
        TaskbarAppItem? task)
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
            await TryLaunchAppAsync(launch);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LaunchNewTaskbarApp(
        TaskbarAppItem? task)
    {
        AppLaunchItem? launch = task?.CreateLaunchItem();
        if (launch != null)
            await TryLaunchAppAsync(launch);
    }

    [RelayCommand]
    private async Task ToggleTaskbarPin(
        TaskbarAppItem? task)
    {
        if (task == null)
            return;

        if (task.IsPinned)
        {
            foreach (AppLaunchItem launch in task.PinnedLaunches)
            {
                if (!await TrySetPinnedAsync(
                        launch,
                        false))
                {
                    break;
                }
            }
        }
        else
        {
            AppLaunchItem? launch = task.CreateLaunchItem();
            if (launch == null)
                return;
            await TrySetPinnedAsync(
                launch,
                true);
        }
        if (_isDisposed)
            return;
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
        SelectCalendarDate(today);
        RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void SelectCalendarDate(CalendarDayItem? item)
    {
        if (item == null)
            return;

        if (!item.IsCurrentMonth)
        {
            DisplayedCalendarMonth =
                new DateTime(
                    item.Date.Year,
                    item.Date.Month,
                    1);
        }
        SelectCalendarDate(item.Date);
        if (!item.IsCurrentMonth)
            RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void NavigateCalendar(
        CalendarNavigationAction action)
    {
        DateTime target =
            CalendarKeyboardNavigationPolicy
                .GetTargetDate(
                    SelectedCalendarDate,
                    action,
                    DateTime.Today);
        bool monthChanged =
            target.Year
                != DisplayedCalendarMonth.Year
            || target.Month
                != DisplayedCalendarMonth.Month;
        if (monthChanged)
        {
            DisplayedCalendarMonth =
                new DateTime(
                    target.Year,
                    target.Month,
                    1);
        }

        SelectCalendarDate(target);
        if (monthChanged)
            RequestTaskSummaryRefresh();
    }

    [RelayCommand]
    private void ToggleFocusCenter()
    {
        bool open = !IsFocusCenterOpen;
        CloseTransientPanels();
        IsFocusCenterOpen = open;
    }

    [RelayCommand]
    private void ToggleWorkspacePin() =>
        IsWorkspacePinned =
            !IsWorkspacePinned;

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
        RequestProtectedVisibilityRefresh();
        bool open = !IsSettingsOpen;
        CloseTransientPanels();
        IsSettingsOpen = open;
    }

    private void
        RequestProtectedVisibilityRefresh()
    {
        if (_isDisposed)
            return;

        _protectedVisibilityRefresh.Request();
    }

    private async Task
        ApplyProtectedVisibilityAsync(
            bool showsProtectedSystemFiles,
            CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (!_isDisposed
                    && !cancellationToken
                        .IsCancellationRequested)
                {
                    ShowsProtectedSystemFiles =
                        showsProtectedSystemFiles;
                }
            },
            DispatcherPriority.Background,
            cancellationToken);
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
        IsMuted = !IsMuted;

    public void AdjustMasterVolume(float step)
    {
        float requested = Math.Clamp(
            MasterVolume + step,
            0f,
            1f);
        MasterVolume = requested;
        if (requested > 0
            && IsMuted)
        {
            IsMuted = false;
        }
    }

    [RelayCommand]
    private async Task OpenQuickSettings()
        => await RunSystemActionAsync(
            _systemStatus.OpenQuickSettings,
            "无法唤起 Windows 快捷设置，请使用 Win+A。");

    [RelayCommand]
    private async Task OpenNotifications()
        => await RunSystemActionAsync(
            _systemStatus.OpenNotifications,
            "无法唤起 Windows 通知中心，请使用 Win+N。");

    [RelayCommand]
    private async Task OpenInputSwitcher()
        => await RunSystemActionAsync(
            _systemStatus.OpenInputSwitcher,
            "无法唤起输入法切换器，请使用 Win+Space。");

    [RelayCommand]
    private async Task OpenStartMenu()
        => await RunSystemActionAsync(
            _systemStatus.OpenStartMenu,
            "无法唤起开始菜单，请按 Windows 键。");

    [RelayCommand]
    private async Task OpenTaskView()
        => await RunSystemActionAsync(
            _systemStatus.OpenTaskView,
            "无法唤起任务视图，请使用 Win+Tab。");

    [RelayCommand]
    private async Task SwitchVirtualDesktop(
        VirtualDesktopDirection direction)
        => await RunSystemActionAsync(
            () => _systemStatus
                .SwitchVirtualDesktop(
                    direction),
            direction
            == VirtualDesktopDirection.Previous
                ? "无法切换到上一个虚拟桌面，请使用 Win+Ctrl+←。"
                : "无法切换到下一个虚拟桌面，请使用 Win+Ctrl+→。");

    [RelayCommand]
    private async Task CreateVirtualDesktop()
        => await RunSystemActionAsync(
            _systemStatus
                .CreateVirtualDesktop,
            "无法新建虚拟桌面，请使用 Win+Ctrl+D。");

    [RelayCommand]
    private async Task CloseCurrentVirtualDesktop()
        => await RunSystemActionAsync(
            _systemStatus
                .CloseCurrentVirtualDesktop,
            "无法关闭当前虚拟桌面，请使用 Win+Ctrl+F4。");

    [RelayCommand]
    private async Task OpenWindowsSearch()
        => await RunSystemActionAsync(
            _systemStatus.OpenWindowsSearch,
            "无法唤起 Windows 搜索，请使用 Win+S。");

    [RelayCommand]
    private async Task OpenWidgets()
        => await RunSystemActionAsync(
            _systemStatus.OpenWidgets,
            "无法唤起 Windows 小组件，请使用 Win+W。");

    [RelayCommand]
    private async Task OpenRunDialog()
        => await RunSystemActionAsync(
            _systemStatus.OpenRunDialog,
            "无法唤起运行对话框，请使用 Win+R。");

    [RelayCommand]
    private async Task OpenManagementTool(
        SystemManagementTool tool)
        => await RunSystemActionAsync(
            () => _systemStatus
                .OpenManagementTool(tool),
            "无法打开所选 Windows 管理工具。当前账户权限或系统版本可能不支持该入口。");

    [RelayCommand]
    private async Task OpenPowerSettings()
        => await RunSystemActionAsync(
            _systemStatus.OpenPowerSettings,
            "无法打开 Windows 电源设置。");

    [RelayCommand]
    private async Task ShowDesktop()
        => await RunSystemActionAsync(
            _systemStatus.ShowDesktop,
            "无法显示桌面，请使用 Win+D。");

    [RelayCommand]
    private async Task LockComputer()
        => await RunSystemActionAsync(
            _systemStatus.Lock,
            "Windows 拒绝锁定当前会话，请使用 Win+L。");

    [RelayCommand]
    private async Task SleepComputer()
        => await RunSystemActionAsync(
            _systemStatus.Sleep,
            "Windows 拒绝进入睡眠，当前电源策略可能不支持该操作。");

    [RelayCommand]
    private async Task RestartComputer()
    {
        if (FocusDialogService.Show("确定要立即重启电脑吗？", "重启电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            await RunSystemActionAsync(
                _systemStatus.Restart,
                "无法启动系统重启，当前账户权限或系统策略可能阻止了操作。");
        }
    }

    [RelayCommand]
    private async Task ShutdownComputer()
    {
        if (FocusDialogService.Show("确定要立即关闭电脑吗？", "关闭电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            await RunSystemActionAsync(
                _systemStatus.Shutdown,
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
        QueueShellPreference(
            ShellPreferenceRepository
                .FirstRunAcceptedKey,
            bool.TrueString);
        QueueShellPreference(
            ShellPreferenceRepository
                .ReplacementEnabledKey,
            bool.FalseString);
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
        QueueShellPreference(
            ShellPreferenceRepository
                .FirstRunAcceptedKey,
            bool.TrueString);
        QueueShellPreference(
            ShellPreferenceRepository
                .ReplacementEnabledKey,
            enabled.ToString());
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
    private async Task RestoreDatabase()
    {
        var result = FocusDialogService.Show(
            "确定要从最新备份恢复数据库吗？\n任务、番茄钟、桌面收纳和 OKR 数据都会回到备份时的状态，应用将立即重启。",
            "恢复数据库",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        string executable;
        try
        {
            executable = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法定位 FocusPanel 可执行文件。");
        }
        catch (Exception ex)
        {
            FocusDialogService.Show($"无法重启应用：{ex.Message}", "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        int processId =
            Environment.ProcessId;
        SystemActionCompletion completion =
            await _systemActions.ExecuteAsync(
                () => AppLaunchExecution.TryStart(
                    new ProcessStartInfo(
                        executable,
                        $"--restore-after-exit {processId}")
                    {
                        UseShellExecute = true
                    }));
        if (_isDisposed
            || !_systemActions.IsCurrent(
                completion.Revision))
        {
            return;
        }
        if (completion.Succeeded)
        {
            RequestClose?.Invoke();
            return;
        }

        FocusDialogService.Show(
            "无法启动数据库恢复交接进程。"
            + "系统任务栏和当前数据库保持不变。",
            "恢复失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public async Task CheckForUpdatesInBackgroundAsync()
    {
        if (IsUpdateBusy)
            return;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        try
        {
            UpdateStatus = "正在从 GitHub Releases 自动检查更新…";
            AppUpdateInfo? update = await _updateService.CheckForUpdateAsync();
            if (!_updateService.CanUpdate)
            {
                ApplyUpdateAvailability(null);
                UpdateStatus =
                    "当前为开发运行版；安装发布包后可一键更新";
                return;
            }

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

        IsUpdateBusy = true;
        UpdateProgress = 0;
        try
        {
            UpdateStatus = "正在检查更新…";
            AppUpdateInfo? update = await _updateService.CheckForUpdateAsync();
            if (!_updateService.CanUpdate)
            {
                ApplyUpdateAvailability(null);
                UpdateStatus =
                    "开发运行版不能原地更新，请先安装 Setup.exe 发布包。";
                return;
            }

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
            Func<Task>? applyUpdate =
                RequestApplyUpdate;
            if (applyUpdate != null)
                await applyUpdate();
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
    private async Task OpenUpdateDownloadPage()
    {
        SystemActionCompletion completion =
            await _systemActions.ExecuteAsync(
                _updateService.OpenDownloadPage);
        if (_isDisposed
            || !_systemActions.IsCurrent(
                completion.Revision))
        {
            return;
        }

        UpdateStatus = completion.Succeeded
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
    {
        TaskbarAppCollectionSynchronizer.Synchronize(
            TaskbarApps,
            _taskbarComposer.Compose(_appCatalog.GetPinned(), _windowTracker.GetSnapshot()));
        for (int index = 0;
             index < TaskbarApps.Count;
             index++)
        {
            TaskbarApps[index]
                .SetShortcutSlot(
                    index
                        < TaskbarSlotHotkeyPolicy
                            .SlotCount
                        ? index + 1
                        : null);
        }
        ActiveTaskbarIdentity =
            TaskbarApps.FirstOrDefault(
                item => item.IsActive)
            ?.IdentityKey;
    }

    private void RefreshSearchResults()
    {
        string? selectedKey =
            SelectedSearchResult?.StableKey;
        IReadOnlyList<AppLaunchItem> applications =
            _appCatalog.Search(
                SearchQuery,
                ShellSearchPolicy.DefaultLimit);
        IReadOnlyList<ShellSearchResult> results =
            ShellSearchPolicy.Compose(
                applications,
                _windowTracker.GetSnapshot(),
                SearchQuery);
        ReplaceCollection(
            SearchResults,
            results);
        SelectedSearchResult = SearchResults.FirstOrDefault(
            item => !string.IsNullOrWhiteSpace(selectedKey)
                && string.Equals(
                    item.StableKey,
                    selectedKey,
                    StringComparison.OrdinalIgnoreCase))
            ?? SearchResults.FirstOrDefault();
        OnPropertyChanged(
            nameof(IsAppSearchStatusVisible));
        OnPropertyChanged(
            nameof(AppSearchStatusText));
    }

    private void OnWindowSnapshotChanged(
        object? sender,
        EventArgs e)
    {
        RefreshTaskbarApps();
        RefreshSearchResults();
    }
    private void Dashboard_NavigationRequested(
        string destination) =>
        Navigate(destination);

    private void PomodoroViewModel_SessionCompleted(
        object? sender,
        PomodoroCompletedEventArgs e)
        => PomodoroCompleted?.Invoke(
            e.DurationMinutes);

    private void PomodoroViewModel_SessionPersisted(
        object? sender,
        EventArgs e)
        => RequestTaskSummaryRefresh();

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
        bool audioWritePendingBeforeCapture =
            _volumeWritePending
            || _muteWritePending;
        SystemStatusSnapshot snapshot =
            _systemStatus.GetStatusSnapshot();
        bool audioWritePendingAfterCapture =
            _volumeWritePending
            || _muteWritePending;
        return new PendingSystemStatusSnapshot(
            snapshot,
            audioRevision,
            audioWritePendingBeforeCapture
            || audioWritePendingAfterCapture);
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
        if (!pending.AudioWritePending
            && SystemStatusRefreshPolicy.ShouldApplyAudio(
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

    private void SetDisplayedVolume(
        float value)
    {
        _updatingAudioState = true;
        try
        {
            MasterVolume =
                Math.Clamp(
                    value,
                    0f,
                    1f);
        }
        finally
        {
            _updatingAudioState = false;
        }
    }

    private void SetDisplayedMuted(
        bool value)
    {
        _updatingAudioState = true;
        try
        {
            IsMuted = value;
        }
        finally
        {
            _updatingAudioState = false;
        }
    }

    private void OnAudioControlCompleted(
        AudioControlOutcome outcome)
    {
        _uiDispatcher.BeginInvoke(
            () =>
                ApplyAudioControlOutcome(
                    outcome),
            DispatcherPriority.Background);
    }

    private void ApplyAudioControlOutcome(
        AudioControlOutcome outcome)
    {
        if (_isDisposed)
            return;

        AudioControlCompletion completion =
            AudioControlCompletionPolicy.Apply(
                new AudioControlConfirmationState(
                    _confirmedMasterVolume,
                    _confirmedMuted,
                    Volatile.Read(
                        ref _audioVolumeRevision),
                    Volatile.Read(
                        ref _audioMuteRevision),
                    _volumeWritePending,
                    _muteWritePending),
                outcome);
        _confirmedMasterVolume =
            completion.State.ConfirmedVolume;
        _confirmedMuted =
            completion.State.ConfirmedMuted;
        _volumeWritePending =
            completion.State.VolumePending;
        _muteWritePending =
            completion.State.MutePending;
        if (completion.DisplayVolume
            is float displayVolume)
        {
            SetDisplayedVolume(
                displayVolume);
        }
        if (completion.DisplayMuted
            is bool displayMuted)
        {
            SetDisplayedMuted(
                displayMuted);
        }

        if (completion.CurrentFailed)
        {
            ReportAudioFailure(
                "无法调整音量或静音。请检查默认音频输出设备，或使用 Win+A。");
            return;
        }

        if (completion.CurrentSucceeded)
        {
            IsAudioAvailable = true;
            AudioStatusText = string.Empty;
            SystemActionMessage = string.Empty;
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

    private void ApplyStartupPreference(
        bool enable)
    {
        StartupStatus = enable
            ? "正在启用随 Windows 启动…"
            : "正在关闭随 Windows 启动…";
        _ = ApplyStartupPreferenceAsync(
            enable);
    }

    private async Task LoadStartupStateAsync()
    {
        AutoStartupCompletion completion =
            await _autoStartup.ReadAsync();
        if (_isDisposed
            || !_autoStartup.IsCurrent(
                completion.Revision))
        {
            return;
        }

        SetStartupDisplayState(
            completion.Enabled);
        StartupStatus = completion.Succeeded
            ? completion.Enabled
                ? "已设置为随 Windows 启动"
                : "当前不会随 Windows 启动"
            : "无法读取 Windows 启动项，"
                + "当前按未启用显示。";
    }

    private async Task ApplyStartupPreferenceAsync(
        bool enable)
    {
        AutoStartupCompletion completion =
            await _autoStartup.SetAsync(
                enable);
        if (_isDisposed
            || !_autoStartup.IsCurrent(
                completion.Revision))
        {
            return;
        }

        SetStartupDisplayState(
            completion.Enabled);
        StartupStatus = completion.Succeeded
            ? completion.Enabled
                ? "已设置为随 Windows 启动"
                : "当前不会随 Windows 启动"
            : string.IsNullOrWhiteSpace(
                completion.Error)
                ? "无法更新 Windows 启动项。"
                : completion.Error;
    }

    private void SetStartupDisplayState(
        bool enabled)
    {
        _updatingStartupState = true;
        try
        {
            StartWithWindows =
                enabled;
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

    private async Task RunSystemActionAsync(
        Func<bool> action,
        string error)
    {
        SystemActionCompletion completion =
            await _systemActions.ExecuteAsync(
                action);
        if (_isDisposed
            || !_systemActions.IsCurrent(
                completion.Revision))
        {
            return;
        }

        CompleteSystemAction(
            completion.Succeeded,
            error);
    }

    private async Task<bool> TryLaunchAppAsync(
        AppLaunchItem app)
    {
        AppLaunchCompletion completion =
            await _appLaunch.LaunchAsync(app);
        if (_isDisposed
            || !_appLaunch.IsCurrent(
                completion.Revision))
        {
            return false;
        }

        if (completion.Succeeded)
        {
            SystemActionMessage =
                string.Empty;
            return true;
        }

        SystemActionMessage =
            $"无法启动“{app.DisplayName}”。应用可能已卸载，"
            + "或固定目标已经移动；请在搜索中重新固定。";
        CloseTransientPanels();
        IsStatusCenterOpen = true;
        return false;
    }

    private async Task<bool> TrySetPinnedAsync(
        AppLaunchItem app,
        bool pinned)
    {
        bool succeeded;
        try
        {
            succeeded =
                await _appCatalog.SetPinnedAsync(
                    app,
                    pinned);
        }
        catch
        {
            succeeded = false;
        }
        if (_isDisposed)
            return false;
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

    private async Task<bool>
        TryMovePinnedRelativeAsync(
        AppLaunchItem app,
        AppLaunchItem? target,
        TaskbarDropPlacement placement)
    {
        bool succeeded;
        try
        {
            succeeded =
                await _appCatalog
                    .MovePinnedRelativeAsync(
                    app,
                    target,
                    placement);
        }
        catch
        {
            succeeded = false;
        }
        if (_isDisposed)
            return false;
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

    private async Task<bool>
        TryMovePinnedByOffsetAsync(
            AppLaunchItem app,
            int offset)
    {
        bool succeeded;
        try
        {
            succeeded =
                await _appCatalog
                    .MovePinnedByOffsetAsync(
                        app,
                        offset);
        }
        catch
        {
            succeeded = false;
        }
        if (_isDisposed)
            return false;
        if (succeeded)
        {
            SystemActionMessage =
                string.Empty;
            return true;
        }

        ReportTaskbarActionFailure(
            $"无法移动“{app.DisplayName}”。"
            + "固定状态已经保留，请稍后重试。");
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

    private void QueueShellPreference(
        string key,
        string value)
    {
        if (_isDisposed
            || _shellPreferences.QueueSave(
                key,
                value))
        {
            return;
        }

        ReportShellPreferenceFailure();
    }

    private void OnShellPreferenceSaveFailed(
        string key,
        Exception error)
    {
        Debug.WriteLine(
            $"Shell 设置保存失败（{key}）：{error}");
        _uiDispatcher.BeginInvoke(
            ReportShellPreferenceFailure);
    }

    private void ReportShellPreferenceFailure()
    {
        if (_isDisposed)
            return;

        SystemActionMessage =
            "设置暂时无法保存；当前会话仍然有效，"
            + "请稍后重试。";
    }

    internal Task DisposeAsync()
    {
        if (_disposeTask != null)
            return _disposeTask;

        _disposeTask =
            DisposeCoreAsync();
        return _disposeTask;
    }

    private async Task DisposeCoreAsync()
    {
        _isDisposed = true;
        _audioControl.Completed -=
            OnAudioControlCompleted;
        _shellPreferences.SaveFailed -=
            OnShellPreferenceSaveFailed;
        _clockTimer.Stop();
        _systemStatusTimer.Stop();
        _taskSummaryTimer.Stop();
        _updateCheckTimer.Stop();
        _systemStatusRefresh.Dispose();
        _taskSummaryRefresh.Dispose();
        _protectedVisibilityRefresh.Dispose();
        _windowTracker.SnapshotChanged -= OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged -= OnCatalogChanged;
        var completions =
            new List<Task>
            {
                _audioControl.CompleteAsync(),
                _autoStartup.CompleteAsync(),
                _shellPreferences.CompleteAsync()
            };
        if (_tasksViewModel != null)
        {
            completions.Add(
                _tasksViewModel
                    .DisposeAsync());
        }

        _okrViewModel?.Dispose();
        _aiAssistantViewModel?.Dispose();
        if (_fileOrganizerViewModel != null)
        {
            completions.Add(
                _fileOrganizerViewModel
                    .DisposeAsync());
        }
        else
        {
            completions.Add(
                DisposePreparedFileOrganizerAsync());
        }

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
            _pomodoroViewModel.SessionPersisted -=
                PomodoroViewModel_SessionPersisted;
            completions.Add(
                _pomodoroViewModel
                    .DisposeAsync());
        }

        try
        {
            await Task.WhenAll(
                    completions)
                .ConfigureAwait(false);
        }
        finally
        {
            _audioControl.Dispose();
            _shellPreferences.Dispose();
        }
    }

    public void Dispose() =>
        DisposeAsync()
            .GetAwaiter()
            .GetResult();

    private async Task
        DisposePreparedFileOrganizerAsync()
    {
        try
        {
            FileOrganizerViewModel viewModel =
                await _fileOrganizerInitialization
                    .ConfigureAwait(false);
            await viewModel
                .DisposeAsync()
                .ConfigureAwait(false);
        }
        catch
        {
            // Initialization failures are already surfaced by the loader.
        }
    }

    private readonly record struct
        PendingSystemStatusSnapshot(
            SystemStatusSnapshot Snapshot,
            long AudioRevision,
            bool AudioWritePending);
}
