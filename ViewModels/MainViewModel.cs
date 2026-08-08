using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security;
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
    private readonly IDisplayBrightnessService
        _brightness;
    private readonly IApplicationAudioSessionService
        _applicationAudio;
    private readonly ISystemRadioService _radios;
    private readonly IWifiNetworkService
        _wifiNetworks;
    private readonly IBluetoothDeviceService
        _bluetoothDevices;
    private readonly IAppUpdateService _updateService;
    private readonly FocusNotificationCenter
        _notificationCenter;
    private readonly IDesktopItemVisibilityService _desktopVisibility;
    private readonly IShellPreferenceRepository
        _shellPreferences;
    private readonly AudioControlCoordinator
        _audioControl;
    private readonly BrightnessControlCoordinator
        _brightnessControl;
    private readonly
        ApplicationAudioControlCoordinator
        _applicationAudioControl;
    private readonly InFlightTaskTracker
        _radioOperations = new();
    private readonly InFlightTaskTracker
        _wifiNetworkOperations = new();
    private readonly InFlightTaskTracker
        _bluetoothDeviceOperations = new();
    private readonly AppLaunchCoordinator
        _appLaunch;
    private readonly ElevatedAppLaunchCoordinator
        _elevatedAppLaunch;
    private readonly IAppLocationService
        _appLocation;
    private readonly IPanelRunService
        _panelRun;
    private readonly SystemActionCoordinator
        _systemActions;
    private readonly ClipboardTextService
        _clipboard;
    private readonly AutoStartupCoordinator
        _autoStartup;
    private readonly TaskService _taskService;
    private readonly TaskQuickCaptureCoordinator
        _taskCapture;
    private readonly TaskSearchCoordinator
        _taskSearch;
    private readonly TaskbarAppComposer _taskbarComposer = new();
    private readonly WindowFocusSessionCoordinator
        _windowFocusSession = new();
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
    private bool _applyingPanelPlacement;
    private readonly Task<FileOrganizerViewModel>
        _fileOrganizerInitialization;
    private readonly WorkspaceLoadingViewModel
        _fileOrganizerLoadingViewModel =
            new("正在准备桌面收纳…");
    private DashboardViewModel? _dashboardViewModel;
    private TasksViewModel? _tasksViewModel;
    private PomodoroViewModel? _pomodoroViewModel;
    private TaskbarSlotHotkeyRegistration
        _taskbarSlotHotkeyRegistration =
            new(
                Array.Empty<
                    TaskbarSlotHotkeyBinding>());
    private FileOrganizerViewModel? _fileOrganizerViewModel;
    private AIAssistantViewModel? _aiAssistantViewModel;
    private bool _updatingAudioState;
    private long _audioStateRevision;
    private long _audioVolumeRevision;
    private long _audioMuteRevision;
    private volatile bool _volumeWritePending;
    private volatile bool _muteWritePending;
    private bool _updatingBrightnessState;
    private long _brightnessRevision;
    private volatile bool _brightnessWritePending;
    private int _confirmedBrightnessPercent;
    private long _applicationAudioRevision;
    private long _wifiNetworkRevision;
    private long _bluetoothDeviceRevision;
    private readonly ConcurrentDictionary<
        string,
        long> _applicationAudioRevisions =
            new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<
        string,
        byte> _applicationAudioWritePending =
            new(StringComparer.Ordinal);
    private long _taskSummaryMonthTicks;
    private float _confirmedMasterVolume;
    private bool _confirmedMuted;
    private bool _updatingStartupState;
    private string? _lastNotifiedUpdateVersion;
    private IntPtr
        _lastActiveExternalWindowHandle;
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
    private IReadOnlyList<TaskSearchItem>
        _taskSearchItems =
            Array.Empty<TaskSearchItem>();
    private IReadOnlyList<string>
        _recentAppIdentities =
            Array.Empty<string>();
    private readonly List<IntPtr>
        _recentWindowHandles = new();
    private long _recentAppHistoryRevision;

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
    [NotifyPropertyChangedFor(
        nameof(IsTaskQuickCaptureDraft))]
    [NotifyPropertyChangedFor(
        nameof(IsPanelRunDraft))]
    [NotifyPropertyChangedFor(
        nameof(SearchPanelTitle))]
    [NotifyPropertyChangedFor(
        nameof(SearchPanelInstruction))]
    [NotifyPropertyChangedFor(
        nameof(AreSearchSuggestionsVisible))]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsAllSearchScope))]
    [NotifyPropertyChangedFor(
        nameof(IsWindowSearchScope))]
    [NotifyPropertyChangedFor(
        nameof(IsWindowDisplayFilterVisible))]
    [NotifyPropertyChangedFor(
        nameof(IsApplicationSearchScope))]
    [NotifyPropertyChangedFor(
        nameof(IsSystemSearchScope))]
    [NotifyPropertyChangedFor(
        nameof(IsTaskQuickCaptureDraft))]
    [NotifyPropertyChangedFor(
        nameof(IsPanelRunDraft))]
    [NotifyPropertyChangedFor(
        nameof(SearchPanelTitle))]
    [NotifyPropertyChangedFor(
        nameof(SearchPanelInstruction))]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    [NotifyPropertyChangedFor(
        nameof(AreSearchSuggestionsVisible))]
    [NotifyPropertyChangedFor(
        nameof(IsApplicationLauncherOpen))]
    [NotifyPropertyChangedFor(
        nameof(IsUnifiedSearchEntryActive))]
    [NotifyPropertyChangedFor(
        nameof(SearchPlaceholder))]
    private ShellSearchScope searchScope =
        ShellSearchScope.All;

    private bool _refreshingWindowDisplayFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsWindowDisplayFilterActive))]
    [NotifyPropertyChangedFor(
        nameof(SearchPanelTitle))]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    private string selectedWindowDisplayFilter =
        WindowDisplayOverviewPolicy
            .AllDisplaysValue;

    private string? _windowOverviewIdentityFilter;
    private string _windowOverviewApplicationName =
        string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(OpenWindowScopeLabel))]
    private int openWindowCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    [NotifyPropertyChangedFor(
        nameof(IsAppSearchStatusVisible))]
    private bool isAppCatalogLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsApplicationLauncherOpen))]
    [NotifyPropertyChangedFor(
        nameof(IsUnifiedSearchEntryActive))]
    private bool isSearchOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    private bool isTaskSearchLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(AppSearchStatusText))]
    private string taskSearchError =
        string.Empty;

    [ObservableProperty]
    private ShellSearchResult? selectedSearchResult;

    [ObservableProperty]
    private string? activeTaskbarIdentity;

    [ObservableProperty]
    private string? attentionTaskbarIdentity;

    [ObservableProperty]
    private int compactTaskbarStructureRevision;

    [ObservableProperty]
    private bool isCalendarOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsStatusEntryActive))]
    private bool isStatusCenterOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(FilteredPanelNotifications))]
    [NotifyPropertyChangedFor(
        nameof(HasFilteredPanelNotifications))]
    [NotifyPropertyChangedFor(
        nameof(PanelNotificationEmptyText))]
    private bool showUnreadPanelNotificationsOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(FilteredBackgroundApps))]
    [NotifyPropertyChangedFor(
        nameof(HasFilteredBackgroundApps))]
    [NotifyPropertyChangedFor(
        nameof(BackgroundAppResultSummary))]
    [NotifyPropertyChangedFor(
        nameof(BackgroundAppEmptyText))]
    private string backgroundAppQuery =
        string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(FilteredBackgroundApps))]
    [NotifyPropertyChangedFor(
        nameof(HasFilteredBackgroundApps))]
    [NotifyPropertyChangedFor(
        nameof(BackgroundAppResultSummary))]
    [NotifyPropertyChangedFor(
        nameof(BackgroundAppEmptyText))]
    [NotifyPropertyChangedFor(
        nameof(IsAllBackgroundAppScope))]
    [NotifyPropertyChangedFor(
        nameof(IsWindowBackgroundAppScope))]
    [NotifyPropertyChangedFor(
        nameof(IsBackgroundOnlyAppScope))]
    private BackgroundAppFilterScope
        backgroundAppScope =
            BackgroundAppFilterScope.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsDashboardWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsFilesWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsTasksWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsPomodoroWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsAiWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsSettingsWorkspaceActive))]
    private bool isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsStatusEntryActive))]
    private bool isPowerMenuOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(WorkspacePinActionText))]
    private bool isWorkspacePinned;

    [ObservableProperty]
    private bool isOnboardingVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(TaskbarHealthStatus))]
    [NotifyPropertyChangedFor(
        nameof(StatusCenterAutomationName))]
    private bool isReplacementEnabled;

    [ObservableProperty]
    private string replacementStatus = "Windows 任务栏保持显示";

    [ObservableProperty]
    private string replacementError = string.Empty;

    [ObservableProperty]
    private string replacementDiagnostics = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasTaskbarHealthWarning))]
    [NotifyPropertyChangedFor(
        nameof(TaskbarHealthStatus))]
    [NotifyPropertyChangedFor(
        nameof(StatusCenterAutomationName))]
    [NotifyPropertyChangedFor(
        nameof(TaskbarDiagnosticsActionText))]
    private bool hasReplacementDiagnostics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(TaskbarDiagnosticsActionText))]
    [NotifyPropertyChangedFor(
        nameof(CanInspectTaskbarReplacement))]
    private bool isTaskbarDiagnosticsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasTaskbarHealthWarning))]
    [NotifyPropertyChangedFor(
        nameof(TaskbarHealthStatus))]
    [NotifyPropertyChangedFor(
        nameof(StatusCenterAutomationName))]
    private bool isReplacementDiagnosticsHealthy;

    [ObservableProperty]
    private TaskbarReplacementStopReason? replacementStopReason;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasTaskbarHealthWarning))]
    [NotifyPropertyChangedFor(
        nameof(TaskbarHealthStatus))]
    [NotifyPropertyChangedFor(
        nameof(StatusCenterAutomationName))]
    private bool hasReplacementWarning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsOrganizerEntryActive))]
    [NotifyPropertyChangedFor(
        nameof(IsTasksEntryActive))]
    [NotifyPropertyChangedFor(
        nameof(IsDashboardWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsFilesWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsTasksWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsPomodoroWorkspaceActive))]
    [NotifyPropertyChangedFor(
        nameof(IsAiWorkspaceActive))]
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
    private string windowOverviewShortcutText =
        "正在注册窗口总览快捷键…";

    [ObservableProperty]
    private string windowFocusShortcutText =
        "正在注册窗口专注快捷键…";

    [ObservableProperty]
    private string taskbarSlotShortcutText =
        "九槽位全局快速键已关闭";

    [ObservableProperty]
    private string displayTargetMode =
        ShellDisplayTarget.OutermostRightValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(PanelVerticalAnchorLabel))]
    private string panelEdge =
        ShellPanelEdgePolicy.RightValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(PanelVerticalAnchorLabel))]
    private string panelVerticalAnchor =
        ShellPanelVerticalAnchorPolicy
            .CenterValue;

    [ObservableProperty]
    private int autoHideDelayMilliseconds =
        ShellAutoHideDelayPolicy
            .DefaultMilliseconds;

    [ObservableProperty]
    private int hotZoneDwellMilliseconds =
        EdgeHotZoneSensitivityPolicy
            .DefaultDwellMilliseconds;

    [ObservableProperty]
    private bool keepCompactDockVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(AudioCompactValueText))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private float masterVolume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(AudioCompactValueText))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioGlyph))]
    [NotifyPropertyChangedFor(nameof(AudioSummary))]
    [NotifyPropertyChangedFor(nameof(AudioToggleLabel))]
    [NotifyPropertyChangedFor(nameof(AudioCompactValueText))]
    [NotifyPropertyChangedFor(nameof(StatusCenterSummary))]
    [NotifyPropertyChangedFor(nameof(StatusCenterAutomationName))]
    private bool isAudioAvailable;

    [ObservableProperty]
    private string audioStatusText =
        "正在读取音频设备…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BrightnessSummary))]
    private int brightnessPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BrightnessSummary))]
    private bool isBrightnessAvailable;

    [ObservableProperty]
    private string brightnessStatusText =
        "正在读取内置显示器…";

    [ObservableProperty]
    private string applicationAudioStatusText =
        "正在读取应用音频会话…";

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
    [NotifyPropertyChangedFor(
        nameof(WiFiRadioActionText))]
    [NotifyPropertyChangedFor(
        nameof(CanToggleWiFiRadio))]
    private SystemRadioState wiFiRadioState =
        SystemRadioState.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BluetoothRadioActionText))]
    [NotifyPropertyChangedFor(
        nameof(CanToggleBluetoothRadio))]
    private SystemRadioState bluetoothRadioState =
        SystemRadioState.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(WiFiRadioActionText))]
    [NotifyPropertyChangedFor(
        nameof(CanToggleWiFiRadio))]
    private bool isWiFiRadioBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BluetoothRadioActionText))]
    [NotifyPropertyChangedFor(
        nameof(CanToggleBluetoothRadio))]
    private bool isBluetoothRadioBusy;

    [ObservableProperty]
    private string radioStatusText =
        "正在读取 Wi‑Fi 与蓝牙状态…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(WifiNetworkToggleText))]
    private bool isWifiNetworkListVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(WifiNetworkToggleText))]
    private bool isWifiNetworkBusy;

    [ObservableProperty]
    private string wifiNetworkStatusText =
        "按“查找网络”后由 Windows 请求位置权限并显示附近 Wi‑Fi";

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BluetoothDeviceToggleText))]
    private bool isBluetoothDeviceListVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(BluetoothDeviceToggleText))]
    private bool isBluetoothDeviceBusy;

    [ObservableProperty]
    private string bluetoothDeviceStatusText =
        "按“查找设备”后直接在 Panel 管理蓝牙设备";

    [ObservableProperty]
    private bool hasWifiLocationAccessWarning;

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
    private bool hasWindowFocusSession;

    [ObservableProperty]
    private string windowFocusSessionSummary =
        string.Empty;

    [ObservableProperty]
    private string windowFocusHiddenWindowCountText =
        string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasOpenTasks))]
    [NotifyPropertyChangedFor(
        nameof(OpenTaskCountBadgeText))]
    [NotifyPropertyChangedFor(
        nameof(TasksEntryAutomationName))]
    private int openTaskCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasCollectedDesktopItems))]
    [NotifyPropertyChangedFor(
        nameof(CollectedDesktopItemCountBadgeText))]
    [NotifyPropertyChangedFor(
        nameof(OrganizerEntryAutomationName))]
    private int collectedDesktopItemCount;

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
        IAppUpdateService updateService,
        IDisplayBrightnessService brightness,
        IApplicationAudioSessionService
            applicationAudio,
        ISystemRadioService radios,
        IWifiNetworkService wifiNetworks,
        IBluetoothDeviceService bluetoothDevices,
        FocusNotificationCenter? notificationCenter = null)
        : this(
            appCatalog,
            windowTracker,
            systemStatus,
            updateService,
            new ShellPreferenceRepository(),
            brightness: brightness,
            applicationAudio:
                applicationAudio,
            radios: radios,
            wifiNetworks: wifiNetworks,
            bluetoothDevices: bluetoothDevices,
            notificationCenter: notificationCenter)
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
            clipboard = null,
        TaskService?
            taskService = null,
        IDisplayBrightnessService?
            brightness = null,
        IApplicationAudioSessionService?
            applicationAudio = null,
        ISystemRadioService? radios = null,
        IWifiNetworkService?
            wifiNetworks = null,
        IBluetoothDeviceService?
            bluetoothDevices = null,
        ElevatedAppLaunchCoordinator?
            elevatedAppLaunch = null,
        IAppLocationService?
            appLocation = null,
        IPanelRunService?
            panelRun = null,
        FocusNotificationCenter?
            notificationCenter = null)
    {
        _appCatalog = appCatalog;
        _windowTracker = windowTracker;
        _systemStatus = systemStatus;
        _brightness =
            brightness
            ?? new DisplayBrightnessService();
        _applicationAudio =
            applicationAudio
            ?? new ApplicationAudioSessionService();
        _radios =
            radios
            ?? new SystemRadioService();
        _wifiNetworks =
            wifiNetworks
            ?? new WifiNetworkService();
        _bluetoothDevices =
            bluetoothDevices
            ?? new BluetoothDeviceService();
        _updateService = updateService;
        _notificationCenter =
            notificationCenter
            ?? new FocusNotificationCenter(
                new TransientFocusNotificationStore());
        _appLaunch =
            new AppLaunchCoordinator(
                _appCatalog.Launch);
        _elevatedAppLaunch =
            elevatedAppLaunch
            ?? new ElevatedAppLaunchCoordinator(
                new ElevatedAppLaunchService()
                    .Launch);
        _appLocation =
            appLocation
            ?? new AppLocationService();
        _panelRun =
            panelRun
            ?? new PanelRunService();
        _systemActions =
            systemActions
            ?? new SystemActionCoordinator();
        _clipboard =
            clipboard
            ?? new ClipboardTextService();
        _autoStartup =
            autoStartup
            ?? new AutoStartupCoordinator();
        _taskService =
            taskService
            ?? new TaskService();
        _taskCapture =
            new TaskQuickCaptureCoordinator(
                _taskService);
        _taskSearch =
            new TaskSearchCoordinator(
                _taskService);
        _audioControl =
            new AudioControlCoordinator(
                _systemStatus.TrySetMasterVolume,
                _systemStatus.TrySetMuted);
        _audioControl.Completed +=
            OnAudioControlCompleted;
        _brightnessControl =
            new BrightnessControlCoordinator(
                _brightness.TrySetBrightness);
        _brightnessControl.Completed +=
            OnBrightnessControlCompleted;
        _applicationAudioControl =
            new ApplicationAudioControlCoordinator(
                _applicationAudio.TrySetVolume,
                _applicationAudio.TrySetMuted);
        _applicationAudioControl.Completed +=
            OnApplicationAudioControlCompleted;
        _shellPreferences =
            shellPreferences
            ?? throw new ArgumentNullException(
                nameof(shellPreferences));
        _shellPreferences.SaveFailed +=
            OnShellPreferenceSaveFailed;
        _desktopVisibility = new WindowsDesktopItemVisibilityService();
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _notificationCenter.Changed +=
            NotificationCenter_Changed;
        _notificationCenter.PersistenceStatusChanged +=
            NotificationCenter_PersistenceStatusChanged;
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
        _windowTracker.RequestRefresh();
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
    public ObservableCollection<TaskbarAppItem>
        CompactTaskbarApps
    {
        get;
    } = new();
    public bool HasCompactTaskbarApps =>
        CompactTaskbarApps.Count > 0;
    public int RunningApplicationCount =>
        CompactTaskbarApps.Count(item =>
            item.WindowCount > 0);
    public string RunningApplicationSummary =>
        RunningApplicationCount == 0
            ? "任务栏暂无可切换窗口，单击刷新"
            : $"任务栏有 {RunningApplicationCount} 个可切换应用";
    public string PanelVerticalAnchorLabel =>
        (ShellPanelEdgePolicy.IsLeft(PanelEdge)
            ? "左"
            : "右")
        + " · "
        + (ShellPanelVerticalAnchorPolicy
            .Parse(PanelVerticalAnchor) switch
        {
            ShellPanelVerticalAnchor.Top => "上",
            ShellPanelVerticalAnchor.Bottom => "下",
            _ => "中"
        });
    public IReadOnlyList<TaskbarAppItem>
        FilteredBackgroundApps =>
            BackgroundAppFilterPolicy.Apply(
                TaskbarApps,
                BackgroundAppQuery,
                BackgroundAppScope);
    public bool HasFilteredBackgroundApps =>
        FilteredBackgroundApps.Count > 0;
    public string BackgroundAppResultSummary =>
        $"{FilteredBackgroundApps.Count} / {TaskbarApps.Count} 项";
    public string BackgroundAppEmptyText =>
        TaskbarApps.Count == 0
            ? "没有固定、运行或后台应用"
            : !string.IsNullOrWhiteSpace(
                BackgroundAppQuery)
                ? $"没有匹配“{BackgroundAppQuery.Trim()}”的应用"
                : BackgroundAppScope
                == BackgroundAppFilterScope.Windows
                ? "没有带可见窗口的应用"
                : BackgroundAppScope
                    == BackgroundAppFilterScope.Background
                    ? "没有纯后台应用"
                    : "没有匹配的应用";
    public bool IsAllBackgroundAppScope =>
        BackgroundAppScope
        == BackgroundAppFilterScope.All;
    public bool IsWindowBackgroundAppScope =>
        BackgroundAppScope
        == BackgroundAppFilterScope.Windows;
    public bool IsBackgroundOnlyAppScope =>
        BackgroundAppScope
        == BackgroundAppFilterScope.Background;
    public ReadOnlyObservableCollection<FocusNotificationItem>
        PanelNotifications =>
            _notificationCenter.Items;
    public bool HasPanelNotifications =>
        PanelNotifications.Count > 0;
    public IReadOnlyList<FocusNotificationItem>
        FilteredPanelNotifications =>
            PanelNotificationFilterPolicy.Apply(
                PanelNotifications,
                ShowUnreadPanelNotificationsOnly);
    public bool HasFilteredPanelNotifications =>
        !ShowUnreadPanelNotificationsOnly
            ? HasPanelNotifications
            : HasUnreadPanelNotifications;
    public string PanelNotificationEmptyText =>
        ShowUnreadPanelNotificationsOnly
            ? "没有未读通知"
            : "还没有 Panel 通知";
    public string PanelNotificationCountText =>
        $"共 {PanelNotifications.Count} 条 · "
        + $"{UnreadPanelNotificationCount} 条未读";
    public bool HasUnreadPanelNotifications =>
        _notificationCenter.UnreadCount > 0;
    public int UnreadPanelNotificationCount =>
        _notificationCenter.UnreadCount;
    public string PanelNotificationBadgeText =>
        UnreadPanelNotificationCount > 99
            ? "99+"
            : UnreadPanelNotificationCount.ToString(
                ChineseCulture);
    public string NotificationPersistenceError =>
        _notificationCenter.LastPersistenceError
        ?? string.Empty;
    public bool HasNotificationPersistenceError =>
        !string.IsNullOrWhiteSpace(
            NotificationPersistenceError);
    public bool IsStatusEntryActive =>
        IsStatusCenterOpen
        || IsPowerMenuOpen;
    public ObservableCollection<
        ApplicationAudioSessionItem>
        ApplicationAudioSessions
    {
        get;
    } = new();
    public bool HasApplicationAudioSessions =>
        ApplicationAudioSessions.Count > 0;
    public bool HasApplicationAudioSearchSuggestion =>
        ApplicationAudioSessions.Count > 0;
    public string ApplicationAudioSearchSuggestion
    {
        get
        {
            ApplicationAudioSessionItem? item =
                ApplicationAudioSessions
                    .FirstOrDefault(candidate =>
                        candidate.IsActive)
                ?? ApplicationAudioSessions
                    .FirstOrDefault();
            return item == null
                ? string.Empty
                : $"{item.DisplayName} 音量 30";
        }
    }
    public ObservableCollection<
        WifiNetworkSnapshot> WifiNetworks
    {
        get;
    } = new();
    public bool HasWifiNetworks =>
        WifiNetworks.Count > 0;
    public ObservableCollection<
        BluetoothDeviceSnapshot> BluetoothDevices
    {
        get;
    } = new();
    public bool HasBluetoothDevices =>
        BluetoothDevices.Count > 0;
    public ObservableCollection<
        InputMethodOption> InputMethods
    {
        get;
    } = new();
    public bool HasInputMethods =>
        InputMethods.Count > 0;
    public string InputMethodListStatusText =>
        InputMethods.Count switch
        {
            0 => "Windows 未返回可用输入法，可使用下方系统浮层",
            1 => "当前只安装了一个输入法",
            _ => $"已安装 {InputMethods.Count} 个输入法；选择后直接应用到刚才使用的窗口"
        };
    public IReadOnlyList<
        ShellAutoHideDelayOption>
        AutoHideDelayOptions =>
            ShellAutoHideDelayPolicy.Options;
    public IReadOnlyList<
        EdgeHotZoneSensitivityOption>
        HotZoneSensitivityOptions =>
            EdgeHotZoneSensitivityPolicy
                .Options;
    public string WifiNetworkToggleText =>
        IsWifiNetworkBusy
            ? "正在查找 Wi‑Fi…"
            : IsWifiNetworkListVisible
                ? "刷新网络"
                : "查找网络";
    public string BluetoothDeviceToggleText =>
        IsBluetoothDeviceBusy
            ? "正在查找…"
            : IsBluetoothDeviceListVisible
                ? "刷新设备"
                : "查找设备";
    public ObservableCollection<
        ShellDisplayTargetOption> DisplayTargetOptions
    {
        get;
    } = new();
    public ObservableCollection<
        WindowDisplayFilterOption>
        WindowDisplayFilterOptions
    {
        get;
    } = new();
    public string AppSearchStatusText
    {
        get
        {
            if (IsAppCatalogLoading)
                return "正在载入应用目录…";
            if (IsTaskSearchLoading)
                return "正在载入待办任务…";
            if (!string.IsNullOrWhiteSpace(
                    TaskSearchError))
            {
                return "待办任务暂时无法载入；"
                       + "应用、窗口与命令搜索仍可使用";
            }

            return SearchScope switch
            {
                ShellSearchScope.Windows =>
                    IsWindowDisplayFilterActive
                        ? "所选屏幕当前没有可切换的窗口"
                        : IsWindowApplicationFilterActive
                        ? "该应用当前没有可切换的窗口"
                        : "当前没有可切换的窗口",
                ShellSearchScope.Applications =>
                    "没有找到匹配的应用",
                ShellSearchScope.System =>
                    "没有找到匹配的系统入口",
                _ =>
                    "没有找到匹配的应用、窗口、待办、命令或快捷结果"
            };
        }
    }
    public bool IsAppSearchStatusVisible =>
        SearchResults.Count == 0;
    public bool IsAllSearchScope =>
        SearchScope == ShellSearchScope.All;
    public bool IsWindowSearchScope =>
        SearchScope == ShellSearchScope.Windows;
    public bool IsWindowDisplayFilterVisible =>
        IsWindowSearchScope
        && WindowDisplayOverviewPolicy.IsUseful(
            WindowDisplayFilterOptions);
    public bool IsWindowDisplayFilterActive =>
        IsWindowSearchScope
        && !string.Equals(
            SelectedWindowDisplayFilter,
            WindowDisplayOverviewPolicy
                .AllDisplaysValue,
            StringComparison.Ordinal);
    public bool IsApplicationSearchScope =>
        SearchScope == ShellSearchScope.Applications;
    public bool IsSystemSearchScope =>
        SearchScope == ShellSearchScope.System;
    public string OpenWindowScopeLabel =>
        $"窗口 {OpenWindowCount}";
    public bool IsWindowApplicationFilterActive =>
        SearchScope == ShellSearchScope.Windows
        && !string.IsNullOrWhiteSpace(
            _windowOverviewIdentityFilter);
    public string WindowApplicationFilterText =>
        IsWindowApplicationFilterActive
            ? $"仅显示 {_windowOverviewApplicationName} 的窗口"
            : string.Empty;
    public bool AreSearchSuggestionsVisible =>
        IsAllSearchScope
        && string.IsNullOrEmpty(SearchQuery);
    public string AudioGlyph =>
        GetAudioPresentation().Glyph;
    public string AudioSummary =>
        GetAudioPresentation().Summary;
    public string AudioToggleLabel =>
        GetAudioPresentation().ToggleLabel;
    public string AudioCompactValueText =>
        GetAudioPresentation().CompactValueText;
    public string BrightnessSummary =>
        IsBrightnessAvailable
            ? $"亮度 {BrightnessPercent}%"
            : "亮度控制不可用";
    public string NetworkGlyph =>
        GetNetworkPresentation().Glyph;
    public string NetworkSummary =>
        GetNetworkPresentation().Summary;
    public string WiFiRadioActionText =>
        ComposeRadioActionText(
            "Wi‑Fi",
            WiFiRadioState,
            IsWiFiRadioBusy);
    public string BluetoothRadioActionText =>
        ComposeRadioActionText(
            "蓝牙",
            BluetoothRadioState,
            IsBluetoothRadioBusy);
    public bool CanToggleWiFiRadio =>
        !IsWiFiRadioBusy
        && WiFiRadioState
            is SystemRadioState.On
            or SystemRadioState.Off;
    public bool CanToggleBluetoothRadio =>
        !IsBluetoothRadioBusy
        && BluetoothRadioState
            is SystemRadioState.On
            or SystemRadioState.Off;
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
            BatterySummary,
            UnreadPanelNotificationCount);
    public string StatusCenterAutomationName =>
        $"状态中心，{StatusCenterSummary}，"
        + TaskbarHealthStatus;
    public bool HasTaskbarHealthWarning =>
        GetTaskbarHealthPresentation()
            .HasWarning;
    public string TaskbarHealthStatus =>
        GetTaskbarHealthPresentation()
            .Status;
    public string TaskbarDiagnosticsActionText =>
        GetTaskbarDiagnosticsActionPresentation()
            .Text;
    public bool CanInspectTaskbarReplacement =>
        GetTaskbarDiagnosticsActionPresentation()
            .IsEnabled;

    private TaskbarHealthPresentation
        GetTaskbarHealthPresentation() =>
        TaskbarHealthPresentationPolicy.Compose(
            IsReplacementEnabled,
            HasReplacementWarning,
            HasReplacementDiagnostics,
            IsReplacementDiagnosticsHealthy);

    private TaskbarDiagnosticsActionPresentation
        GetTaskbarDiagnosticsActionPresentation() =>
        TaskbarDiagnosticsPresentationPolicy
            .Compose(
                IsTaskbarDiagnosticsBusy,
                HasReplacementDiagnostics);

    public bool HasOpenTasks =>
        GetTaskEntryPresentation().HasBadge;
    public string OpenTaskCountBadgeText =>
        GetTaskEntryPresentation().BadgeText;
    public string TasksEntryAutomationName =>
        GetTaskEntryPresentation().AutomationName;
    public bool HasCollectedDesktopItems =>
        GetOrganizerEntryPresentation().HasBadge;
    public string CollectedDesktopItemCountBadgeText =>
        GetOrganizerEntryPresentation().BadgeText;
    public string OrganizerEntryAutomationName =>
        GetOrganizerEntryPresentation().AutomationName;
    public bool IsTaskQuickCaptureDraft =>
        IsAllSearchScope
        && SearchQuery.StartsWith(
            TaskCaptureCommandParser
                .QuickCapturePrefix,
            StringComparison.Ordinal);
    public bool IsPanelRunDraft =>
        (SearchScope is ShellSearchScope.All
            or ShellSearchScope.System)
        && PanelRunCommandParser.IsDraft(
            SearchQuery);
    public bool IsApplicationLauncherOpen =>
        IsSearchOpen
        && IsApplicationSearchScope;
    public bool IsUnifiedSearchEntryActive =>
        IsSearchOpen
        && !IsApplicationSearchScope;
    public string SearchPanelTitle =>
        IsPanelRunDraft
            ? "Panel 运行"
            : IsTaskQuickCaptureDraft
            ? "快速添加到 Inbox"
            : SearchScope switch
            {
                ShellSearchScope.Windows =>
                    IsWindowApplicationFilterActive
                        ? $"{_windowOverviewApplicationName} 的窗口"
                        : IsWindowDisplayFilterActive
                            ? "所选屏幕的窗口"
                            : "全部窗口",
                ShellSearchScope.Applications =>
                    "开始 · 全部应用",
                ShellSearchScope.System =>
                    "系统工具与快捷命令",
                _ =>
                    "应用、窗口、待办、命令与计算"
            };
    public string SearchPanelInstruction =>
        IsPanelRunDraft
            ? "在 > 后输入程序、文件夹、文件或网址；含空格的目标请使用双引号"
            : IsTaskQuickCaptureDraft
            ? "输入标题后按 Enter，直接保存到 Inbox"
            : SearchScope switch
            {
                ShellSearchScope.Windows =>
                    IsWindowApplicationFilterActive
                        ? "Enter 切换，Delete 关闭；右侧画面按钮预览；输入标题筛选此应用窗口"
                        : "再次按窗口快捷键循环；↑↓ 选择，Enter 切换，Delete 关闭",
                ShellSearchScope.Applications =>
                    "固定应用优先，其后按最近启动排列；输入名称筛选，右侧图钉固定到任务栏",
                _ =>
                    "输入关键词后用 ↑↓ 选择，按 Enter 执行"
            };
    public string SearchPlaceholder =>
        SearchScope switch
        {
            ShellSearchScope.Applications =>
                "搜索已安装应用",
            ShellSearchScope.Windows =>
                "按窗口标题筛选",
            ShellSearchScope.System =>
                "搜索系统工具与快捷命令",
            _ =>
                "输入应用、窗口、待办、音量、媒体或系统命令"
        };
    public bool IsOrganizerEntryActive =>
        string.Equals(
            LastWorkspace,
            "Files",
            StringComparison.Ordinal);
    public bool IsTasksEntryActive =>
        string.Equals(
            LastWorkspace,
            "Tasks",
            StringComparison.Ordinal);
    public bool IsDashboardWorkspaceActive =>
        GetWorkspaceNavigationState().Dashboard;
    public bool IsFilesWorkspaceActive =>
        GetWorkspaceNavigationState().Files;
    public bool IsTasksWorkspaceActive =>
        GetWorkspaceNavigationState().Tasks;
    public bool IsPomodoroWorkspaceActive =>
        GetWorkspaceNavigationState().Pomodoro;
    public bool IsAiWorkspaceActive =>
        GetWorkspaceNavigationState().Ai;
    public bool IsSettingsWorkspaceActive =>
        GetWorkspaceNavigationState().Settings;

    private CompactTaskEntryPresentation
        GetTaskEntryPresentation() =>
        CompactTaskEntryPresentationComposer
            .Compose(OpenTaskCount);

    private CompactOrganizerEntryPresentation
        GetOrganizerEntryPresentation() =>
        CompactOrganizerEntryPresentationComposer
            .Compose(CollectedDesktopItemCount);

    private WorkspaceNavigationState
        GetWorkspaceNavigationState() =>
        WorkspaceNavigationStateComposer.Compose(
            LastWorkspace,
            IsSettingsOpen);

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

    private static string ComposeRadioActionText(
        string displayName,
        SystemRadioState state,
        bool isBusy)
    {
        if (isBusy)
            return $"{displayName}：切换中…";

        string stateText = state switch
        {
            SystemRadioState.On => "开",
            SystemRadioState.Off => "关",
            SystemRadioState.Disabled =>
                "硬件禁用",
            SystemRadioState.Unknown =>
                "状态未知",
            _ => "不可用"
        };
        return $"{displayName}：{stateText}";
    }
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
        // Running applications are core taskbar state. Keep the event-driven
        // tracker current even while the auto-hidden shell is not visible.
        _windowTracker.SetTrackingActive(true);
        if (becameVisible)
        {
            CurrentTime = DateTime.Now;
            _windowTracker.RequestRefresh();
            RequestSystemStatusRefresh();
        }
        UpdateRefreshActivity();
    }

    public void RefreshSystemStatusForInteraction()
    {
        if (_isShellVisible)
            RequestSystemStatusRefresh();
    }

    public event Action? RequestClose;
    public event Action? RequestEnableReplacement;
    public event Action? RequestDisableReplacement;
    public event Action? RequestInspectTaskbarReplacement;
    public event Func<Task>? RequestApplyUpdate;
    public event Action<AppUpdateInfo>? UpdateAvailable;
    public event Action<string>? WorkspaceRequested;
    public event Action<int>? PomodoroCompleted;
    public event Action<int, string>? TaskCaptured;
    public event Action<int, string>? TaskCompleted;
    public event Action? DisplayTargetChanged;
    public event Action<bool>? WorkspacePinChanged;
    public event Action<WifiNetworkSnapshot>?
        WifiCredentialsRequested;
    internal event Action<StatusCenterDetail>?
        StatusCenterDetailRequested;

    internal void SetSummonShortcutStatus(
        ShellHotkeyRegistration registration)
    {
        SummonShortcutText =
            registration.DisplayText;
    }

    internal void SetWindowOverviewShortcutStatus(
        ShellHotkeyRegistration registration)
    {
        WindowOverviewShortcutText =
            registration.DisplayText;
    }

    internal void SetWindowFocusShortcutStatus(
        ShellHotkeyRegistration registration)
    {
        WindowFocusShortcutText =
            registration.DisplayText;
    }

    internal TaskbarAppItem?
        GetWindowFocusShortcutTarget() =>
        WindowFocusShortcutTargetPolicy.Select(
            TaskbarApps,
            _lastActiveExternalWindowHandle);

    internal void SetTaskbarSlotShortcutStatus(
        TaskbarSlotHotkeyRegistration
            registration)
    {
        _taskbarSlotHotkeyRegistration =
            registration;
        TaskbarSlotShortcutText =
            registration.DisplayText;
        ApplyTaskbarShortcutStates();
    }

    internal void SetTaskbarSlotShortcutDisabled()
    {
        _taskbarSlotHotkeyRegistration =
            new TaskbarSlotHotkeyRegistration(
                Array.Empty<
                    TaskbarSlotHotkeyBinding>());
        TaskbarSlotShortcutText =
            EnableTaskbarSlotHotkeys
                ? "快速应用快捷键将在任务栏接管成功后注册"
                : "九槽位全局快速键已关闭";
        ApplyTaskbarShortcutStates();
    }

    public void RefreshDisplayTargetOptions()
    {
        string selectedValue =
            DisplayTargetMode;
        IReadOnlyList<ShellDisplayTargetOption>
            options =
                ShellDisplayTarget.GetOptions(
                    selectedValue,
                    PanelEdge);
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
        long recentHistoryRevision =
            Volatile.Read(
                ref _recentAppHistoryRevision);
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
            PanelEdge =
                preferenceSnapshot.PanelEdge;
            PanelVerticalAnchor =
                preferenceSnapshot
                    .PanelVerticalAnchor;
            AutoHideDelayMilliseconds =
                preferenceSnapshot
                    .AutoHideDelayMilliseconds;
            HotZoneDwellMilliseconds =
                preferenceSnapshot
                    .HotZoneDwellMilliseconds;
            KeepCompactDockVisible =
                preferenceSnapshot
                    .KeepCompactDockVisible;
            if (Volatile.Read(
                    ref _recentAppHistoryRevision)
                == recentHistoryRevision)
            {
                _recentAppIdentities =
                    RecentAppHistoryPolicy.Parse(
                        preferenceSnapshot
                            .RecentAppHistoryJson);
            }
            IsOnboardingVisible =
                FirstRunOnboardingPolicy.ShouldShow(
                    preferenceSnapshot
                        .FirstRunAccepted);
        }
        finally
        {
            _loadingShellPreferences = false;
        }

        ThemeService.SetMode(
            ThemeMode);
        RefreshSearchResults();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshSearchResults();
    }

    partial void OnSearchScopeChanged(
        ShellSearchScope value)
    {
        if (value != ShellSearchScope.Windows)
        {
            ClearWindowOverviewFilterCore();
            _refreshingWindowDisplayFilter = true;
            SelectedWindowDisplayFilter =
                WindowDisplayOverviewPolicy
                    .AllDisplaysValue;
            _refreshingWindowDisplayFilter = false;
        }
        RefreshSearchResults();
    }

    partial void OnSelectedWindowDisplayFilterChanged(
        string value)
    {
        if (!_refreshingWindowDisplayFilter)
            RefreshSearchResults();
    }

    partial void OnIsSearchOpenChanged(bool value)
    {
        if (value)
            _ = RefreshTaskSearchIndexAsync();
    }

    partial void OnIsCalendarOpenChanged(bool value)
    {
        if (value && _isShellVisible)
            RequestTaskSummaryRefresh();
        UpdateRefreshActivity();
    }

    partial void OnIsStatusCenterOpenChanged(bool value)
    {
        if (value && IsReplacementEnabled)
        {
            RequestInspectTaskbarReplacement?
                .Invoke();
        }

        if (value && _isShellVisible)
        {
            RequestSystemStatusRefresh();
            if (IsWifiNetworkListVisible
                && !IsWifiNetworkBusy)
                _ = RefreshWifiNetworksCoreAsync(false);
            if (IsBluetoothDeviceListVisible
                && !IsBluetoothDeviceBusy)
                _ = RefreshBluetoothDevicesCoreAsync();
        }
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

    partial void OnBrightnessPercentChanged(
        int value)
    {
        if (_updatingBrightnessState)
            return;

        QueueBrightness(value);
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

    private void QueueBrightness(int value)
    {
        int normalized =
            Math.Clamp(value, 0, 100);
        long revision =
            Interlocked.Increment(
                ref _brightnessRevision);
        _brightnessWritePending = true;
        if (_brightnessControl.Queue(
                revision,
                normalized))
        {
            return;
        }

        _brightnessWritePending = false;
        SetDisplayedBrightness(
            _confirmedBrightnessPercent);
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
        if (_applyingPanelPlacement)
            return;

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

    partial void OnPanelVerticalAnchorChanged(
        string value)
    {
        if (_applyingPanelPlacement)
            return;

        string normalized =
            ShellPanelVerticalAnchorPolicy
                .NormalizeValue(value);
        if (!string.Equals(
                normalized,
                value,
                StringComparison.Ordinal))
        {
            PanelVerticalAnchor = normalized;
            return;
        }

        DisplayTargetChanged?.Invoke();
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .PanelVerticalAnchorKey,
                normalized);
        }
    }

    partial void OnPanelEdgeChanged(
        string value)
    {
        if (_applyingPanelPlacement)
            return;

        string normalized =
            ShellPanelEdgePolicy
                .NormalizeValue(value);
        if (!string.Equals(
                normalized,
                value,
                StringComparison.Ordinal))
        {
            PanelEdge = normalized;
            return;
        }

        DisplayTargetChanged?.Invoke();
        RefreshDisplayTargetOptions();
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .PanelEdgeKey,
                normalized);
        }
    }

    public void ApplyPanelPlacement(
        string displayTarget,
        string panelEdge,
        string verticalAnchor)
    {
        string normalizedTarget =
            ShellDisplayTarget.NormalizeValue(
                displayTarget);
        string normalizedEdge =
            ShellPanelEdgePolicy.NormalizeValue(
                panelEdge);
        string normalizedAnchor =
            ShellPanelVerticalAnchorPolicy
                .NormalizeValue(
                    verticalAnchor);
        bool targetChanged = !string.Equals(
            DisplayTargetMode,
            normalizedTarget,
            StringComparison.Ordinal);
        bool edgeChanged = !string.Equals(
            PanelEdge,
            normalizedEdge,
            StringComparison.Ordinal);
        bool anchorChanged = !string.Equals(
            PanelVerticalAnchor,
            normalizedAnchor,
            StringComparison.Ordinal);
        if (!targetChanged
            && !edgeChanged
            && !anchorChanged)
        {
            return;
        }

        _applyingPanelPlacement = true;
        try
        {
            DisplayTargetMode = normalizedTarget;
            PanelEdge = normalizedEdge;
            PanelVerticalAnchor = normalizedAnchor;
        }
        finally
        {
            _applyingPanelPlacement = false;
        }

        DisplayTargetChanged?.Invoke();
        RefreshDisplayTargetOptions();
        if (_loadingShellPreferences)
            return;

        if (targetChanged)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .DisplayTargetModeKey,
                normalizedTarget);
        }
        if (edgeChanged)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .PanelEdgeKey,
                normalizedEdge);
        }
        if (anchorChanged)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .PanelVerticalAnchorKey,
                normalizedAnchor);
        }
    }

    partial void OnAutoHideDelayMillisecondsChanged(
        int value)
    {
        int normalized =
            ShellAutoHideDelayPolicy.Normalize(
                value);
        if (normalized != value)
        {
            AutoHideDelayMilliseconds =
                normalized;
            return;
        }

        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .AutoHideDelayKey,
                normalized.ToString());
        }
    }

    partial void OnHotZoneDwellMillisecondsChanged(
        int value)
    {
        int normalized =
            EdgeHotZoneSensitivityPolicy
                .NormalizeDwell(value);
        if (normalized != value)
        {
            HotZoneDwellMilliseconds =
                normalized;
            return;
        }

        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .HotZoneDwellKey,
                normalized.ToString());
        }
    }

    partial void OnKeepCompactDockVisibleChanged(
        bool value)
    {
        if (!_loadingShellPreferences)
        {
            QueueShellPreference(
                ShellPreferenceRepository
                    .KeepCompactDockVisibleKey,
                value.ToString());
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
                _tasksViewModel =
                    GetOrCreateTasksViewModel();
                CurrentViewModel = _tasksViewModel;
                CurrentSectionTitle = "任务";
                break;
            case "Pomodoro":
                _pomodoroViewModel =
                    GetOrCreatePomodoroViewModel();
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

            if (_fileOrganizerViewModel == null)
            {
                _fileOrganizerViewModel =
                    viewModel;
                _fileOrganizerViewModel
                    .CollectedCountChanged +=
                    FileOrganizer_CollectedCountChanged;
                ApplyCollectedDesktopItemCount(
                    _fileOrganizerViewModel
                        .CollectedItemCount);
            }
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

    private void FileOrganizer_CollectedCountChanged(
        int count)
    {
        if (_isDisposed)
            return;

        if (!_uiDispatcher.CheckAccess())
        {
            _uiDispatcher.BeginInvoke(
                () =>
                    ApplyCollectedDesktopItemCount(
                        count),
                DispatcherPriority.Background);
            return;
        }

        ApplyCollectedDesktopItemCount(count);
    }

    private void ApplyCollectedDesktopItemCount(
        int count)
    {
        if (!_isDisposed)
        {
            CollectedDesktopItemCount =
                Math.Max(0, count);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ExecuteSearchResult(
        ShellSearchResult? result)
    {
        if (result?.RunCommand
            is PanelRunCommand runCommand)
        {
            await ExecutePanelRunCommandAsync(
                runCommand);
            return;
        }

        if (result?.TaskCaptureCommand
            is TaskCaptureCommand taskCapture)
        {
            await ExecuteTaskCaptureCommandAsync(
                taskCapture);
            return;
        }

        if (result?.TaskItem
            is TaskSearchItem taskItem)
        {
            await OpenSearchTaskAsync(
                taskItem);
            return;
        }

        if (result?.FocusCommand
            is PomodoroSearchCommand focusCommand)
        {
            ExecuteFocusSearchCommand(
                focusCommand);
            return;
        }

        if (result?.AudioCommand
            is AudioSearchCommand audioCommand)
        {
            ExecuteAudioSearchCommand(
                audioCommand);
            return;
        }

        if (result?.ApplicationAudioCommand
            is ApplicationAudioSearchCommand
                applicationAudioCommand)
        {
            ExecuteApplicationAudioSearchCommand(
                applicationAudioCommand);
            return;
        }

        if (result?.BrightnessCommand
            is BrightnessSearchCommand
                brightnessCommand)
        {
            ExecuteBrightnessSearchCommand(
                brightnessCommand);
            return;
        }

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

        if (result?.StatusDetail
            is StatusCenterDetail statusDetail)
        {
            ShowStatusCenterDetail(statusDetail);
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

    private async Task ExecutePanelRunCommandAsync(
        PanelRunCommand command)
    {
        PanelRunResult result =
            await _panelRun.RunAsync(
                command);
        if (_isDisposed)
            return;

        if (result.Status
            == PanelRunStatus.Started)
        {
            SystemActionMessage =
                string.Empty;
            IsSearchOpen = false;
            return;
        }

        string reason =
            string.IsNullOrWhiteSpace(
                result.Error)
                ? "Windows 无法打开该目标；请检查名称、路径或文件关联。"
                : result.Error!;
        SystemActionMessage =
            $"无法运行“{command.FileName}”。{reason}";
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private TasksViewModel
        GetOrCreateTasksViewModel()
    {
        _tasksViewModel ??=
            new TasksViewModel(
                _taskService);
        return _tasksViewModel;
    }

    private async Task
        ExecuteTaskCaptureCommandAsync(
            TaskCaptureCommand command)
    {
        IsSearchOpen = false;
        TaskQuickCaptureResult result =
            await _taskCapture
                .CaptureAsync(
                    command.Title);
        if (_isDisposed)
            return;

        if (result.Succeeded
            && result.Item != null)
        {
            _tasksViewModel
                ?.ApplyQuickCapturedInboxItem(
                    result.Item);
            PrependTaskSearchItem(
                new TaskSearchItem(
                    result.Item.Id,
                    result.Item.Title,
                    TaskQuickCaptureCoordinator
                        .InboxId,
                    "Inbox",
                    result.Item.Status,
                    result.Item.CreatedAt));
            SystemActionMessage =
                string.Empty;
            TaskCaptured?.Invoke(
                result.Item.Id,
                result.Item.Title);
            RequestTaskSummaryRefresh();
            return;
        }

        SystemActionMessage =
            "无法收集任务到 Inbox。"
            + (string.IsNullOrWhiteSpace(
                    result.Error)
                ? "请打开任务工作区后重试。"
                : $" {result.Error}");
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private async Task OpenSearchTaskAsync(
        TaskSearchItem task)
    {
        IsSearchOpen = false;
        Navigate("Tasks");
        bool opened =
            await GetOrCreateTasksViewModel()
                .NavigateToSearchTaskAsync(
                    task.Id);
        if (_isDisposed
            || opened)
        {
            return;
        }

        SystemActionMessage =
            $"无法打开“{task.Title}”。"
            + "任务可能已被删除或移动，请重新搜索。";
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    [RelayCommand]
    private async Task CompleteSearchTask(
        ShellSearchResult? result)
    {
        if (result?.TaskItem
            is not TaskSearchItem task)
        {
            return;
        }

        TaskSearchCompletionResult completion =
            await _taskSearch
                .CompleteTaskAsync(
                    task.Id);
        if (_isDisposed)
            return;

        if (completion.Succeeded)
        {
            _taskSearchItems =
                _taskSearchItems
                    .Where(item =>
                        item.Id
                        != completion.TaskId)
                    .ToArray();
            _tasksViewModel
                ?.ApplyExternallyCompletedTask(
                    completion.TaskId);
            RefreshSearchResults();
            RequestTaskSummaryRefresh();
            SystemActionMessage =
                string.Empty;
            TaskCompleted?.Invoke(
                completion.TaskId,
                completion.Title);
            return;
        }

        SystemActionMessage =
            $"无法完成“{task.Title}”。"
            + (string.IsNullOrWhiteSpace(
                    completion.Error)
                ? "请打开任务工作区后重试。"
                : $" {completion.Error}");
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private PomodoroViewModel
        GetOrCreatePomodoroViewModel()
    {
        if (_pomodoroViewModel != null)
            return _pomodoroViewModel;

        _pomodoroViewModel =
            new PomodoroViewModel();
        _pomodoroViewModel.SessionCompleted +=
            PomodoroViewModel_SessionCompleted;
        _pomodoroViewModel.SessionPersisted +=
            PomodoroViewModel_SessionPersisted;
        return _pomodoroViewModel;
    }

    private void ExecuteFocusSearchCommand(
        PomodoroSearchCommand command)
    {
        PomodoroViewModel viewModel =
            GetOrCreatePomodoroViewModel();
        PomodoroQuickStartResult result =
            viewModel.TryStartQuickSession(
                command.DurationMinutes);
        switch (result)
        {
            case PomodoroQuickStartResult.Started:
                SystemActionMessage =
                    string.Empty;
                IsSearchOpen = false;
                break;
            case PomodoroQuickStartResult
                .AlreadyRunning:
            case PomodoroQuickStartResult
                .SessionInProgress:
                Navigate("Pomodoro");
                break;
            default:
                SystemActionMessage =
                    "无法开始专注计时，请打开番茄钟工作区后重试。";
                CloseTransientPanels();
                IsStatusCenterOpen = true;
                break;
        }
    }

    private void ExecuteAudioSearchCommand(
        AudioSearchCommand command)
    {
        if (command.RequiresCurrentVolume
            && !IsAudioAvailable)
        {
            ReportAudioFailure(
                "当前音量尚未读取完成，无法安全执行相对调整。"
                + "请在状态中心确认默认音频设备后重试。");
            return;
        }

        AudioSearchMutation mutation =
            command.Resolve(
                MasterVolume);
        SystemActionMessage =
            string.Empty;
        if (mutation.Volume
            is float volume)
        {
            MasterVolume =
                volume;
        }

        if (mutation.Muted
            is bool muted)
        {
            IsMuted =
                muted;
        }

        IsSearchOpen = false;
    }

    private void
        ExecuteApplicationAudioSearchCommand(
            ApplicationAudioSearchCommand command)
    {
        ApplicationAudioSessionItem? item =
            ApplicationAudioSessions
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.SessionId,
                        command.SessionId,
                        StringComparison.Ordinal));
        if (item == null)
        {
            SystemActionMessage =
                $"“{command.ApplicationName}”的音频会话已经结束。"
                + "请重新搜索或在声音与应用音量中刷新。";
            CloseTransientPanels();
            IsStatusCenterOpen = true;
            RequestSystemStatusRefresh();
            return;
        }

        AudioSearchMutation mutation =
            command.Command.Resolve(
                item.Volume);
        SystemActionMessage = string.Empty;
        if (mutation.Volume is float volume)
            item.Volume = volume;
        if (mutation.Muted is bool muted)
            SetApplicationAudioMuted(
                item,
                muted);
        IsSearchOpen = false;
    }

    private void ExecuteBrightnessSearchCommand(
        BrightnessSearchCommand command)
    {
        if (!IsBrightnessAvailable)
        {
            ReportBrightnessFailure(
                "此设备没有向 Windows 公开内置显示器亮度控制。"
                + "外接显示器请使用显示器按键。Panel 不会弹出任务栏快捷设置。");
            return;
        }

        if (command.RequiresCurrentBrightness
            && _brightnessWritePending)
        {
            ReportBrightnessFailure(
                "上一项亮度调整仍在执行，"
                + "请稍后再使用相对亮度命令。");
            return;
        }

        SystemActionMessage = string.Empty;
        BrightnessPercent =
            command.Resolve(
                BrightnessPercent);
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
                WindowsShellAction.ScreenSnipping =>
                    _systemStatus.OpenScreenSnipping,
                WindowsShellAction.ProjectDisplay =>
                    _systemStatus.OpenProjectDisplay,
                WindowsShellAction.CastDevices =>
                    _systemStatus.OpenCastDevices,
                WindowsShellAction.ShowDesktop =>
                    _systemStatus.ShowDesktop,
                WindowsShellAction
                    .MediaPreviousTrack =>
                    () => _systemStatus
                        .SendMediaCommand(
                            MediaTransportAction
                                .PreviousTrack),
                WindowsShellAction
                    .MediaPlayPause =>
                    () => _systemStatus
                        .SendMediaCommand(
                            MediaTransportAction
                                .PlayPause),
                WindowsShellAction
                    .MediaNextTrack =>
                    () => _systemStatus
                        .SendMediaCommand(
                            MediaTransportAction
                                .NextTrack),
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
        TaskbarPrimaryAction action =
            TaskbarPrimaryActionPolicy.Get(task);
        if (action
            == TaskbarPrimaryAction
                .ActivateOrMinimize
            && task?.RunningTask != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.ActivateOrMinimize(
                        task.RunningTask)),
                $"无法切换“{task.DisplayName}”。窗口可能已经关闭，"
                + "或 Windows 暂时阻止了前台切换。");
            return;
        }
        if (action != TaskbarPrimaryAction.Launch)
            return;
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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LaunchElevatedTaskbarApp(
        TaskbarAppItem? task)
    {
        AppLaunchItem? launch =
            task?.CreateElevatedLaunchItem();
        if (launch == null)
        {
            ReportTaskbarActionFailure(
                $"“{task?.DisplayName ?? "该应用"}”没有可靠的桌面启动目标，"
                + "Windows 商店应用不能由 Panel 强制提权。");
            return;
        }

        await LaunchElevatedAppAsync(
            launch);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task
        LaunchElevatedSearchApplication(
            ShellSearchResult? result)
    {
        AppLaunchItem? launch =
            result?.Application;
        if (launch == null
            || !ElevatedAppLaunchRequestBuilder
                .TryBuild(
                    launch,
                    out _))
        {
            ReportTaskbarActionFailure(
                $"“{result?.DisplayName ?? "该应用"}”没有可靠的桌面启动目标，"
                + "Windows 商店应用不能由 Panel 强制提权。");
            return;
        }

        await LaunchElevatedAppAsync(
            launch);
    }

    private async Task LaunchElevatedAppAsync(
        AppLaunchItem launch)
    {

        ElevatedAppLaunchCompletion completion =
            await _elevatedAppLaunch
                .LaunchAsync(launch);
        if (_isDisposed
            || !_elevatedAppLaunch.IsCurrent(
                completion.Revision))
        {
            return;
        }

        switch (completion.Status)
        {
            case ElevatedAppLaunchStatus.Started:
                SystemActionMessage =
                    string.Empty;
                RecordSuccessfulAppLaunch(launch);
                break;
            case ElevatedAppLaunchStatus.Cancelled:
                SystemActionMessage =
                    $"已取消以管理员身份启动“{launch.DisplayName}”。";
                break;
            case ElevatedAppLaunchStatus.Unsupported:
                ReportTaskbarActionFailure(
                    $"“{launch.DisplayName}”不支持以管理员身份启动。");
                break;
            default:
                ReportTaskbarActionFailure(
                    $"无法以管理员身份启动“{launch.DisplayName}”。"
                    + "目标可能已移动，或 Windows 阻止了此操作。");
                break;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenTaskbarAppLocation(
        TaskbarAppItem? task)
    {
        if (task == null
            || !AppLocationPolicy.TryResolve(
                task.CreateLaunchItem(),
                task.ExecutablePath,
                out AppLocationTarget target))
        {
            ReportTaskbarActionFailure(
                $"“{task?.DisplayName ?? "该应用"}”没有可定位的本地启动文件。");
            return;
        }

        await OpenAppLocationAsync(
            task.DisplayName,
            target);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task
        OpenSearchApplicationLocation(
            ShellSearchResult? result)
    {
        AppLaunchItem? application =
            result?.Application;
        if (application == null
            || !AppLocationPolicy.TryResolve(
                application,
                null,
                out AppLocationTarget target))
        {
            ReportTaskbarActionFailure(
                $"“{result?.DisplayName ?? "该应用"}”没有可定位的本地启动文件。");
            return;
        }

        await OpenAppLocationAsync(
            result!.DisplayName,
            target);
    }

    private async Task OpenAppLocationAsync(
        string displayName,
        AppLocationTarget target)
    {
        AppLocationOpenResult result =
            await _appLocation.OpenAsync(
                target);
        if (_isDisposed)
            return;

        if (result.Status
            == AppLocationOpenStatus.Opened)
        {
            SystemActionMessage =
                string.Empty;
            return;
        }

        string reason = result.Status
            == AppLocationOpenStatus.Missing
                ? "启动文件可能已移动、删除或卸载。"
                : string.IsNullOrWhiteSpace(
                    result.Error)
                    ? "文件资源管理器拒绝了定位请求。"
                    : result.Error!;
        ReportTaskbarActionFailure(
            $"无法定位“{displayName}”。{reason}");
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
    private void MinimizeWindow(
        WindowReference? window)
    {
        if (window != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.Minimize(
                        window.Handle)),
                $"无法最小化“{window.Title}”。窗口可能已经关闭，"
                + "或当前应用拒绝了状态更改。");
        }
    }

    [RelayCommand]
    private void MaximizeWindow(
        WindowReference? window)
    {
        if (window != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.Maximize(
                        window.Handle)),
                $"无法最大化“{window.Title}”。窗口可能已经关闭，"
                + "或当前应用拒绝了状态更改。");
        }
    }

    [RelayCommand]
    private void RestoreWindow(
        WindowReference? window)
    {
        if (window != null)
        {
            CompleteTaskbarWindowAction(
                SystemActionExecution.Try(
                    () => _windowTracker.Restore(
                        window.Handle)),
                $"无法还原“{window.Title}”。窗口可能已经关闭，"
                + "或当前应用拒绝了状态更改。");
        }
    }

    [RelayCommand]
    private void FocusWindow(
        WindowReference? window)
    {
        if (window == null)
            return;

        StartWindowFocusSession(
            new[]
            {
                window.Handle
            },
            window.Title,
            window);
    }

    [RelayCommand]
    private void FocusTaskWindows(
        TaskbarAppItem? task)
    {
        if (task == null
            || task.WindowCount == 0)
        {
            return;
        }

        WindowReference target =
            task.Windows.FirstOrDefault(
                window => window.IsActive)
            ?? task.Windows[0];
        StartWindowFocusSession(
            task.Windows.Select(window =>
                window.Handle).ToArray(),
            task.DisplayName,
            target);
    }

    [RelayCommand]
    private void RestoreWindowFocusSession()
    {
        WindowFocusRestoreResult result =
            _windowFocusSession.Restore(
                GetTrackedWindows(),
                handle =>
                    _windowTracker.Restore(handle),
                handle =>
                    _windowTracker.Maximize(handle));
        UpdateWindowFocusSessionState();
        if (result.RemainingCount == 0)
        {
            SystemActionMessage =
                string.Empty;
            return;
        }

        ReportTaskbarActionFailure(
            $"仍有 {result.RemainingCount} 个专注窗口"
            + "未能恢复。窗口可能已关闭、权限更高，"
            + "或应用暂时拒绝状态更改；可以再次重试。"
            + (result.RestoredCount > 0
                ? $"另有 {result.RestoredCount} 个窗口已经恢复。"
                : string.Empty));
    }

    [RelayCommand]
    private void ArrangeWindow(
        WindowLayoutRequest? request)
    {
        if (request == null)
            return;

        CompleteTaskbarWindowAction(
            SystemActionExecution.Try(
                () => _windowTracker.Arrange(
                    request.Window.Handle,
                    request.Target)),
            $"无法把“{request.Window.Title}”排列到"
            + $"{WindowLayoutPresentation.GetName(request.Target)}。"
            + "窗口可能已经关闭、没有标准布局能力，"
            + "或当前应用拒绝了位置更改。");
    }

    [RelayCommand]
    private void MoveWindowToPanelDisplay(
        WindowReference? window)
    {
        if (window == null)
            return;

        System.Drawing.Rectangle targetWorkArea =
            ShellDisplayTarget.GetWorkingArea(
                DisplayTargetMode,
                PanelEdge);
        CompleteTaskbarWindowAction(
            SystemActionExecution.Try(
                () => _windowTracker.MoveToDisplay(
                    window.Handle,
                    targetWorkArea)),
            $"无法把“{window.Title}”移到 Panel 所在屏幕。"
            + "窗口可能已经关闭，或当前应用拒绝了位置更改。");
    }

    [RelayCommand]
    private void MoveWindowToDisplay(
        WindowDisplayMoveRequest? request)
    {
        if (request == null)
            return;

        CompleteTaskbarWindowAction(
            SystemActionExecution.Try(
                () => _windowTracker.MoveToDisplay(
                    request.Window.Handle,
                    request.TargetWorkArea)),
            $"无法把“{request.Window.Title}”移到"
            + $"{request.TargetDisplayName}。"
            + "窗口可能已经关闭，或当前应用拒绝了位置更改。");
    }

    [RelayCommand]
    private void MoveTaskWindowsToPanelDisplay(
        TaskbarAppItem? task)
    {
        if (task == null)
            return;

        System.Drawing.Rectangle targetWorkArea =
            ShellDisplayTarget.GetWorkingArea(
                DisplayTargetMode,
                PanelEdge);
        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                task.Windows,
                handle =>
                    _windowTracker.CanMoveToDisplay(
                        handle,
                        targetWorkArea),
                handle =>
                    _windowTracker.MoveToDisplay(
                        handle,
                        targetWorkArea));
        if (!result.HasWork
            || result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            return;
        }

        ReportTaskbarActionFailure(
            $"“{task.DisplayName}”有 {result.FailedCount} 个窗口"
            + "未能移到 Panel 所在屏幕。"
            + (result.MovedCount > 0
                ? $"另有 {result.MovedCount} 个窗口已经移动。"
                : "窗口可能已经关闭，或应用拒绝了位置更改。"));
    }

    [RelayCommand]
    private void MoveTaskWindowsToDisplay(
        TaskbarDisplayMoveRequest? request)
    {
        if (request == null)
            return;

        WindowBatchMoveResult result =
            WindowBatchMoveCoordinator.Execute(
                request.Task.Windows,
                handle =>
                    _windowTracker.CanMoveToDisplay(
                        handle,
                        request.TargetWorkArea),
                handle =>
                    _windowTracker.MoveToDisplay(
                        handle,
                        request.TargetWorkArea));
        if (!result.HasWork
            || result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            return;
        }

        ReportTaskbarActionFailure(
            $"“{request.Task.DisplayName}”有 "
            + $"{result.FailedCount} 个窗口未能移到"
            + $"{request.TargetDisplayName}。"
            + (result.MovedCount > 0
                ? $"另有 {result.MovedCount} 个窗口已经移动。"
                : "窗口可能已经关闭，或应用拒绝了位置更改。"));
    }

    [RelayCommand]
    private void ToggleWindowTopmost(
        WindowReference? window)
    {
        if (window == null)
            return;

        bool target = !window.IsTopmost;
        CompleteTaskbarWindowAction(
            SystemActionExecution.Try(
                () => _windowTracker.SetTopmost(
                    window.Handle,
                    target)),
            $"无法{(target ? "置顶" : "取消置顶")}"
            + $"“{window.Title}”。窗口可能已经关闭，"
            + "或当前应用拒绝了层级更改。");
    }

    [RelayCommand]
    private void MinimizeTaskWindows(
        TaskbarAppItem? task)
    {
        if (task == null)
            return;

        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                task.Windows,
                window =>
                    window.State
                    != TrackedWindowState.Minimized,
                handle =>
                    _windowTracker.Minimize(
                        handle));
        CompleteTaskbarBatchAction(
            task,
            result,
            "最小化",
            "窗口可能已经关闭，或应用拒绝了状态更改。");
    }

    [RelayCommand]
    private void RestoreTaskWindows(
        TaskbarAppItem? task)
    {
        if (task == null)
            return;

        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                task.Windows,
                window =>
                    window.State
                    == TrackedWindowState.Minimized,
                handle =>
                    _windowTracker.Restore(
                        handle));
        CompleteTaskbarBatchAction(
            task,
            result,
            "还原",
            "窗口可能已经关闭，或应用拒绝了状态更改。");
    }

    [RelayCommand]
    private void CloseTask(TaskbarAppItem? task)
    {
        if (task == null)
            return;

        WindowBatchActionResult result =
            WindowBatchActionCoordinator.Execute(
                task.Windows,
                _ => true,
                handle =>
                    _windowTracker.Close(
                        handle));
        CompleteTaskbarBatchAction(
            task,
            result,
            "关闭",
            "窗口可能已经关闭，或应用拒绝了关闭消息。");
    }

    private void CompleteTaskbarBatchAction(
        TaskbarAppItem task,
        WindowBatchActionResult result,
        string actionName,
        string failureReason)
    {
        if (!result.HasWork
            || result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            return;
        }

        ReportTaskbarActionFailure(
            $"“{task.DisplayName}”有 {result.FailedCount} 个窗口"
            + $"未能{actionName}。"
            + (result.SucceededCount > 0
                ? $"另有 {result.SucceededCount} 个窗口已{actionName}。"
                : failureReason));
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        bool open = !IsSearchOpen;
        CloseTransientPanels();
        IsSearchOpen = open;
        if (IsSearchOpen
            && SearchScope
                is ShellSearchScope.All
                    or ShellSearchScope.System
            && ApplicationAudioSearchCommandParser
                .HasTargetedCommandSyntax(
                    SearchQuery))
        {
            RefreshSearchResults();
        }
    }

    internal void PrepareTaskbarWindowOverview(
        TaskbarAppItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _windowOverviewIdentityFilter =
            task.IdentityKey;
        _windowOverviewApplicationName =
            task.DisplayName;
        SearchScope = ShellSearchScope.Windows;
        SearchQuery = string.Empty;
        NotifyWindowOverviewFilterChanged();
        RefreshSearchResults();
    }

    internal void ClearWindowOverviewFilter()
    {
        if (!ClearWindowOverviewFilterCore())
            return;

        RefreshSearchResults();
    }

    [RelayCommand]
    private void ClearWindowApplicationFilter() =>
        ClearWindowOverviewFilter();

    private bool ClearWindowOverviewFilterCore()
    {
        if (string.IsNullOrWhiteSpace(
                _windowOverviewIdentityFilter)
            && string.IsNullOrWhiteSpace(
                _windowOverviewApplicationName))
        {
            return false;
        }

        _windowOverviewIdentityFilter = null;
        _windowOverviewApplicationName =
            string.Empty;
        NotifyWindowOverviewFilterChanged();
        return true;
    }

    private void NotifyWindowOverviewFilterChanged()
    {
        OnPropertyChanged(
            nameof(
                IsWindowApplicationFilterActive));
        OnPropertyChanged(
            nameof(
                WindowApplicationFilterText));
        OnPropertyChanged(
            nameof(SearchPanelTitle));
        OnPropertyChanged(
            nameof(SearchPanelInstruction));
        OnPropertyChanged(
            nameof(AppSearchStatusText));
    }

    [RelayCommand]
    private void SelectSearchScope(
        string? scope)
    {
        if (Enum.TryParse(
                scope,
                ignoreCase: true,
                out ShellSearchScope parsed))
        {
            SearchScope = parsed;
        }
    }

    [RelayCommand]
    private void SetBackgroundAppFilterScope(
        BackgroundAppFilterScope scope) =>
        BackgroundAppScope = scope;

    [RelayCommand]
    private void ClearBackgroundAppSearch() =>
        BackgroundAppQuery = string.Empty;

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

    internal void ShowStatusCenterDetail(
        StatusCenterDetail detail)
    {
        if (detail == StatusCenterDetail.None)
            return;

        CloseTransientPanels();
        IsStatusCenterOpen = true;
        StatusCenterDetailRequested?.Invoke(detail);
        if (detail == StatusCenterDetail.Network
            && IsBluetoothDeviceListVisible
            && !IsBluetoothDeviceBusy)
        {
            _ = RefreshBluetoothDevicesCoreAsync();
        }
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
    private async Task SendMediaCommand(
        MediaTransportAction action)
    {
        string actionName =
            action switch
            {
                MediaTransportAction
                    .PreviousTrack =>
                    "上一首",
                MediaTransportAction
                    .PlayPause =>
                    "播放 / 暂停",
                MediaTransportAction
                    .NextTrack =>
                    "下一首",
                _ =>
                    "媒体控制"
            };
        await RunInlineStatusActionAsync(
            () => _systemStatus
                .SendMediaCommand(action),
            $"无法发送“{actionName}”媒体键。"
            + "当前系统会话可能不允许模拟输入。");
    }

    [RelayCommand]
    private async Task OpenWifiLocationSettings()
        => await RunSystemActionAsync(
            _systemStatus
                .OpenLocationPrivacySettings,
            "无法打开 Windows 位置权限设置。"
            + "请进入“设置 > 隐私和安全性 > 位置”。");

    [RelayCommand]
    private void MarkAllPanelNotificationsRead() =>
        _notificationCenter.MarkAllRead();

    [RelayCommand]
    private void MarkPanelNotificationRead(
        FocusNotificationItem? notification) =>
            _notificationCenter.MarkRead(notification);

    [RelayCommand]
    private void ClearPanelNotifications() =>
        _notificationCenter.Clear();

    [RelayCommand]
    private void RemovePanelNotification(
        FocusNotificationItem? notification) =>
            _notificationCenter.Remove(notification);

    [RelayCommand]
    private void InvokePanelNotification(
        FocusNotificationItem? notification) =>
        _notificationCenter.Invoke(notification);

    private void NotificationCenter_Changed(
        object? sender,
        EventArgs e)
    {
        OnPropertyChanged(nameof(HasPanelNotifications));
        OnPropertyChanged(nameof(HasUnreadPanelNotifications));
        OnPropertyChanged(nameof(UnreadPanelNotificationCount));
        OnPropertyChanged(nameof(PanelNotificationBadgeText));
        OnPropertyChanged(nameof(FilteredPanelNotifications));
        OnPropertyChanged(nameof(HasFilteredPanelNotifications));
        OnPropertyChanged(nameof(PanelNotificationEmptyText));
        OnPropertyChanged(nameof(PanelNotificationCountText));
        OnPropertyChanged(nameof(StatusCenterSummary));
        OnPropertyChanged(nameof(StatusCenterAutomationName));
    }

    private void NotificationCenter_PersistenceStatusChanged(
        object? sender,
        EventArgs e)
    {
        if (_isDisposed)
            return;

        if (!_uiDispatcher.CheckAccess())
        {
            _uiDispatcher.BeginInvoke(
                new Action(() =>
                    NotificationCenter_PersistenceStatusChanged(
                        sender,
                        e)));
            return;
        }

        OnPropertyChanged(nameof(NotificationPersistenceError));
        OnPropertyChanged(nameof(HasNotificationPersistenceError));
    }

    [RelayCommand]
    private async Task ActivateInputMethod(
        InputMethodOption inputMethod)
    {
        IntPtr target =
            _lastActiveExternalWindowHandle;
        SystemActionCompletion completion =
            await _systemActions.ExecuteAsync(
                () => _systemStatus
                    .TryActivateInputMethod(
                        inputMethod,
                        target));
        if (_isDisposed
            || !_systemActions.IsCurrent(
                completion.Revision))
        {
            return;
        }

        if (!completion.Succeeded)
        {
            SystemActionMessage =
                $"未能确认“{inputMethod.DisplayName}”已应用到刚才使用的窗口。"
                + "目标应用可能已关闭、以更高权限运行、尚未处理或拒绝切换；"
                + "Panel 保留当前列表，请重新选择或切回目标窗口后重试。";
            IsStatusCenterOpen = true;
            return;
        }

        SystemActionMessage = string.Empty;
        ReplaceCollection(
            InputMethods,
            InputMethods.Select(item =>
                item with
                {
                    IsActive =
                        item.LayoutHandle
                        == inputMethod
                            .LayoutHandle
                }).ToArray());
        OnPropertyChanged(
            nameof(HasInputMethods));
        OnPropertyChanged(
            nameof(
                InputMethodListStatusText));
        await Task.Delay(120);
        RequestSystemStatusRefresh();
    }

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
            "Windows 桌面服务拒绝切换；当前窗口状态保持不变。");

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
    private void InspectTaskbarReplacement() =>
        RequestInspectTaskbarReplacement?.Invoke();

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
        PersistCompactDockPreference();
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
        ReplacementDiagnostics = string.Empty;
        HasReplacementDiagnostics = false;
        IsReplacementDiagnosticsHealthy = false;
        QueueShellPreference(
            ShellPreferenceRepository
                .FirstRunAcceptedKey,
            bool.TrueString);
        QueueShellPreference(
            ShellPreferenceRepository
                .ReplacementEnabledKey,
            enabled.ToString());
        PersistCompactDockPreference();
        ApplyStartupPreference(enabled && StartWithWindows);
    }

    private void PersistCompactDockPreference()
    {
        QueueShellPreference(
            ShellPreferenceRepository
                .KeepCompactDockVisibleKey,
            KeepCompactDockVisible.ToString());
    }

    public void MarkReplacementStopped(TaskbarReplacementStopReason reason, string message)
    {
        ReplacementStopReason = reason;
        MarkReplacementEnabled(false, message);
    }

    public void MarkReplacementDiagnostics(
        TaskbarReplacementDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(
            diagnostics);
        ReplacementDiagnostics =
            diagnostics.Summary;
        HasReplacementDiagnostics = true;
        IsReplacementDiagnosticsHealthy =
            diagnostics.IsHealthy;
    }

    public void MarkTaskbarDiagnosticsBusy(
        bool isBusy) =>
        IsTaskbarDiagnosticsBusy = isBusy;

    [RelayCommand]
    private void CloseApp() => RequestClose?.Invoke();

    [RelayCommand]
    private void MinimizeApp()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.CollapseSidebar();
    }

    [RelayCommand]
    private async Task RestoreDatabase()
    {
        var result = FocusDialogService.Show(
            "确定要从最新备份恢复数据库吗？\n任务、番茄钟、桌面收纳和兼容保留数据都会回到备份时的状态，应用将立即重启。",
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
        bool compactStructureChanged =
            TaskbarAppCollectionSynchronizer.Synchronize(
            CompactTaskbarApps,
            CompactTaskbarAppPolicy.Select(
                TaskbarApps));
        if (compactStructureChanged)
        {
            CompactTaskbarStructureRevision =
                unchecked(
                    CompactTaskbarStructureRevision
                    + 1);
        }
        ApplyTaskbarShortcutStates();
        ActiveTaskbarIdentity =
            TaskbarApps.FirstOrDefault(
                item => item.IsActive)
            ?.IdentityKey;
        AttentionTaskbarIdentity =
            TaskbarApps.FirstOrDefault(
                item =>
                    item.IsAttentionRequested)
            ?.IdentityKey;
        WindowReference? activeWindow =
            TaskbarApps
                .SelectMany(item =>
                    item.Windows)
                .FirstOrDefault(window =>
                    window.IsActive);
        if (activeWindow != null
            && activeWindow.Handle
                != IntPtr.Zero)
        {
            RememberActiveWindow(
                activeWindow.Handle);
            _lastActiveExternalWindowHandle =
                activeWindow.Handle;
        }

        RefreshBackgroundAppViewPresentation();
        OnPropertyChanged(
            nameof(HasCompactTaskbarApps));
        OnPropertyChanged(
            nameof(RunningApplicationCount));
        OnPropertyChanged(
            nameof(RunningApplicationSummary));
    }

    [RelayCommand]
    private void RefreshRunningApplications() =>
        _windowTracker.RequestRefresh();

    private void RememberActiveWindow(
        IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        _recentWindowHandles.Remove(handle);
        _recentWindowHandles.Insert(0, handle);
        const int historyLimit = 32;
        if (_recentWindowHandles.Count
            > historyLimit)
        {
            _recentWindowHandles.RemoveRange(
                historyLimit,
                _recentWindowHandles.Count
                - historyLimit);
        }
    }

    private void RefreshBackgroundAppViewPresentation()
    {
        OnPropertyChanged(
            nameof(FilteredBackgroundApps));
        OnPropertyChanged(
            nameof(HasFilteredBackgroundApps));
        OnPropertyChanged(
            nameof(BackgroundAppResultSummary));
        OnPropertyChanged(
            nameof(BackgroundAppEmptyText));
    }

    private void ApplyTaskbarShortcutStates()
    {
        foreach (TaskbarAppItem item
                 in TaskbarApps)
        {
            item.SetShortcutState(
                TaskbarSlotShortcutState.None);
        }

        for (int index = 0;
             index < CompactTaskbarApps.Count;
             index++)
        {
            TaskbarAppItem item =
                CompactTaskbarApps[index];
            TaskbarSlotShortcutState state =
                _taskbarSlotHotkeyRegistration
                    .GetShortcutState(index);
            if (!item.CanLaunchNewInstance
                && state.CanLaunchNewInstance)
            {
                state = state with
                {
                    CanLaunchNewInstance =
                        false
                };
            }

            item.SetShortcutState(state);
        }
    }

    private void RefreshSearchResults()
    {
        string? selectedKey =
            SelectedSearchResult?.StableKey;
        int applicationLimit =
            ShellSearchEntryPolicy.GetApplicationLimit(
                SearchScope,
                SearchQuery);
        int resultLimit =
            ShellSearchEntryPolicy.GetResultLimit(
                SearchScope,
                SearchQuery);
        IReadOnlyList<AppLaunchItem> applications =
            _appCatalog.Search(
                SearchQuery,
                applicationLimit);
        bool isApplicationLauncherOverview =
            SearchScope
                == ShellSearchScope.Applications
            && string.IsNullOrWhiteSpace(
                SearchQuery);
        IReadOnlySet<string>?
            recentApplicationIdentities = null;
        if (isApplicationLauncherOverview)
        {
            applications =
                RecentAppHistoryPolicy
                    .OrderForLauncher(
                        applications,
                        _recentAppIdentities);
            recentApplicationIdentities =
                new HashSet<string>(
                    _recentAppIdentities,
                    StringComparer.OrdinalIgnoreCase);
        }
        IReadOnlyList<WindowTaskItem> windows =
            _windowTracker.GetSnapshot();
        RefreshWindowDisplayFilterOptions(
            windows);
        OpenWindowCount = windows
            .SelectMany(
                item => item.Windows)
            .Select(
                window => window.Handle)
            .Distinct()
            .Count();
        IReadOnlyList<ShellSearchResult> results =
            ShellSearchPolicy.Compose(
                applications,
                windows,
                SearchQuery,
                limit: resultLimit,
                taskItems:
                    _taskSearchItems,
                scope: SearchScope,
                windowIdentityFilter:
                    IsWindowApplicationFilterActive
                        ? _windowOverviewIdentityFilter
                        : null,
                windowDisplayDeviceFilter:
                    IsWindowDisplayFilterActive
                        ? SelectedWindowDisplayFilter
                        : null,
                recentApplicationIdentities:
                    recentApplicationIdentities,
                applicationAudioSessions:
                    ApplicationAudioSessions
                        .Select(item =>
                            new ApplicationAudioSessionSnapshot(
                                item.SessionId,
                                item.DisplayName,
                                item.ProcessId,
                                item.Volume,
                                item.IsMuted,
                                item.IsActive,
                                item.IsSystemSounds))
                        .ToArray(),
                recentWindowHandles:
                    _recentWindowHandles);
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

    private void RefreshWindowDisplayFilterOptions(
        IReadOnlyList<WindowTaskItem> windows)
    {
        IReadOnlyList<WindowDisplayFilterOption>
            options = WindowDisplayOverviewPolicy
                .CreateOptions(windows);
        string selection =
            WindowDisplayOverviewPolicy
                .NormalizeSelection(
                    SelectedWindowDisplayFilter,
                    options);
        _refreshingWindowDisplayFilter = true;
        try
        {
            ReplaceCollection(
                WindowDisplayFilterOptions,
                options);
            SelectedWindowDisplayFilter =
                selection;
        }
        finally
        {
            _refreshingWindowDisplayFilter =
                false;
        }

        OnPropertyChanged(
            nameof(IsWindowDisplayFilterVisible));
        OnPropertyChanged(
            nameof(IsWindowDisplayFilterActive));
        OnPropertyChanged(
            nameof(SearchPanelTitle));
    }

    private async Task
        RefreshTaskSearchIndexAsync()
    {
        IsTaskSearchLoading = true;
        TaskSearchIndexResult result =
            await _taskSearch
                .RefreshAsync();
        if (_isDisposed
            || !_taskSearch.IsCurrent(
                result.Revision))
        {
            return;
        }

        IsTaskSearchLoading = false;
        if (result.Succeeded)
        {
            TaskSearchError =
                string.Empty;
            _taskSearchItems =
                result.Items;
            RefreshSearchResults();
            return;
        }

        TaskSearchError =
            result.Error;
    }

    private void PrependTaskSearchItem(
        TaskSearchItem item)
    {
        _taskSearchItems =
            new[]
            {
                item
            }
            .Concat(
                _taskSearchItems.Where(
                    existing =>
                        existing.Id
                        != item.Id))
            .ToArray();
    }

    private void OnWindowSnapshotChanged(
        object? sender,
        EventArgs e)
    {
        _windowFocusSession.Reconcile(
            GetTrackedWindows());
        UpdateWindowFocusSessionState();
        RefreshTaskbarApps();
        RefreshSearchResults();
    }

    private void StartWindowFocusSession(
        IReadOnlyCollection<IntPtr> keepHandles,
        string targetLabel,
        WindowReference activationTarget)
    {
        WindowFocusStartResult result =
            _windowFocusSession.Start(
                GetTrackedWindows(),
                keepHandles,
                targetLabel,
                handle =>
                    _windowTracker.Minimize(handle),
                handle =>
                    _windowTracker.Restore(handle),
                handle =>
                    _windowTracker.Maximize(handle));
        UpdateWindowFocusSessionState();
        if (result.BlockedByPreviousSession)
        {
            ReportTaskbarActionFailure(
                "上一次窗口专注仍有 "
                + $"{result.PreviousRestoreFailedCount} 个窗口"
                + "未能恢复；请先重试恢复，再开始新的专注。");
            return;
        }

        bool activated =
            SystemActionExecution.Try(
                () => _windowTracker.Activate(
                    activationTarget.Handle));
        if (result.FailedCount > 0)
        {
            SystemActionMessage =
                $"已收起 {result.MinimizedCount} 个窗口；"
                + $"另有 {result.FailedCount} 个窗口"
                + "拒绝最小化并保持原状。";
        }
        else if (!activated)
        {
            SystemActionMessage =
                $"窗口专注已经建立，但无法切换到“"
                + $"{activationTarget.Title}”。";
        }
        else
        {
            SystemActionMessage =
                string.Empty;
        }
    }

    private IReadOnlyList<WindowReference>
        GetTrackedWindows() =>
        _windowTracker.GetSnapshot()
            .SelectMany(application =>
                application.Windows)
            .GroupBy(window =>
                window.Handle)
            .Select(group => group.First())
            .ToArray();

    private void UpdateWindowFocusSessionState()
    {
        HasWindowFocusSession =
            _windowFocusSession.HasActiveSession;
        WindowFocusSessionSummary =
            _windowFocusSession.Summary;
        WindowFocusHiddenWindowCountText =
            _windowFocusSession.HiddenWindowCount > 99
                ? "99+"
                : _windowFocusSession
                    .HiddenWindowCount
                    .ToString(ChineseCulture);
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
        long brightnessRevision =
            Volatile.Read(
                ref _brightnessRevision);
        bool brightnessWritePendingBeforeCapture =
            _brightnessWritePending;
        BrightnessStatusSnapshot brightness =
            _brightness.GetStatus();
        IReadOnlyList<
            ApplicationAudioSessionSnapshot>
            applicationAudioSessions =
                _applicationAudio.GetSessions();
        IReadOnlyList<SystemRadioSnapshot>
            radioSnapshots =
                _radios
                    .GetStatusAsync(
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
        IReadOnlyList<InputMethodOption>
            inputMethods =
                _systemStatus
                    .GetInputMethods();
        bool brightnessWritePendingAfterCapture =
            _brightnessWritePending;
        return new PendingSystemStatusSnapshot(
            snapshot,
            audioRevision,
            audioWritePendingBeforeCapture
                || audioWritePendingAfterCapture,
            brightness,
            brightnessRevision,
            brightnessWritePendingBeforeCapture
                || brightnessWritePendingAfterCapture,
            applicationAudioSessions,
            radioSnapshots,
            inputMethods);
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

        if (!pending.BrightnessWritePending
            && pending.BrightnessRevision
                == Volatile.Read(
                    ref _brightnessRevision))
        {
            BrightnessStatusSnapshot brightness =
                pending.Brightness;
            IsBrightnessAvailable =
                brightness.IsAvailable;
            BrightnessStatusText =
                brightness.Detail;
            if (brightness.IsAvailable)
            {
                RestoreConfirmedBrightness(
                    brightness.Percent);
            }
        }

        ApplyApplicationAudioSessions(
            pending.ApplicationAudioSessions);
        ApplySystemRadioSnapshots(
            pending.RadioSnapshots);

        NetworkStatusSnapshot network =
            snapshot.Network;
        IsNetworkAvailable = network.IsAvailable;
        NetworkConnectionKind =
            network.ConnectionKind;
        NetworkDisplayName = network.DisplayName;
        NetworkDetail = network.Detail;
        InputMethodStatus =
            snapshot.InputMethod;
        ReplaceCollection(
            InputMethods,
            pending.InputMethods);
        OnPropertyChanged(
            nameof(HasInputMethods));
        OnPropertyChanged(
            nameof(
                InputMethodListStatusText));
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

    private void RestoreConfirmedBrightness(
        int percent)
    {
        _confirmedBrightnessPercent =
            Math.Clamp(percent, 0, 100);
        SetDisplayedBrightness(
            _confirmedBrightnessPercent);
    }

    private void SetDisplayedBrightness(
        int percent)
    {
        _updatingBrightnessState = true;
        try
        {
            BrightnessPercent =
                Math.Clamp(percent, 0, 100);
        }
        finally
        {
            _updatingBrightnessState = false;
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
                "无法调整音量或静音。请检查默认音频输出设备；Panel 不会弹出任务栏快捷设置。");
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

    private void OnBrightnessControlCompleted(
        BrightnessControlOutcome outcome)
    {
        _uiDispatcher.BeginInvoke(
            () =>
                ApplyBrightnessControlOutcome(
                    outcome),
            DispatcherPriority.Background);
    }

    private void ApplyBrightnessControlOutcome(
        BrightnessControlOutcome outcome)
    {
        if (_isDisposed)
            return;

        bool isCurrent =
            outcome.Revision
            == Volatile.Read(
                ref _brightnessRevision);
        if (outcome.Succeeded)
        {
            _confirmedBrightnessPercent =
                outcome.Percent;
        }

        if (!isCurrent)
            return;

        _brightnessWritePending = false;
        if (outcome.Succeeded)
        {
            IsBrightnessAvailable = true;
            BrightnessStatusText =
                "内置显示器";
            SystemActionMessage = string.Empty;
            return;
        }

        SetDisplayedBrightness(
            _confirmedBrightnessPercent);
        ReportBrightnessFailure(
            "无法调整显示亮度。显示驱动可能正在切换，"
            + "或此显示器不支持 Windows 亮度接口。");
    }

    private void ReportBrightnessFailure(
        string message)
    {
        IsBrightnessAvailable = false;
        BrightnessStatusText =
            "内置显示器亮度控制暂时不可用";
        SystemActionMessage = message;
        CloseTransientPanels();
        IsStatusCenterOpen = true;
    }

    private void ApplyApplicationAudioSessions(
        IReadOnlyList<
            ApplicationAudioSessionSnapshot>
            snapshots)
    {
        bool hadSessions =
            ApplicationAudioSessions.Count > 0;
        var existing =
            ApplicationAudioSessions
                .ToDictionary(
                    item => item.SessionId,
                    StringComparer.Ordinal);
        var incomingIds =
            new HashSet<string>(
                snapshots.Select(
                    snapshot =>
                        snapshot.SessionId),
                StringComparer.Ordinal);
        for (int index = 0;
             index < snapshots.Count;
             index++)
        {
            ApplicationAudioSessionSnapshot
                snapshot = snapshots[index];
            if (!existing.TryGetValue(
                    snapshot.SessionId,
                    out ApplicationAudioSessionItem?
                        item))
            {
                item =
                    new ApplicationAudioSessionItem(
                        snapshot);
                item.VolumeRequested +=
                    ApplicationAudioSession_VolumeRequested;
                existing[snapshot.SessionId] =
                    item;
            }
            else if (!_applicationAudioWritePending
                         .ContainsKey(
                             snapshot.SessionId))
            {
                item.ApplySnapshot(snapshot);
            }

            int currentIndex =
                ApplicationAudioSessions
                    .IndexOf(item);
            if (currentIndex < 0)
            {
                ApplicationAudioSessions.Insert(
                    Math.Min(
                        index,
                        ApplicationAudioSessions.Count),
                    item);
            }
            else if (currentIndex != index)
            {
                ApplicationAudioSessions.Move(
                    currentIndex,
                    index);
            }
        }

        for (int index =
                 ApplicationAudioSessions.Count - 1;
             index >= 0;
             index--)
        {
            ApplicationAudioSessionItem item =
                ApplicationAudioSessions[index];
            if (incomingIds.Contains(item.SessionId)
                || _applicationAudioWritePending
                    .ContainsKey(item.SessionId))
            {
                continue;
            }

            item.VolumeRequested -=
                ApplicationAudioSession_VolumeRequested;
            ApplicationAudioSessions.RemoveAt(index);
            _applicationAudioRevisions.TryRemove(
                item.SessionId,
                out _);
        }

        ApplicationAudioStatusText =
            ApplicationAudioSessions.Count == 0
                ? IsAudioAvailable
                    ? "当前没有可调节的应用音频会话"
                    : "默认音频输出设备暂时不可用"
                : $"{ApplicationAudioSessions.Count} 个应用音频会话";
        if (hadSessions
            != HasApplicationAudioSessions)
        {
            OnPropertyChanged(
                nameof(
                    HasApplicationAudioSessions));
        }

        OnPropertyChanged(
            nameof(
                HasApplicationAudioSearchSuggestion));
        OnPropertyChanged(
            nameof(
                ApplicationAudioSearchSuggestion));

        if (IsSearchOpen)
            RefreshSearchResults();
    }

    private void ApplySystemRadioSnapshots(
        IReadOnlyList<SystemRadioSnapshot>
            snapshots)
    {
        SystemRadioSnapshot wiFi =
            snapshots.FirstOrDefault(
                snapshot =>
                    snapshot.Kind
                    == SystemRadioKind.WiFi);
        SystemRadioSnapshot bluetooth =
            snapshots.FirstOrDefault(
                snapshot =>
                    snapshot.Kind
                    == SystemRadioKind.Bluetooth);
        if (!IsWiFiRadioBusy)
        {
            WiFiRadioState =
                wiFi.IsPresent
                    ? wiFi.State
                    : SystemRadioState.Unavailable;
        }
        if (!IsBluetoothRadioBusy)
        {
            BluetoothRadioState =
                bluetooth.IsPresent
                    ? bluetooth.State
                    : SystemRadioState.Unavailable;
        }

        bool hasAny =
            wiFi.IsPresent
            || bluetooth.IsPresent;
        RadioStatusText = hasAny
            ? "首次切换时 Windows 可能请求无线控制权限"
            : "此设备未公开可控制的 Wi‑Fi 或蓝牙 Radio";
    }

    [RelayCommand]
    private Task ToggleWiFiRadio() =>
        ToggleSystemRadioAsync(
            SystemRadioKind.WiFi);

    [RelayCommand]
    private Task ToggleBluetoothRadio() =>
        ToggleSystemRadioAsync(
            SystemRadioKind.Bluetooth);

    [RelayCommand]
    private Task RefreshWifiNetworks()
    {
        IsWifiNetworkListVisible = true;
        return RefreshWifiNetworksCoreAsync(true);
    }

    [RelayCommand]
    private Task RefreshBluetoothDevices()
    {
        IsBluetoothDeviceListVisible = true;
        return RefreshBluetoothDevicesCoreAsync();
    }

    [RelayCommand]
    private async Task ManageBluetoothDevice(
        BluetoothDeviceSnapshot? device)
    {
        if (device == null
            || IsBluetoothDeviceBusy
            || !device.CanInvokeAction)
        {
            return;
        }

        if (device.IsPaired)
        {
            MessageBoxResult confirmation =
                FocusDialogService.Show(
                    $"要从这台电脑移除“{device.DisplayName}”吗？",
                    "移除蓝牙设备",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        long revision =
            Interlocked.Increment(
                ref _bluetoothDeviceRevision);
        IsBluetoothDeviceBusy = true;
        BluetoothDeviceStatusText =
            device.IsPaired
                ? $"正在移除 {device.DisplayName}…"
                : $"正在配对 {device.DisplayName}；请确认 Windows 安全提示…";
        Task<BluetoothDeviceOperationResult>?
            operation =
                _bluetoothDeviceOperations.TryStart(
                    () =>
                        device.IsPaired
                            ? _bluetoothDevices.UnpairAsync(
                                device,
                                CancellationToken.None)
                            : _bluetoothDevices.PairAsync(
                                device,
                                CancellationToken.None));
        if (operation == null)
        {
            IsBluetoothDeviceBusy = false;
            return;
        }

        BluetoothDeviceOperationResult result =
            await operation;
        if (_isDisposed
            || revision
                != Volatile.Read(
                    ref _bluetoothDeviceRevision))
        {
            return;
        }

        IsBluetoothDeviceBusy = false;
        BluetoothDeviceStatusText =
            ComposeBluetoothOperationStatus(result);
        if (result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            await RefreshBluetoothDevicesCoreAsync();
        }
        else if (result.Status
                 != BluetoothDeviceOperationStatus.Canceled)
        {
            SystemActionMessage =
                BluetoothDeviceStatusText;
            IsStatusCenterOpen = true;
        }
    }

    [RelayCommand]
    private async Task ConnectWifiNetwork(
        WifiNetworkSnapshot? network)
    {
        if (network == null
            || IsWifiNetworkBusy)
        {
            return;
        }

        if (network.IsConnected)
        {
            await ManageWifiNetworkCoreAsync(
                network,
                WifiNetworkManageAction.Disconnect);
            return;
        }

        if (network.NeedsCredentials)
        {
            IsStatusCenterOpen = true;
            WifiCredentialsRequested?.Invoke(network);
            return;
        }

        if (!network.HasProfile)
        {
            using var emptyPassword =
                new SecureString();
            await ConnectWifiNetworkCoreAsync(
                network,
                emptyPassword);
            return;
        }

        await ConnectWifiNetworkCoreAsync(
            network,
            null);
    }

    [RelayCommand]
    private async Task ForgetWifiNetwork(
        WifiNetworkSnapshot? network)
    {
        if (network == null
            || IsWifiNetworkBusy
            || !network.CanForget)
        {
            return;
        }

        string consequence = network.IsConnected
            ? "Panel 会先断开当前连接，再删除 Windows 保存的网络配置。"
            : "Windows 保存的网络配置将被删除，下次连接需要重新输入密码。";
        MessageBoxResult confirmation =
            FocusDialogService.Show(
                $"确定要忘记“{network.DisplayName}”吗？\n\n{consequence}",
                "忘记 Wi‑Fi 网络",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
            return;

        await ManageWifiNetworkCoreAsync(
            network,
            WifiNetworkManageAction.Forget);
    }

    public Task ConnectWifiNetworkWithCredentialsAsync(
        WifiNetworkSnapshot network,
        SecureString password) =>
        ConnectWifiNetworkCoreAsync(
            network,
            password);

    private async Task ConnectWifiNetworkCoreAsync(
        WifiNetworkSnapshot network,
        SecureString? password)
    {
        if (IsWifiNetworkBusy)
            return;

        long revision =
            Interlocked.Increment(
                ref _wifiNetworkRevision);
        IsWifiNetworkBusy = true;
        WifiNetworkStatusText =
            $"正在连接 {network.DisplayName}…";
        Task<WifiNetworkConnectResult>?
            operation =
                _wifiNetworkOperations.TryStart(
                    () =>
                        password == null
                            ? _wifiNetworks.ConnectAsync(
                                network,
                                CancellationToken.None)
                            : _wifiNetworks
                                .ConnectWithCredentialsAsync(
                                    network,
                                    password,
                                    CancellationToken.None));
        if (operation == null)
        {
            IsWifiNetworkBusy = false;
            return;
        }

        WifiNetworkConnectResult result =
            await operation;
        if (_isDisposed
            || revision
                != Volatile.Read(
                    ref _wifiNetworkRevision))
        {
            return;
        }

        IsWifiNetworkBusy = false;
        if (result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            WifiNetworkStatusText =
                $"已连接 {result.DisplayName}";
            await RefreshWifiNetworksCoreAsync(false);
            return;
        }

        SystemActionMessage =
            password != null
            && result.Status
                == WifiNetworkConnectStatus.NotConfirmed
                ? $"无法连接“{result.DisplayName}”：密码不正确或网络未响应。"
                  + "Panel 已移除这次失败的配置，可直接重新输入。"
                : ComposeWifiConnectFailure(result);
        WifiNetworkStatusText =
            "连接没有完成；Panel 已保留网络列表，可直接重试";
        IsStatusCenterOpen = true;
    }

    private async Task ManageWifiNetworkCoreAsync(
        WifiNetworkSnapshot network,
        WifiNetworkManageAction action)
    {
        if (IsWifiNetworkBusy)
            return;

        long revision =
            Interlocked.Increment(
                ref _wifiNetworkRevision);
        IsWifiNetworkBusy = true;
        string actionText =
            action == WifiNetworkManageAction.Forget
                ? "忘记"
                : "断开";
        WifiNetworkStatusText =
            $"正在{actionText} {network.DisplayName}…";
        Task<WifiNetworkManageResult>? operation =
            _wifiNetworkOperations.TryStart(
                () =>
                    action
                        == WifiNetworkManageAction.Forget
                        ? _wifiNetworks.ForgetAsync(
                            network,
                            CancellationToken.None)
                        : _wifiNetworks.DisconnectAsync(
                            network,
                            CancellationToken.None));
        if (operation == null)
        {
            IsWifiNetworkBusy = false;
            return;
        }

        WifiNetworkManageResult result =
            await operation;
        if (_isDisposed
            || revision
                != Volatile.Read(
                    ref _wifiNetworkRevision))
        {
            return;
        }

        IsWifiNetworkBusy = false;
        WifiNetworkStatusText =
            ComposeWifiManageStatus(result);
        if (result.Succeeded)
        {
            SystemActionMessage = string.Empty;
            await RefreshWifiNetworksCoreAsync(false);
            RequestSystemStatusRefresh();
            return;
        }

        SystemActionMessage =
            WifiNetworkStatusText;
        IsStatusCenterOpen = true;
    }

    private async Task
        RefreshWifiNetworksCoreAsync(
            bool requestScan)
    {
        if (_isDisposed
            || IsWifiNetworkBusy)
            return;

        long revision =
            Interlocked.Increment(
                ref _wifiNetworkRevision);
        IsWifiNetworkBusy = true;
        WifiNetworkStatusText = requestScan
            ? "Windows 正在扫描附近 Wi‑Fi…"
            : "正在更新 Wi‑Fi 状态…";
        Task<WifiNetworkListResult>? operation =
            _wifiNetworkOperations.TryStart(
                () =>
                    _wifiNetworks.GetNetworksAsync(
                        requestScan,
                        CancellationToken.None));
        if (operation == null)
        {
            IsWifiNetworkBusy = false;
            return;
        }

        WifiNetworkListResult result =
            await operation;
        if (_isDisposed
            || revision
                != Volatile.Read(
                    ref _wifiNetworkRevision))
        {
            return;
        }

        IsWifiNetworkBusy = false;
        ReplaceCollection(
            WifiNetworks,
            result.Networks);
        OnPropertyChanged(
            nameof(HasWifiNetworks));
        HasWifiLocationAccessWarning =
            result.Status
            == WifiNetworkListStatus.AccessDenied;
        WifiNetworkStatusText =
            ComposeWifiListStatus(result);
    }

    private static string ComposeWifiListStatus(
        WifiNetworkListResult result)
    {
        int savedOutOfRange =
            result.Networks.Count(network =>
                network.IsSavedOutOfRange);
        int nearby =
            result.Networks.Count
            - savedOutOfRange;
        return result.Status switch
        {
            WifiNetworkListStatus.Succeeded
                when nearby > 0
                    && savedOutOfRange > 0 =>
                $"{nearby} 个附近网络 · "
                + $"{savedOutOfRange} 个已保存网络当前不在附近",
            WifiNetworkListStatus.Succeeded
                when nearby > 0 =>
                $"找到 {nearby} 个附近网络；"
                + "首次密码也可直接在 Panel 输入",
            WifiNetworkListStatus.Succeeded
                when savedOutOfRange > 0 =>
                $"已列出 {savedOutOfRange} 个已保存网络；"
                + "当前没有可连接的附近网络",
            WifiNetworkListStatus.Succeeded =>
                "附近暂时没有可显示的 Wi‑Fi",
            WifiNetworkListStatus.AccessDenied =>
                "Windows 未授予精确位置权限，"
                + "无法列出附近 Wi‑Fi",
            WifiNetworkListStatus.RadioOff =>
                "Wi‑Fi Radio 已关闭，请先开启",
            WifiNetworkListStatus.NoAdapter =>
                "没有检测到 Windows WLAN 适配器",
            WifiNetworkListStatus
                .ServiceUnavailable =>
                "Windows WLAN AutoConfig 服务不可用",
            _ =>
                "无法读取附近 Wi‑Fi，请检查无线服务后重试"
        };
    }

    private async Task
        RefreshBluetoothDevicesCoreAsync()
    {
        if (_isDisposed
            || IsBluetoothDeviceBusy)
        {
            return;
        }

        long revision =
            Interlocked.Increment(
                ref _bluetoothDeviceRevision);
        IsBluetoothDeviceBusy = true;
        BluetoothDeviceStatusText =
            BluetoothRadioState == SystemRadioState.Off
                ? "蓝牙已关闭；开启后才能发现附近设备"
                : "正在读取已配对和附近蓝牙设备…";
        Task<BluetoothDeviceListResult>? operation =
            _bluetoothDeviceOperations.TryStart(
                () =>
                    _bluetoothDevices.GetDevicesAsync(
                        CancellationToken.None));
        if (operation == null)
        {
            IsBluetoothDeviceBusy = false;
            return;
        }

        BluetoothDeviceListResult result =
            await operation;
        if (_isDisposed
            || revision
                != Volatile.Read(
                    ref _bluetoothDeviceRevision))
        {
            return;
        }

        IsBluetoothDeviceBusy = false;
        ReplaceCollection(
            BluetoothDevices,
            result.Devices);
        OnPropertyChanged(
            nameof(HasBluetoothDevices));
        BluetoothDeviceStatusText =
            ComposeBluetoothListStatus(result);
    }

    private string ComposeBluetoothListStatus(
        BluetoothDeviceListResult result) =>
        result.Status switch
        {
            BluetoothDeviceListStatus.Succeeded
                when result.Devices.Count > 0 =>
                $"找到 {result.Devices.Count} 个设备；已连接和已配对设备优先显示",
            BluetoothDeviceListStatus.Succeeded
                when BluetoothRadioState
                    == SystemRadioState.Off =>
                "蓝牙已关闭；请先在 Panel 中开启蓝牙",
            BluetoothDeviceListStatus.Succeeded =>
                "暂未发现可显示的蓝牙设备",
            BluetoothDeviceListStatus.AccessDenied =>
                "Windows 未授予蓝牙设备访问权限",
            BluetoothDeviceListStatus.Unavailable =>
                "当前系统不支持 Panel 蓝牙设备管理",
            _ =>
                "无法读取蓝牙设备，请检查蓝牙驱动后重试"
        };

    private static string
        ComposeBluetoothOperationStatus(
            BluetoothDeviceOperationResult result)
    {
        string action =
            result.RequestedPaired
                ? "配对"
                : "移除";
        return result.Status switch
        {
            BluetoothDeviceOperationStatus.Succeeded
                or BluetoothDeviceOperationStatus
                    .AlreadyInDesiredState =>
                result.RequestedPaired
                    ? $"已配对 {result.DisplayName}"
                    : $"已移除 {result.DisplayName}",
            BluetoothDeviceOperationStatus.Canceled =>
                $"已取消{action} {result.DisplayName}",
            BluetoothDeviceOperationStatus.NotFound =>
                $"{result.DisplayName} 已离开范围，请刷新后重试",
            BluetoothDeviceOperationStatus.AccessDenied =>
                $"Windows 拒绝{action} {result.DisplayName}；请检查蓝牙权限",
            BluetoothDeviceOperationStatus.NotReady =>
                $"{result.DisplayName} 暂未准备好{action}",
            BluetoothDeviceOperationStatus
                .AuthenticationFailed =>
                $"{result.DisplayName} 的安全验证未通过",
            BluetoothDeviceOperationStatus.Rejected =>
                $"{result.DisplayName} 拒绝了{action}请求",
            BluetoothDeviceOperationStatus.NotConfirmed =>
                $"已请求{action} {result.DisplayName}，但 Windows 未确认完成",
            _ =>
                $"无法{action} {result.DisplayName}，请刷新后重试"
        };
    }

    private static string
        ComposeWifiConnectFailure(
            WifiNetworkConnectResult result) =>
        result.Status switch
        {
            WifiNetworkConnectStatus
                .NeedsCredentials =>
                $"“{result.DisplayName}”需要首次输入密码，"
                + "请直接在 Panel 中输入。",
            WifiNetworkConnectStatus
                .InvalidCredentials =>
                "密码长度应为 8–63 个字符，请重新输入。",
            WifiNetworkConnectStatus.AccessDenied =>
                "Windows 拒绝读取 Wi‑Fi 连接状态。"
                + "请检查精确位置权限。",
            WifiNetworkConnectStatus.RadioOff =>
                "Wi‑Fi Radio 已关闭，无法连接。",
            WifiNetworkConnectStatus
                .ServiceUnavailable =>
                "Windows WLAN AutoConfig 服务不可用。",
            WifiNetworkConnectStatus.NotFound =>
                $"“{result.DisplayName}”已离开范围"
                + "或保存的配置已删除。",
            WifiNetworkConnectStatus.NotConfirmed =>
                $"已请求连接“{result.DisplayName}”，"
                + "但 Windows 未确认连接成功。",
            _ =>
                $"无法连接“{result.DisplayName}”，请在 Panel 中刷新后重试。"
        };

    private static string ComposeWifiManageStatus(
        WifiNetworkManageResult result)
    {
        bool forgetting =
            result.Action
            == WifiNetworkManageAction.Forget;
        string action = forgetting
            ? "忘记"
            : "断开";
        return result.Status switch
        {
            WifiNetworkManageStatus.Succeeded
                or WifiNetworkManageStatus
                    .AlreadyInDesiredState =>
                forgetting
                    ? $"已忘记 {result.DisplayName}"
                    : $"已断开 {result.DisplayName}",
            WifiNetworkManageStatus.NotFound =>
                $"{result.DisplayName} 或对应无线适配器已不存在",
            WifiNetworkManageStatus.AccessDenied =>
                $"Windows 拒绝{action} {result.DisplayName}；当前账户没有所需的网络配置权限",
            WifiNetworkManageStatus.RadioOff =>
                $"Wi‑Fi Radio 已关闭，无法完成{action}",
            WifiNetworkManageStatus
                .ServiceUnavailable =>
                $"Windows WLAN AutoConfig 服务不可用，无法完成{action}",
            WifiNetworkManageStatus.NotConfirmed =>
                $"已请求{action} {result.DisplayName}，但 Windows 未确认状态改变",
            _ =>
                $"无法{action} {result.DisplayName}，请刷新网络后重试"
        };
    }

    private async Task ToggleSystemRadioAsync(
        SystemRadioKind kind)
    {
        bool isWiFi =
            kind == SystemRadioKind.WiFi;
        SystemRadioState current =
            isWiFi
                ? WiFiRadioState
                : BluetoothRadioState;
        bool canToggle =
            isWiFi
                ? CanToggleWiFiRadio
                : CanToggleBluetoothRadio;
        if (!canToggle)
            return;

        bool targetEnabled =
            current == SystemRadioState.Off;
        if (isWiFi)
            IsWiFiRadioBusy = true;
        else
            IsBluetoothRadioBusy = true;

        Task<SystemRadioSetResult>? operation =
            _radioOperations.TryStart(
                () =>
                    _radios.SetEnabledAsync(
                        kind,
                        targetEnabled,
                        CancellationToken.None));
        if (operation == null)
        {
            if (isWiFi)
                IsWiFiRadioBusy = false;
            else
                IsBluetoothRadioBusy = false;
            return;
        }

        SystemRadioSetResult result =
            await operation;
        if (_isDisposed)
            return;

        if (isWiFi)
            IsWiFiRadioBusy = false;
        else
            IsBluetoothRadioBusy = false;
        if (result.Succeeded)
        {
            SystemRadioState confirmed =
                targetEnabled
                    ? SystemRadioState.On
                    : SystemRadioState.Off;
            if (isWiFi)
                WiFiRadioState = confirmed;
            else
                BluetoothRadioState = confirmed;
            SystemActionMessage = string.Empty;
        }
        else
        {
            SystemActionMessage =
                ComposeRadioFailureMessage(result);
            IsStatusCenterOpen = true;
        }

        RequestSystemStatusRefresh();
    }

    private static string ComposeRadioFailureMessage(
        SystemRadioSetResult result)
    {
        string name =
            result.Kind == SystemRadioKind.WiFi
                ? "Wi‑Fi"
                : "蓝牙";
        return result.Status switch
        {
            SystemRadioSetStatus.DeniedByUser =>
                $"Windows 未授予 {name} 控制权限。"
                + "请在系统隐私权限中允许无线控制。",
            SystemRadioSetStatus.DeniedBySystem =>
                $"{name} 被系统策略禁止由应用控制。"
                + "Panel 不会绕过系统策略。",
            SystemRadioSetStatus.HardwareDisabled =>
                $"{name} 已被硬件开关或驱动禁用，"
                + "Panel 无法强制开启。",
            SystemRadioSetStatus.NotFound =>
                $"未找到可控制的 {name} Radio。"
                + "设备可能刚刚移除。",
            SystemRadioSetStatus.NotConfirmed =>
                $"{name} 切换请求已发送，但硬件状态没有确认改变。"
                + "请检查飞行模式、驱动或硬件开关。",
            _ =>
                $"无法切换 {name}，请在 Panel 中刷新状态后重试。"
        };
    }

    private void
        ApplicationAudioSession_VolumeRequested(
            ApplicationAudioSessionItem item,
            float volume)
    {
        long revision =
            Interlocked.Increment(
                ref _applicationAudioRevision);
        _applicationAudioRevisions[
            item.SessionId] = revision;
        _applicationAudioWritePending[
            item.SessionId] = 0;
        if (_applicationAudioControl.QueueVolume(
                item.SessionId,
                revision,
                volume))
        {
            return;
        }

        _applicationAudioWritePending.TryRemove(
            item.SessionId,
            out _);
        item.ApplyDisplayedVolume(
            item.ConfirmedVolume);
    }

    [RelayCommand]
    private void ToggleApplicationAudioMute(
        ApplicationAudioSessionItem? item)
    {
        if (item == null)
            return;

        SetApplicationAudioMuted(
            item,
            !item.IsMuted);
    }

    private void SetApplicationAudioMuted(
        ApplicationAudioSessionItem item,
        bool target)
    {
        if (item.IsMuted == target)
            return;

        item.ApplyDisplayedMuted(target);
        long revision =
            Interlocked.Increment(
                ref _applicationAudioRevision);
        _applicationAudioRevisions[
            item.SessionId] = revision;
        _applicationAudioWritePending[
            item.SessionId] = 0;
        if (_applicationAudioControl.QueueMuted(
                item.SessionId,
                revision,
                target))
        {
            return;
        }

        _applicationAudioWritePending.TryRemove(
            item.SessionId,
            out _);
        item.ApplyDisplayedMuted(
            item.ConfirmedMuted);
    }

    private void OnApplicationAudioControlCompleted(
        ApplicationAudioControlOutcome outcome)
    {
        _uiDispatcher.BeginInvoke(
            () =>
                ApplyApplicationAudioControlOutcome(
                    outcome),
            DispatcherPriority.Background);
    }

    private void ApplyApplicationAudioControlOutcome(
        ApplicationAudioControlOutcome outcome)
    {
        if (_isDisposed)
            return;

        ApplicationAudioControlMutation mutation =
            outcome.Mutation;
        ApplicationAudioSessionItem? item =
            ApplicationAudioSessions
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.SessionId,
                            mutation.SessionId,
                            StringComparison.Ordinal));
        if (item != null)
        {
            if (mutation.Volume is float volume
                && outcome.VolumeSucceeded == true)
            {
                item.ConfirmVolume(volume);
            }
            if (mutation.IsMuted is bool muted
                && outcome.MuteSucceeded == true)
            {
                item.ConfirmMuted(muted);
            }
        }

        if (!_applicationAudioRevisions
                .TryGetValue(
                    mutation.SessionId,
                    out long currentRevision)
            || currentRevision
                != mutation.Revision)
        {
            return;
        }

        _applicationAudioWritePending.TryRemove(
            mutation.SessionId,
            out _);
        bool failed =
            outcome.VolumeSucceeded == false
            || outcome.MuteSucceeded == false;
        if (item != null && failed)
        {
            if (outcome.VolumeSucceeded == false)
            {
                item.ApplyDisplayedVolume(
                    item.ConfirmedVolume);
            }
            if (outcome.MuteSucceeded == false)
            {
                item.ApplyDisplayedMuted(
                    item.ConfirmedMuted);
            }
        }

        if (failed)
        {
            SystemActionMessage =
                $"无法调整“{item?.DisplayName ?? "该应用"}”的音量。"
                + "音频会话可能已经结束或默认输出设备正在切换。";
            IsStatusCenterOpen = true;
            RequestSystemStatusRefresh();
            return;
        }

        SystemActionMessage = string.Empty;
        RequestSystemStatusRefresh();
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
                IsStatusCenterOpen);
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
        if (!completion.Succeeded)
            WorkspaceRequested?.Invoke("Status");
    }

    private async Task RunInlineStatusActionAsync(
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

        SystemActionMessage =
            completion.Succeeded
                ? string.Empty
                : error;
        if (!completion.Succeeded)
        {
            CloseTransientPanels();
            IsStatusCenterOpen = true;
        }
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
            RecordSuccessfulAppLaunch(app);
            return true;
        }

        SystemActionMessage =
            $"无法启动“{app.DisplayName}”。应用可能已卸载，"
            + "或固定目标已经移动；请在搜索中重新固定。";
        CloseTransientPanels();
        IsStatusCenterOpen = true;
        return false;
    }

    private void RecordSuccessfulAppLaunch(
        AppLaunchItem app)
    {
        IReadOnlyList<string> updated =
            RecentAppHistoryPolicy.Record(
                _recentAppIdentities,
                app.IdentityKey);
        if (_recentAppIdentities.SequenceEqual(
                updated,
                StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _recentAppIdentities = updated;
        Interlocked.Increment(
            ref _recentAppHistoryRevision);
        QueueShellPreference(
            ShellPreferenceRepository
                .RecentAppHistoryKey,
            RecentAppHistoryPolicy.Serialize(
                updated));
        if (IsApplicationLauncherOpen
            && string.IsNullOrWhiteSpace(
                SearchQuery))
        {
            RefreshSearchResults();
        }
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
        _windowFocusSession.Restore(
            GetTrackedWindows(),
            handle =>
                _windowTracker.Restore(handle),
            handle =>
                _windowTracker.Maximize(handle));
        UpdateWindowFocusSessionState();
        _isDisposed = true;
        _audioControl.Completed -=
            OnAudioControlCompleted;
        _brightnessControl.Completed -=
            OnBrightnessControlCompleted;
        _applicationAudioControl.Completed -=
            OnApplicationAudioControlCompleted;
        foreach (ApplicationAudioSessionItem item
                 in ApplicationAudioSessions)
        {
            item.VolumeRequested -=
                ApplicationAudioSession_VolumeRequested;
        }
        _shellPreferences.SaveFailed -=
            OnShellPreferenceSaveFailed;
        _notificationCenter.Changed -=
            NotificationCenter_Changed;
        _notificationCenter.PersistenceStatusChanged -=
            NotificationCenter_PersistenceStatusChanged;
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
                _brightnessControl.CompleteAsync(),
                _applicationAudioControl
                    .CompleteAsync(),
                _radioOperations.CompleteAsync(),
                _wifiNetworkOperations
                    .CompleteAsync(),
                _bluetoothDeviceOperations
                    .CompleteAsync(),
                _autoStartup.CompleteAsync(),
                _taskCapture.CompleteAsync(),
                _taskSearch.CompleteAsync(),
                _shellPreferences.CompleteAsync()
            };
        if (_tasksViewModel != null)
        {
            completions.Add(
                _tasksViewModel
                    .DisposeAsync());
        }

        _aiAssistantViewModel?.Dispose();
        if (_fileOrganizerViewModel != null)
        {
            _fileOrganizerViewModel
                .CollectedCountChanged -=
                FileOrganizer_CollectedCountChanged;
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
            _brightnessControl.Dispose();
            _applicationAudioControl.Dispose();
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
            bool AudioWritePending,
            BrightnessStatusSnapshot Brightness,
            long BrightnessRevision,
            bool BrightnessWritePending,
            IReadOnlyList<
                ApplicationAudioSessionSnapshot>
                ApplicationAudioSessions,
            IReadOnlyList<SystemRadioSnapshot>
                RadioSnapshots,
            IReadOnlyList<InputMethodOption>
                InputMethods);
}
