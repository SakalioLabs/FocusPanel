using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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

    private readonly IAppCatalogService _appCatalog;
    private readonly IWindowTracker _windowTracker;
    private readonly ISystemStatusService _systemStatus;
    private readonly IAppUpdateService _updateService;
    private readonly IDesktopItemVisibilityService _desktopVisibility;
    private readonly DispatcherTimer _clockTimer;
    private TasksViewModel? _tasksViewModel;
    private PomodoroViewModel? _pomodoroViewModel;
    private FileOrganizerViewModel? _fileOrganizerViewModel;
    private OkrViewModel? _okrViewModel;
    private AIAssistantViewModel? _aiAssistantViewModel;
    private bool _updatingAudioState;

    [ObservableProperty]
    private string title = "FocusPanel";

    [ObservableProperty]
    private object currentViewModel;

    [ObservableProperty]
    private string currentSectionTitle = "桌面收纳";

    [ObservableProperty]
    private DateTime currentTime;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isSearchOpen;

    [ObservableProperty]
    private bool isCalendarOpen;

    [ObservableProperty]
    private bool isQuickSettingsOpen;

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
    private bool startWithWindows;

    [ObservableProperty]
    private string themeMode = "System";

    [ObservableProperty]
    private bool disableHotZoneInFullscreen = true;

    [ObservableProperty]
    private float masterVolume;

    [ObservableProperty]
    private bool isMuted;

    [ObservableProperty]
    private bool isNetworkAvailable;

    [ObservableProperty]
    private string networkDisplayName = "未连接";

    [ObservableProperty]
    private string networkDetail = "当前没有可用连接";

    [ObservableProperty]
    private string inputLanguageDisplay = "—";

    [ObservableProperty]
    private string inputMethodDisplay = "—";

    [ObservableProperty]
    private bool hasBattery;

    [ObservableProperty]
    private int batteryPercent;

    [ObservableProperty]
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

        CurrentTime = DateTime.Now;
        CurrentAppVersion = _updateService.CurrentVersion;
        UpdateStatus = _updateService.CanUpdate
            ? "点击按钮检查 GitHub Releases"
            : "当前为开发运行版；安装发布包后可一键更新";
        StartWithWindows = AutoStartupService.IsStartupEnabled();
        bool firstRunAccepted = ReadBooleanConfig(FirstRunAcceptedKey);
        IsReplacementEnabled = ReadBooleanConfig(ReplacementEnabledKey);
        ReplacementStatus = IsReplacementEnabled
            ? "正在接管主屏任务栏"
            : "未接管，Windows 任务栏保持显示";
        ThemeMode = ReadStringConfig(ThemeModeKey, "System");
        DisableHotZoneInFullscreen = ReadBooleanConfig(FullscreenHotZoneKey, true);
        ShowsProtectedSystemFiles = _desktopVisibility.ShowsProtectedSystemFiles;
        ThemeService.SetMode(ThemeMode);
        // Replacement is the product's primary mode. Never hide the taskbar without
        // a click, but keep the activation screen discoverable until it is enabled.
        IsOnboardingVisible = !firstRunAccepted || !IsReplacementEnabled;

        _fileOrganizerViewModel = new FileOrganizerViewModel();
        CurrentViewModel = _fileOrganizerViewModel;

        RefreshPinnedApps();
        RefreshSearchResults();
        RefreshRunningApps();
        RefreshStatus();

        _windowTracker.SnapshotChanged += OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged += OnCatalogChanged;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now;
            RefreshStatus();
        };
        _clockTimer.Start();
    }

    public ObservableCollection<AppLaunchItem> PinnedApps { get; } = new();
    public ObservableCollection<AppLaunchItem> SearchResults { get; } = new();
    public ObservableCollection<WindowTaskItem> RunningApps { get; } = new();

    public event Action? RequestClose;
    public event Action? RequestEnableReplacement;
    public event Action? RequestDisableReplacement;
    public event Action? RequestApplyUpdate;
    public event Action<string>? WorkspaceRequested;

    partial void OnSearchQueryChanged(string value)
    {
        IsSearchOpen = true;
        RefreshSearchResults();
    }

    partial void OnMasterVolumeChanged(float value)
    {
        if (!_updatingAudioState)
            _systemStatus.MasterVolume = value;
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (!_updatingAudioState)
            _systemStatus.IsMuted = value;
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
        if (IsReplacementEnabled)
            AutoStartupService.SetStartup(value);
    }

    [RelayCommand]
    private void Navigate(string? destination)
    {
        switch (destination)
        {
            case "Tasks":
                _tasksViewModel ??= new TasksViewModel();
                CurrentViewModel = _tasksViewModel;
                CurrentSectionTitle = "任务";
                break;
            case "Pomodoro":
                _pomodoroViewModel ??= new PomodoroViewModel();
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

        WorkspaceRequested?.Invoke(destination);
    }

    [RelayCommand]
    private void LaunchApp(AppLaunchItem? app)
    {
        if (app == null)
            return;
        _appCatalog.Launch(app);
        IsSearchOpen = false;
    }

    [RelayCommand]
    private void TogglePin(AppLaunchItem? app)
    {
        if (app == null)
            return;

        _appCatalog.SetPinned(app, !app.IsPinned);
        RefreshPinnedApps();
        RefreshSearchResults();
    }

    public void MovePinned(AppLaunchItem source, AppLaunchItem target)
    {
        int targetIndex = PinnedApps.IndexOf(target);
        if (targetIndex < 0 || ReferenceEquals(source, target))
            return;

        _appCatalog.MovePinned(source, targetIndex);
        RefreshPinnedApps();
    }

    [RelayCommand]
    private void ActivateTask(WindowTaskItem? task)
    {
        if (task != null)
            _windowTracker.ActivateOrMinimize(task);
    }

    [RelayCommand]
    private void ActivateWindow(WindowReference? window)
    {
        if (window != null)
            _windowTracker.Activate(window.Handle);
    }

    [RelayCommand]
    private void CloseWindow(WindowReference? window)
    {
        if (window != null)
            _windowTracker.Close(window.Handle);
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchOpen = !IsSearchOpen;
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
    private void ToggleQuickSettings()
    {
        bool open = !IsQuickSettingsOpen;
        CloseTransientPanels();
        IsQuickSettingsOpen = open;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowsProtectedSystemFiles = _desktopVisibility.ShowsProtectedSystemFiles;
        IsSettingsOpen = !IsSettingsOpen;
    }

    [RelayCommand]
    private void TogglePowerMenu()
    {
        bool open = !IsPowerMenuOpen;
        CloseTransientPanels();
        IsPowerMenuOpen = open;
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void OpenQuickSettings()
        => SetSystemActionResult(
            _systemStatus.OpenQuickSettings(),
            "无法唤起 Windows 快捷设置，请使用 Win+A。");

    [RelayCommand]
    private void OpenNotificationOverflow()
        => SetSystemActionResult(
            _systemStatus.OpenNotificationOverflow(),
            "无法打开系统托盘浮层；请先恢复 Windows 任务栏后重试。");

    [RelayCommand]
    private void OpenNotifications()
        => SetSystemActionResult(
            _systemStatus.OpenNotifications(),
            "无法唤起 Windows 通知中心，请使用 Win+N。");

    [RelayCommand]
    private void OpenInputSwitcher()
        => SetSystemActionResult(
            _systemStatus.OpenInputSwitcher(),
            "无法唤起输入法切换器，请使用 Win+Space。");

    [RelayCommand]
    private void OpenPowerSettings() => _systemStatus.OpenPowerSettings();

    [RelayCommand]
    private void ShowDesktop() => _systemStatus.ShowDesktop();

    [RelayCommand]
    private void LockComputer() => _systemStatus.Lock();

    [RelayCommand]
    private void SleepComputer() => _systemStatus.Sleep();

    [RelayCommand]
    private void RestartComputer()
    {
        if (MessageBox.Show("确定要立即重启电脑吗？", "重启电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            _systemStatus.Restart();
        }
    }

    [RelayCommand]
    private void ShutdownComputer()
    {
        if (MessageBox.Show("确定要立即关闭电脑吗？", "关闭电脑", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes)
        {
            _systemStatus.Shutdown();
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
        ReplacementStatus = enabled
            ? "已接管主屏任务栏 · 紧急恢复 Ctrl+Alt+Shift+F10"
            : "未接管，Windows 任务栏保持显示";
        ReplacementError = error ?? string.Empty;
        SaveBooleanConfig(FirstRunAcceptedKey, true);
        SaveBooleanConfig(ReplacementEnabledKey, enabled);
        AutoStartupService.SetStartup(enabled && StartWithWindows);
    }

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
        var result = MessageBox.Show(
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
            Process.Start(new ProcessStartInfo(executable, "--restore") { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法重启应用：{ex.Message}", "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                UpdateStatus = $"已是最新版本 v{CurrentAppVersion}";
                return;
            }

            string sizeText = update.DownloadSize > 0
                ? $"{update.DownloadSize / 1024d / 1024d:F1} MB"
                : "未知大小";
            string notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "本次版本未提供更新说明。"
                : update.ReleaseNotes.Trim();
            if (notes.Length > 600)
                notes = notes[..600] + "…";

            MessageBoxResult result = MessageBox.Show(
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
            UpdateStatus = $"更新失败：{ex.Message}";
            MessageBox.Show(
                $"无法完成更新：{ex.Message}\n\n系统任务栏和现有数据不会被修改。",
                "FocusPanel 更新失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    private void RefreshPinnedApps()
    {
        ReplaceCollection(PinnedApps, _appCatalog.GetPinned());
    }

    private void RefreshSearchResults()
    {
        ReplaceCollection(SearchResults, _appCatalog.Search(SearchQuery));
    }

    private void RefreshRunningApps()
    {
        ReplaceCollection(RunningApps, _windowTracker.GetSnapshot());
    }

    private void OnWindowSnapshotChanged(object? sender, EventArgs e) => RefreshRunningApps();
    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        RefreshPinnedApps();
        RefreshSearchResults();
    }

    private void RefreshStatus()
    {
        _updatingAudioState = true;
        MasterVolume = _systemStatus.MasterVolume;
        IsMuted = _systemStatus.IsMuted;
        _updatingAudioState = false;

        IsNetworkAvailable = _systemStatus.IsNetworkAvailable;
        NetworkDisplayName = _systemStatus.NetworkDisplayName;
        NetworkDetail = _systemStatus.NetworkDetail;
        InputLanguageDisplay = _systemStatus.InputLanguageDisplay;
        InputMethodDisplay = _systemStatus.InputMethodDisplay;
        HasBattery = _systemStatus.HasBattery;
        BatteryPercent = _systemStatus.BatteryPercent;
        IsCharging = _systemStatus.IsCharging;

        try
        {
            using var context = new AppDbContext();
            OpenTaskCount = context.Todos.Count(item => item.ParentId != null && !item.IsCompleted);
        }
        catch
        {
            OpenTaskCount = 0;
        }
    }

    private void CloseTransientPanels()
    {
        IsSearchOpen = false;
        IsCalendarOpen = false;
        IsQuickSettingsOpen = false;
        IsSettingsOpen = false;
        IsPowerMenuOpen = false;
    }

    private void SetSystemActionResult(bool succeeded, string error)
        => SystemActionMessage = succeeded ? string.Empty : error;

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
        _clockTimer.Stop();
        _windowTracker.SnapshotChanged -= OnWindowSnapshotChanged;
        _appCatalog.CatalogChanged -= OnCatalogChanged;
    }
}
