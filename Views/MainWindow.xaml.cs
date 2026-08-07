using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Microsoft.Win32;

namespace FocusPanel.Views;

public partial class MainWindow :
    Window,
    IFocusDialogInteractionHost
{
    private const double CompactWidth = 76;
    private const double ExpandedWidth = 720;
    private const double ScreenMargin = 12;
    private const double CompactTaskbarScrollStep = 46;
    private const double CompactTaskbarOverflowInset = 46;
    private const int TaskbarHoverOpenDelayMilliseconds = 420;
    private const int TaskbarHoverCloseDelayMilliseconds = 260;
    private const int TaskbarWindowCycleThrottleMilliseconds = 90;
    private const int TaskbarWindowCycleMemoryMilliseconds = 2000;
    private const int ShellEntryClickDelayMilliseconds = 80;
    private const int SwShowNoActivate = 4;
    private const int WmHotkey = 0x0312;
    private const int WmDpiChanged = 0x02E0;
    private const int SummonHotkeyId = 0x4650;

    private readonly ShellCoordinator _coordinator;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _taskbarHoverOpenTimer;
    private readonly DispatcherTimer
        _searchWindowHoverOpenTimer;
    private readonly DispatcherTimer _taskbarHoverCloseTimer;
    private readonly FocusToastManager _toastManager;
    private readonly
        UpdateInstallPreparationCoordinator
        _updateInstallPreparation;
    private readonly DesktopDragSession _desktopDragSession =
        new();
    private EdgeHotZoneMonitor? _hotZoneMonitor;
    private EdgeIndicatorWindow? _edgeIndicator;
    private HwndSource? _windowSource;
    private bool _summonHotkeyRegistered;
    private bool _isExit;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private bool _hiddenToTray;
    private bool _hiddenForFullscreen;
    private bool _isHotZoneAvailable;
    private bool _shellStartupReady;
    private bool _autoHideIgnoresInputFocus;
    private FrameworkElement? _overlayReturnFocusTarget;
    private int _transientInteractionDepth;
    private System.Windows.Point _pinnedDragStart;
    private long _lastTaskbarDragScrollTick = -1;
    private TaskbarAppItem? _taskbarDropCueItem;
    private TaskbarAppItem?
        _taskbarFileDropTarget;
    private bool
        _taskbarExternalFileDragActive;
    private Button? _taskbarHoverButton;
    private TaskbarAppItem? _taskbarHoverTask;
    private FrameworkElement?
        _searchWindowHoverTarget;
    private ShellSearchResult?
        _searchWindowHoverResult;
    private ContextMenu? _taskbarHoverMenu;
    private TaskbarWindowPreviewWindow?
        _taskbarWindowPreview;
    private bool _taskbarPreviewInteractionActive;
    private TaskbarSlotHotkeySession?
        _taskbarSlotHotkeySession;
    private CancellationTokenSource?
        _jumpListCancellation;
    private long _jumpListRevision;
    private string? _lastTaskbarWindowCycleIdentity;
    private IntPtr _lastTaskbarWindowCycleHandle;
    private long _lastTaskbarWindowCycleTick = -1;

    public MainWindow()
        : this(null)
    {
    }

    internal MainWindow(
        EdgeIndicatorWindow? startupIndicator)
    {
        _edgeIndicator =
            startupIndicator;
        _coordinator = new ShellCoordinator();
        _viewModel = new MainViewModel(
            _coordinator.Apps,
            _coordinator.Windows,
            _coordinator.SystemStatus,
            _coordinator.Updates,
            _coordinator.Brightness,
            _coordinator.ApplicationAudio,
            _coordinator.Radios,
            _coordinator.WifiNetworks);

        InitializeComponent();
        DataContext = _viewModel;
        MyNotifyIcon.Icon = SystemIcons.Application;
        _toastManager = new FocusToastManager(this);
        _updateInstallPreparation =
            new
                UpdateInstallPreparationCoordinator();

        _viewModel.RequestClose += ForceClose;
        _viewModel.RequestEnableReplacement += EnableTaskbarReplacement;
        _viewModel.RequestDisableReplacement += DisableTaskbarReplacement;
        _viewModel.RequestApplyUpdate += ApplyDownloadedUpdate;
        _viewModel.UpdateAvailable += ViewModel_UpdateAvailable;
        _viewModel.PomodoroCompleted +=
            ViewModel_PomodoroCompleted;
        _viewModel.TaskCaptured +=
            ViewModel_TaskCaptured;
        _viewModel.TaskCompleted +=
            ViewModel_TaskCompleted;
        _viewModel.DisplayTargetChanged +=
            ViewModel_DisplayTargetChanged;
        _viewModel.WorkspacePinChanged +=
            ViewModel_WorkspacePinChanged;
        _viewModel.PropertyChanged +=
            ViewModel_PropertyChanged;
        _viewModel.WorkspaceRequested += _ => ExpandSidebar();
        _coordinator.Taskbar.ReplacementStopped += Taskbar_ReplacementStopped;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            bool transientSurfaceActive =
                ShellTransientSurfacePolicy.IsActive(
                    _transientInteractionDepth > 0,
                    Mouse.Captured != null,
                    HasOpenComboBoxDropDown(this));
            ShellAutoHideAction autoHideAction =
                ShellAutoHidePolicy.Decide(
                _viewModel.KeepCompactDockVisible,
                WorkspaceHost.Visibility
                    == Visibility.Visible,
                _viewModel.IsWorkspacePinned,
                _desktopDragSession.IsActive,
                transientSurfaceActive,
                IsCursorInsideShell(),
                IsInputFocusActive(),
                _autoHideIgnoresInputFocus);
            if (autoHideAction
                == ShellAutoHideAction.None)
            {
                if (!_viewModel
                        .KeepCompactDockVisible
                    || WorkspaceHost.Visibility
                        == Visibility.Visible)
                {
                    _autoHideTimer.Start();
                }
                return;
            }

            if (autoHideAction
                == ShellAutoHideAction.CollapseToCompact)
            {
                CollapseSidebar();
                return;
            }

            HideShell();
        };
        _taskbarHoverOpenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                TaskbarHoverOpenDelayMilliseconds)
        };
        _taskbarHoverOpenTimer.Tick +=
            TaskbarHoverOpenTimer_Tick;
        _searchWindowHoverOpenTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        TaskbarHoverOpenDelayMilliseconds)
            };
        _searchWindowHoverOpenTimer.Tick +=
            SearchWindowHoverOpenTimer_Tick;
        _taskbarHoverCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                TaskbarHoverCloseDelayMilliseconds)
        };
        _taskbarHoverCloseTimer.Tick +=
            TaskbarHoverCloseTimer_Tick;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = CompactWidth;
        ShowInTaskbar = false;

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        Deactivated += (_, _) => ScheduleAutoHide(220, ignoreInputFocus: true);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        PositionAtTargetRightEdge();
        HideShell();
        await _viewModel
            .WaitForShellPreferencesAsync();
        if (_isExit)
            return;

        EnsureEdgeIndicator();
        EnsureHotZoneMonitor();
        _hotZoneMonitor?.Start();
        _shellStartupReady = true;

        if (_viewModel.IsOnboardingVisible)
        {
            ExpandSidebar();
            Activate();
        }
        else
        {
            if (_viewModel
                    .KeepCompactDockVisible)
            {
                OpenCompactDock();
            }
            else
            {
                HideShell();
            }
            if (_viewModel.IsReplacementEnabled)
            {
                _ = Dispatcher.BeginInvoke(
                    EnableTaskbarReplacement,
                    DispatcherPriority
                        .ApplicationIdle);
            }
        }

        _ = Dispatcher.BeginInvoke(
            new Action(() => _ = _viewModel.CheckForUpdatesInBackgroundAsync()),
            DispatcherPriority.ContextIdle);
    }

    private void ViewModel_UpdateAvailable(AppUpdateInfo update)
    {
        _toastManager.Enqueue(
            new FocusToastNotification(
                "update-available",
                "FocusPanel 更新可用",
                $"GitHub 已发布 v{update.Version}，可在设置中一键安装。",
                "\uE895",
                FocusToastKind.Information,
                "打开更新",
                OpenUpdateSettings));
    }

    private void ViewModel_PomodoroCompleted(
        int durationMinutes)
    {
        SystemSounds.Asterisk.Play();
        _toastManager.Enqueue(
            new FocusToastNotification(
                "pomodoro-completed",
                "专注完成",
                $"本轮 {durationMinutes} 分钟专注已完成，休息一下吧。",
                "\uE823",
                FocusToastKind.Success,
                "查看专注",
                OpenPomodoroWorkspace));
    }

    private void ViewModel_TaskCaptured(
        int taskId,
        string title)
    {
        _toastManager.Enqueue(
            new FocusToastNotification(
                $"task-captured:{taskId}",
                "已收集到 Inbox",
                title,
                "\uE73E",
                FocusToastKind.Success,
                "查看任务",
                OpenTasksWorkspace));
    }

    private void ViewModel_TaskCompleted(
        int taskId,
        string title)
    {
        _toastManager.Enqueue(
            new FocusToastNotification(
                $"task-completed:{taskId}",
                "任务已完成",
                title,
                "\uE73E",
                FocusToastKind.Success,
                "查看任务",
                OpenTasksWorkspace));
    }

    private void OpenUpdateSettings()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        CloseOverlayPanels();
        _viewModel.IsSettingsOpen = true;
        Activate();
        QueueOverlayFocus(
            SettingsNavigationButton,
            SettingsUpdateActionButton,
            () => _viewModel.IsSettingsOpen);
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_isExit
                    || !IsVisible
                    || !_viewModel.IsSettingsOpen)
                {
                    return;
                }

                SettingsUpdateCard.BringIntoView();
            }),
            DispatcherPriority.Loaded);
    }

    private void OpenPomodoroWorkspace()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        _viewModel.NavigateCommand.Execute("Pomodoro");
        Activate();
    }

    private void OpenTasksWorkspace()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        _viewModel.NavigateCommand.Execute(
            "Tasks");
        Activate();
    }

    internal void ShowDesktopRecoveryNotice(
        DesktopCrashRecoveryResult recovery)
    {
        if (recovery.Restored == 0
            && recovery.Failed == 0)
        {
            return;
        }

        _toastManager.Enqueue(
            new FocusToastNotification(
                "desktop-crash-recovery",
                recovery.Failed == 0
                    ? "桌面图标已自动恢复"
                    : "部分桌面图标等待恢复",
                recovery.Failed == 0
                    ? $"FocusPanel 已自动恢复 {recovery.Restored} 个图标，原有分区仍然保留。"
                    : $"已恢复 {recovery.Restored} 个图标；另有 {recovery.Failed} 个项目因权限或文件状态暂未恢复。",
                "\uE777",
                recovery.Failed == 0
                    ? FocusToastKind.Success
                    : FocusToastKind.Warning,
                recovery.Failed == 0
                    ? null
                    : "查看桌面收纳",
                recovery.Failed == 0
                    ? null
                    : OpenDesktopOrganizerWorkspace));
    }

    private void OpenDesktopOrganizerWorkspace()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        _viewModel.NavigateCommand.Execute("Files");
        Activate();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(hwnd);
        _windowSource?.AddHook(WindowMessageHook);
        ShellHotkeyRegistration registration =
            ShellSummonHotkeyPolicy
                .RegisterFirstAvailable(
                    (modifiers, virtualKey) =>
                        NativeMethods.RegisterHotKey(
                            hwnd,
                            SummonHotkeyId,
                            modifiers,
                            virtualKey));
        _summonHotkeyRegistered =
            registration.IsRegistered;
        _viewModel.SetSummonShortcutStatus(
            registration);
        ApplyDwmBackdrop();
    }

    private void ApplyDwmBackdrop()
    {
        WindowBackdropService.Apply(
            this,
            updateThemeState: true);
    }

    private void EnsureHotZoneMonitor()
    {
        if (_hotZoneMonitor != null)
            return;

        _hotZoneMonitor = new EdgeHotZoneMonitor(
            _coordinator.Windows,
            () => _viewModel.DisableHotZoneInFullscreen,
            GetTargetDisplayBounds,
            _viewModel
                .HotZoneDwellMilliseconds);
        _hotZoneMonitor.OpenRequested += (_, _) => OpenCompactDock();
        _hotZoneMonitor.AvailabilityChanged += isAvailable =>
        {
            _isHotZoneAvailable = isAvailable;
            PersistentCompactDockAvailabilityDecision
                decision =
                    ShellAutoHidePolicy
                        .DecideAvailabilityChange(
                            isAvailable,
                            _viewModel
                                .KeepCompactDockVisible,
                            _hiddenToTray,
                            _isExit,
                            IsVisible,
                            _hiddenForFullscreen);
            _hiddenForFullscreen =
                decision
                    .IsHiddenForUnavailableEdge;
            if (decision.Action
                == PersistentCompactDockAvailabilityAction
                    .HideForUnavailableEdge)
            {
                HideShell();
            }
            else if (decision.Action
                     == PersistentCompactDockAvailabilityAction
                         .RestoreAfterUnavailableEdge)
            {
                OpenCompactDock();
            }
            UpdateEdgeIndicatorVisibility();
        };
    }

    private void EnsureEdgeIndicator()
    {
        _edgeIndicator ??= new EdgeIndicatorWindow();
        _edgeIndicator.TargetValue =
            _viewModel.DisplayTargetMode;
    }

    private void PositionAtTargetRightEdge()
    {
        ApplyPanelPlacement(Width);
    }

    private void ApplyPanelPlacement(double widthDip)
    {
        Rectangle targetBounds =
            GetTargetDisplayBounds();
        if (targetBounds.Width <= 0
            || targetBounds.Height <= 0)
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        uint dpi =
            ShellWindowPlacement.GetTargetDpi(
                targetBounds,
                hwnd);
        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculatePanel(
                targetBounds,
                dpi,
                widthDip,
                ScreenMargin);
        Height = bounds.Height / (dpi / 96.0);
        ShellWindowPlacement.Apply(hwnd, bounds);
    }

    private void OpenCompactDock()
    {
        if (!_shellStartupReady
            || _hiddenToTray
            || IsVisible)
            return;

        _autoHideTimer.Stop();
        SetShellWidth(CompactWidth, false, false);
        ShowWithoutActivating();
        ScheduleAutoHide(900);
    }

    public void ExpandSidebar()
    {
        if (!_shellStartupReady)
            return;

        _hiddenToTray = false;
        _autoHideTimer.Stop();
        WorkspaceHost.Visibility = Visibility.Visible;
        ShowWithoutActivating();
        SetShellWidth(ExpandedWidth, true, true);
        ScheduleAutoHide(1200);
    }

    public void CollapseSidebar()
    {
        if (_desktopDragSession.IsActive)
            return;

        CancelTaskbarHoverPreview(
            closeMenu: true);
        _viewModel.IsWorkspacePinned = false;
        CloseOverlayPanels();
        SetShellWidth(CompactWidth, false, true);
    }

    private void SetShellWidth(double targetWidth, bool workspaceVisible, bool animate)
    {
        double currentWidth =
            ActualWidth > 0 ? ActualWidth : Width;
        bool reduceMotion =
            !SystemParameters.ClientAreaAnimation
            || SystemParameters.HighContrast;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(LeftProperty, null);
        Width = targetWidth;

        if (!animate
            || reduceMotion
            || Math.Abs(currentWidth - targetWidth) < 0.5)
        {
            WorkspaceHost.Visibility = workspaceVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ApplyPanelPlacement(targetWidth);
            return;
        }

        if (workspaceVisible)
            WorkspaceHost.Visibility = Visibility.Visible;

        var widthAnimation = new DoubleAnimation(
            currentWidth,
            targetWidth,
            new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        widthAnimation.CurrentTimeInvalidated +=
            (_, _) => ApplyPanelPlacement(Width);
        widthAnimation.Completed += (_, _) =>
        {
            BeginAnimation(WidthProperty, null);
            Width = targetWidth;
            ApplyPanelPlacement(targetWidth);
            if (!workspaceVisible)
            {
                WorkspaceHost.Visibility =
                    Visibility.Collapsed;
            }
        };
        BeginAnimation(
            WidthProperty,
            widthAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void HideShell()
    {
        CancelTaskbarHoverPreview(
            closeMenu: true);
        ClearTaskbarDropCue();
        EndTaskbarExternalFileDrag();
        _autoHideTimer.Stop();
        CloseOverlayPanels();
        _viewModel.SetShellVisible(false);
        Visibility = Visibility.Hidden;
        _viewModel.IsWorkspacePinned = false;
        UpdateEdgeIndicatorVisibility();
    }

    private void ViewModel_WorkspacePinChanged(
        bool isPinned)
    {
        if (isPinned)
        {
            _autoHideTimer.Stop();
            _autoHideIgnoresInputFocus = false;
            return;
        }

        if (IsVisible
            && WorkspaceHost.Visibility
                == Visibility.Visible)
        {
            ScheduleAutoHide();
        }
    }

    private void ShowWithoutActivating()
    {
        _edgeIndicator?.HideIndicator();
        if (!IsVisible)
            Show();
        Visibility = Visibility.Visible;
        _viewModel.SetShellVisible(true);
        Topmost = true;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ApplyDwmBackdrop();
            NativeMethods.ShowWindow(hwnd, SwShowNoActivate);
        }
    }

    private void ScheduleAutoHide(
        int? delayMilliseconds = null,
        bool ignoreInputFocus = false)
    {
        if (_desktopDragSession.IsActive)
            return;

        _autoHideTimer.Stop();
        _autoHideIgnoresInputFocus = ignoreInputFocus;
        int resolvedDelay =
            delayMilliseconds
            ?? _viewModel
                .AutoHideDelayMilliseconds;
        _autoHideTimer.Interval =
            TimeSpan.FromMilliseconds(
                resolvedDelay);
        _autoHideTimer.Start();
    }

    private void Shell_MouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        _autoHideIgnoresInputFocus = false;
    }

    private void Shell_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleAutoHide();
    }

    private void Shell_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ShellBorder.IsKeyboardFocusWithin)
            ScheduleAutoHide();
    }

    private bool IsCursorInsideShell()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        return hwnd != IntPtr.Zero
            && NativeMethods.GetCursorPos(out NativeMethods.Point cursor)
            && NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect rect)
            && cursor.X >= rect.Left
            && cursor.X < rect.Right
            && cursor.Y >= rect.Top
            && cursor.Y < rect.Bottom;
    }

    private static bool IsInputFocusActive()
        => ShellFocusRetentionPolicy.ShouldRetainShell(
            GetKeyboardFocusKind());

    private static ShellKeyboardFocusKind GetKeyboardFocusKind()
        => Keyboard.FocusedElement switch
        {
            TextBoxBase or PasswordBox =>
                ShellKeyboardFocusKind.TextInput,
            ComboBox or ComboBoxItem =>
                ShellKeyboardFocusKind.SelectionInput,
            null => ShellKeyboardFocusKind.None,
            _ => ShellKeyboardFocusKind.Command
        };

    private static bool HasOpenComboBoxDropDown(
        DependencyObject root)
    {
        if (root is ComboBox { IsDropDownOpen: true })
            return true;

        foreach (object child in
                 LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dependencyObject
                && HasOpenComboBoxDropDown(dependencyObject))
            {
                return true;
            }
        }

        return false;
    }

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
    {
        CollapseSidebar();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        ShellSearchEntryState entry =
            ShellSearchEntryPolicy
                .PrepareWindowOverview(
                    _viewModel.IsSearchOpen,
                    _viewModel.SearchScope,
                    _viewModel.SearchQuery);
        ApplySearchEntryState(entry);
        ToggleCompactOverlay(
            () => _viewModel.IsSearchOpen,
            () => _viewModel.ToggleSearchCommand
                .Execute(null),
            SearchButton,
            SearchBox,
            selectAllText: true);
    }

    private void ApplySearchEntryState(
        ShellSearchEntryState entry)
    {
        _viewModel.SearchScope = entry.Scope;
        _viewModel.SearchQuery = entry.Query;
    }

    private void SearchSuggestion_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: string suggestion
            }
            || string.IsNullOrWhiteSpace(
                suggestion))
        {
            return;
        }

        _viewModel.SearchQuery = suggestion;
        SearchBox.Focus();
        SearchBox.Select(
            suggestion.Length,
            0);
    }

    private async void StartButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;
        await InvokeShellEntryAfterClickAsync(
            () => _viewModel.OpenStartMenuCommand
                .Execute(null));
    }

    private void CloseCurrentVirtualDesktop_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBoxResult result = FocusDialogService.Show(
            "关闭当前虚拟桌面？\n\n其中的应用不会被关闭，Windows 会把窗口移动到相邻桌面。",
            "关闭虚拟桌面",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _viewModel.CloseCurrentVirtualDesktopCommand
            .Execute(null);
    }

    private async Task InvokeShellEntryAfterClickAsync(
        Action action)
    {
        await Dispatcher.Yield(
            DispatcherPriority.ApplicationIdle);
        await Task.Delay(
            ShellEntryClickDelayMilliseconds);
        if (!_isExit
            && IsVisible)
        {
            action();
        }
    }

    private void SearchBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
        {
            int selectedIndex = AppSearchSelectionPolicy.Move(
                SearchResultsList.Items.Count,
                SearchResultsList.SelectedIndex,
                e.Key == Key.Up ? -1 : 1);
            SearchResultsList.SelectedIndex = selectedIndex;
            if (selectedIndex >= 0)
                SearchResultsList.ScrollIntoView(
                    SearchResultsList.Items[selectedIndex]);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        int launchIndex = AppSearchSelectionPolicy.ResolveLaunchIndex(
            SearchResultsList.Items.Count,
            SearchResultsList.SelectedIndex);
        if (launchIndex < 0
            || SearchResultsList.Items[launchIndex] is not ShellSearchResult result
            || !_viewModel.ExecuteSearchResultCommand.CanExecute(result))
        {
            return;
        }

        _viewModel.ExecuteSearchResultCommand.Execute(result);
        e.Handled = true;
    }

    private void SearchResultsList_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        ShellSearchResult? focusedResult =
            (e.OriginalSource
                as FrameworkElement)
            ?.DataContext
                as ShellSearchResult
            ?? _viewModel
                .SelectedSearchResult;
        if (e.Key != Key.Delete
            || focusedResult?.Window
                is not WindowReference window
            || !_viewModel.CloseWindowCommand
                .CanExecute(window))
        {
            return;
        }

        _viewModel.CloseWindowCommand
            .Execute(window);
        e.Handled = true;
    }

    private void SearchResult_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender
                is not FrameworkElement
                {
                    DataContext:
                        ShellSearchResult
                        {
                            Window: not null
                        } result
                } target)
        {
            return;
        }

        bool ownsCurrentPreview =
            _taskbarWindowPreview?.IsVisible
                == true
            && ReferenceEquals(
                _searchWindowHoverTarget,
                target);
        if (!ownsCurrentPreview
            && (_taskbarWindowPreview?.IsVisible
                    == true
                || _taskbarHoverMenu?.IsOpen
                    == true
                || _taskbarHoverButton != null))
        {
            CancelTaskbarHoverPreview(
                closeMenu: true);
        }

        _taskbarHoverOpenTimer.Stop();
        _taskbarHoverButton = null;
        _taskbarHoverTask = null;
        _taskbarHoverCloseTimer.Stop();
        if (!TaskbarHoverPreviewPolicy.ShouldOpen(
                windowCount: 1,
                isPointerOver:
                    target.IsMouseOver,
                isMouseButtonPressed:
                    Mouse.LeftButton
                    == MouseButtonState.Pressed
                || Mouse.RightButton
                    == MouseButtonState.Pressed
                || Mouse.MiddleButton
                    == MouseButtonState.Pressed,
                hasOpenMenu:
                    _taskbarWindowPreview?.IsVisible
                    == true
                && ReferenceEquals(
                    _searchWindowHoverTarget,
                    target)))
        {
            return;
        }

        _searchWindowHoverTarget = target;
        _searchWindowHoverResult = result;
        _searchWindowHoverOpenTimer.Stop();
        _searchWindowHoverOpenTimer.Start();
    }

    private void SearchResult_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        _searchWindowHoverOpenTimer.Stop();
        ScheduleTaskbarHoverPreviewClose();
    }

    private void SearchWindowHoverOpenTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _searchWindowHoverOpenTimer.Stop();
        FrameworkElement? target =
            _searchWindowHoverTarget;
        ShellSearchResult? result =
            _searchWindowHoverResult;
        if (!_viewModel.IsSearchOpen
            || target == null
            || result?.Window == null
            || !ReferenceEquals(
                target.DataContext,
                result)
            || !TaskbarHoverPreviewPolicy.ShouldOpen(
                windowCount: 1,
                isPointerOver:
                    target.IsMouseOver,
                isMouseButtonPressed:
                    Mouse.LeftButton
                    == MouseButtonState.Pressed
                || Mouse.RightButton
                    == MouseButtonState.Pressed
                || Mouse.MiddleButton
                    == MouseButtonState.Pressed,
                hasOpenMenu: false))
        {
            return;
        }

        TryOpenSearchWindowPreview(
            target,
            result);
    }

    private void SearchWindow_ContextMenuOpening(
        object sender,
        ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (sender
                is not FrameworkElement
                {
                    DataContext:
                        ShellSearchResult
                        {
                            Window: not null
                        } result
                } target)
        {
            return;
        }

        CancelTaskbarHoverPreview(
            closeMenu: true);
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                PopulateSearchWindowContextMenu(
                    target,
                    result);
                OpenContextMenu(target);
            }),
            DispatcherPriority.Input);
    }

    private void PopulateSearchWindowContextMenu(
        FrameworkElement target,
        ShellSearchResult result)
    {
        if (result.Window
            is not WindowReference window)
        {
            return;
        }

        string applicationName =
            string.IsNullOrWhiteSpace(
                result.WindowApplicationName)
                ? result.DisplayName
                : result.WindowApplicationName;
        ContextMenu menu =
            CreateTaskbarContextMenu();
        menu.Items.Add(
            new MenuItem
            {
                Header = CreateWindowTitle(
                    window,
                    applicationName),
                IsEnabled = false
            });
        menu.Items.Add(
            new Separator());
        menu.Items.Add(
            new MenuItem
            {
                Header = "切换到此窗口",
                InputGestureText = "Enter",
                Command =
                    _viewModel
                        .ExecuteSearchResultCommand,
                CommandParameter = result
            });
        AddWindowStateMenuItems(
            menu,
            window);
        menu.Items.Add(
            new Separator());
        menu.Items.Add(
            new MenuItem
            {
                Header = "关闭窗口",
                InputGestureText = "Delete",
                Command =
                    _viewModel
                        .CloseWindowCommand,
                CommandParameter = window
            });
        AutomationProperties.SetName(
            menu,
            "窗口操作 "
            + GetWindowAccessibleName(
                window,
                applicationName));
        target.ContextMenu = menu;
    }

    private void OrganizerButton_Click(
        object sender,
        RoutedEventArgs e) =>
        OpenFocusWorkspace("Files");

    private void TasksButton_Click(
        object sender,
        RoutedEventArgs e) =>
        OpenFocusWorkspace("Tasks");

    private void TaskQuickCaptureMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        ExpandSidebar();
        CloseOverlayPanels();
        _viewModel.SearchScope =
            ShellSearchScope.All;
        _viewModel.SearchQuery =
            TaskCaptureCommandParser
                .QuickCapturePrefix;
        _viewModel.IsSearchOpen = true;
        _overlayReturnFocusTarget =
            TasksButton;
        _ = Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_isExit
                    || !IsVisible
                    || !_viewModel.IsSearchOpen)
                {
                    return;
                }

                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length;
            }),
            DispatcherPriority.Input);
    }

    private void OpenFocusWorkspace(
        string destination)
    {
        ExpandSidebar();
        _viewModel.NavigateCommand.Execute(
            destination);
    }

    private void StatusCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleCompactOverlay(
            () => _viewModel.IsStatusCenterOpen
                || _viewModel.IsPowerMenuOpen,
            () => _viewModel.ToggleStatusCenterCommand
                .Execute(null),
            StatusCenterButton,
            StatusCenterQuickSettingsButton,
            isOpenAfterToggle:
                () => _viewModel.IsStatusCenterOpen);
    }

    private void StatusCenterButton_MouseEnter(
        object sender,
        MouseEventArgs e) =>
        _viewModel.RefreshSystemStatusForInteraction();

    private void CalendarPanelButton_Click(object sender, RoutedEventArgs e)
    {
        TimeEntryAction action =
            TimeEntryPolicy.FromLeftClick(
                Keyboard.Modifiers.HasFlag(
                    ModifierKeys.Shift));
        if (action
            == TimeEntryAction.ShowDesktop)
        {
            ShowDesktopFromCompactEntry();
            return;
        }

        ToggleCompactOverlay(
            () => _viewModel.IsCalendarOpen,
            () => _viewModel.ToggleCalendarCommand
                .Execute(null),
            TimeButton);
    }

    private void TimeButton_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton
            != MouseButton.Middle)
        {
            return;
        }

        ShowDesktopFromCompactEntry();
        e.Handled = true;
    }

    private void ShowDesktopFromCompactEntry()
    {
        CloseOverlayPanels();
        _viewModel.ShowDesktopCommand.Execute(null);
        ScheduleAutoHide(900);
    }

    private void ToggleCompactOverlay(
        Func<bool> hasOwnedSurfaceOpen,
        Action openSurface,
        FrameworkElement returnTarget,
        FrameworkElement? initialTarget = null,
        bool selectAllText = false,
        Func<bool>? isOpenAfterToggle = null)
    {
        CompactOverlayToggleAction action =
            CompactOverlayTogglePolicy.Decide(
                hasOwnedSurfaceOpen());
        if (action
            == CompactOverlayToggleAction.CloseSurface)
        {
            CloseOverlayPanels();
            ScheduleAutoHide(1200);
            return;
        }

        ExpandSidebar();
        openSurface();
        Func<bool> isOpen =
            isOpenAfterToggle
            ?? hasOwnedSurfaceOpen;
        if (initialTarget == null)
        {
            _overlayReturnFocusTarget =
                isOpen() ? returnTarget : null;
            return;
        }

        QueueOverlayFocus(
            returnTarget,
            initialTarget,
            isOpen);
        if (selectAllText
            && initialTarget is TextBox textBox)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (isOpen()
                        && textBox.IsKeyboardFocusWithin)
                    {
                        textBox.SelectAll();
                    }
                }),
                DispatcherPriority.Input);
        }
    }

    private void QueueOverlayFocus(
        FrameworkElement returnTarget,
        FrameworkElement initialTarget,
        Func<bool> isOpen)
    {
        if (!isOpen())
        {
            _overlayReturnFocusTarget = null;
            return;
        }

        _overlayReturnFocusTarget =
            returnTarget;
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_isExit
                    || !IsVisible
                    || !isOpen()
                    || !initialTarget.IsVisible
                    || !initialTarget.IsEnabled)
                {
                    return;
                }

                initialTarget.Focus();
            }),
            DispatcherPriority.Input);
    }

    private void TaskbarAppsScrollViewer_ScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        UpdateTaskbarOverflowControls();
    }

    private void TaskbarAppsHost_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        int windowCount = 0;
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(
                    TaskbarAppsItemsControl,
                    source)
                is FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem item
                })
        {
            windowCount = item.WindowCount;
        }

        TaskbarWheelAction action =
            TaskbarWheelPolicy.GetAction(
                e.Delta,
                Keyboard.Modifiers.HasFlag(
                    ModifierKeys.Control),
                windowCount);
        if (action != TaskbarWheelAction.ScrollApps)
            return;

        ScrollTaskbarApps(e.Delta);
        e.Handled = true;
    }

    private void TaskbarScrollUpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        TaskbarAppsScrollViewer.ScrollToVerticalOffset(
            Math.Max(
                0,
                TaskbarAppsScrollViewer.VerticalOffset
                    - CompactTaskbarScrollStep));
    }

    private void TaskbarScrollDownButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        TaskbarAppsScrollViewer.ScrollToVerticalOffset(
            Math.Min(
                TaskbarAppsScrollViewer.ScrollableHeight,
                TaskbarAppsScrollViewer.VerticalOffset
                    + CompactTaskbarScrollStep));
    }

    private void UpdateTaskbarOverflowControls()
    {
        CompactTaskbarScrollState state =
            CompactTaskbarScrollPolicy.GetState(
                TaskbarAppsScrollViewer.VerticalOffset,
                TaskbarAppsScrollViewer.ScrollableHeight);
        TaskbarScrollUpButton.Visibility = state.CanScrollUp
            ? Visibility.Visible
            : Visibility.Collapsed;
        TaskbarScrollDownButton.Visibility = state.CanScrollDown
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName
            == nameof(
                MainViewModel
                    .HotZoneDwellMilliseconds))
        {
            _hotZoneMonitor?
                .SetDwellMilliseconds(
                    _viewModel
                        .HotZoneDwellMilliseconds);
            return;
        }

        if (e.PropertyName
            == nameof(
                MainViewModel
                    .KeepCompactDockVisible))
        {
            ApplyCompactDockVisibilityPreference();
            return;
        }

        if (e.PropertyName
            == nameof(
                MainViewModel
                    .EnableTaskbarSlotHotkeys))
        {
            if (_viewModel
                    .EnableTaskbarSlotHotkeys
                && _coordinator.Taskbar
                    .IsReplacementEnabled)
            {
                RegisterTaskbarSlotHotkeys();
            }
            else
            {
                UnregisterTaskbarSlotHotkeys();
            }

            return;
        }

        if (e.PropertyName
            != nameof(
                MainViewModel
                    .ActiveTaskbarIdentity))
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            RevealActiveTaskbarApp,
            DispatcherPriority.Render);
    }

    private void ApplyCompactDockVisibilityPreference()
    {
        if (!_shellStartupReady
            || _hiddenToTray)
        {
            return;
        }

        if (_viewModel.KeepCompactDockVisible)
        {
            if (_isHotZoneAvailable
                && !IsVisible)
            {
                OpenCompactDock();
            }
            return;
        }

        _hiddenForFullscreen = false;
        if (IsVisible
            && WorkspaceHost.Visibility
                != Visibility.Visible
            && !IsCursorInsideShell())
        {
            ScheduleAutoHide();
        }
    }

    private void RevealActiveTaskbarApp()
    {
        if (_isExit
            || string.IsNullOrWhiteSpace(
                _viewModel
                    .ActiveTaskbarIdentity))
        {
            return;
        }

        int activeIndex = -1;
        for (int index = 0;
             index < _viewModel
                 .TaskbarApps.Count;
             index++)
        {
            if (string.Equals(
                    _viewModel
                        .TaskbarApps[index]
                        .IdentityKey,
                    _viewModel
                        .ActiveTaskbarIdentity,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                activeIndex = index;
                break;
            }
        }
        if (activeIndex < 0
            || TaskbarAppsItemsControl
                .ItemContainerGenerator
                .ContainerFromIndex(
                    activeIndex)
                is not FrameworkElement
                    container)
        {
            return;
        }

        try
        {
            double itemTop = container
                .TransformToAncestor(
                    TaskbarAppsScrollViewer)
                .Transform(
                    new System.Windows.Point())
                .Y;
            CompactTaskbarScrollState state =
                CompactTaskbarScrollPolicy
                    .GetState(
                        TaskbarAppsScrollViewer
                            .VerticalOffset,
                        TaskbarAppsScrollViewer
                            .ScrollableHeight);
            double inset =
                state.ShowsOverflowControls
                    ? CompactTaskbarOverflowInset
                    : 0;
            double targetOffset =
                CompactTaskbarScrollPolicy
                    .GetRevealOffset(
                        TaskbarAppsScrollViewer
                            .VerticalOffset,
                        itemTop,
                        container.ActualHeight,
                        TaskbarAppsScrollViewer
                            .ViewportHeight,
                        TaskbarAppsScrollViewer
                            .ScrollableHeight,
                        inset,
                        inset);
            if (Math.Abs(
                    targetOffset
                    - TaskbarAppsScrollViewer
                        .VerticalOffset)
                > 0.5)
            {
                TaskbarAppsScrollViewer
                    .ScrollToVerticalOffset(
                        targetOffset);
            }
        }
        catch (InvalidOperationException)
        {
            // The active item changed again while WPF was
            // regenerating the compact taskbar containers.
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ExpandSidebar();
    private void PowerButton_Click(object sender, RoutedEventArgs e) => ExpandSidebar();
    private void SystemButton_Click(object sender, RoutedEventArgs e) => ExpandSidebar();
    private void NotificationsButton_Click(object sender, RoutedEventArgs e) => ScheduleAutoHide();

    private void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu == null)
            return;

        _autoHideTimer.Stop();
        button.ContextMenu.Closed -= ButtonContextMenu_Closed;
        button.ContextMenu.Closed += ButtonContextMenu_Closed;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void ButtonContextMenu_Closed(object sender, RoutedEventArgs e)
        => ScheduleAutoHide();

    private void TransientContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
            FocusMenuTheme.Apply(menu);

        BeginTransientInteraction();
    }

    private void TransientContextMenu_Closed(object sender, RoutedEventArgs e)
        => EndTransientInteraction();

    public void BeginTransientInteraction()
    {
        _transientInteractionDepth++;
        _autoHideTimer.Stop();
        _autoHideIgnoresInputFocus = false;
    }

    public void EndTransientInteraction()
    {
        if (_transientInteractionDepth > 0)
            _transientInteractionDepth--;

        if (_transientInteractionDepth == 0)
            ScheduleAutoHide();
    }

    private void TaskbarApp_Click(object sender, RoutedEventArgs e)
    {
        CancelTaskbarHoverPreview();
        if (sender is not Button { DataContext: TaskbarAppItem task } button)
            return;

        TaskbarAppClickAction action = TaskbarAppClickPolicy.FromLeftClick(
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            task.CanLaunchNewInstance,
            task.WindowCount);
        if (action == TaskbarAppClickAction.LaunchElevated)
        {
            _viewModel.LaunchElevatedTaskbarAppCommand.Execute(task);
            return;
        }
        if (action == TaskbarAppClickAction.LaunchNewInstance)
        {
            _viewModel.LaunchNewTaskbarAppCommand.Execute(task);
            return;
        }
        if (action == TaskbarAppClickAction.CycleWindows)
        {
            CycleTaskbarWindows(
                task,
                wheelDelta: -1,
                applyThrottle: false);
            return;
        }

        if (task.WindowCount <= 1)
        {
            _viewModel.ActivateTaskbarAppCommand.Execute(task);
            return;
        }

        PopulateTaskbarWindowList(button, task);
        OpenContextMenu(button);
    }

    private void TaskbarApp_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle
            || sender is not Button { DataContext: TaskbarAppItem task })
        {
            return;
        }

        if (TaskbarAppClickPolicy.FromMiddleClick(
                task.CanLaunchNewInstance)
            == TaskbarAppClickAction.LaunchNewInstance)
        {
            _viewModel.LaunchNewTaskbarAppCommand.Execute(task);
        }

        e.Handled = true;
    }

    private void TaskbarApp_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        int windowCount = sender
                is Button
                {
                    DataContext:
                        TaskbarAppItem item
                }
            ? item.WindowCount
            : 0;
        TaskbarWheelAction wheelAction =
            TaskbarWheelPolicy.GetAction(
                e.Delta,
                Keyboard.Modifiers.HasFlag(
                    ModifierKeys.Control),
                windowCount);
        if (wheelAction
            == TaskbarWheelAction.ScrollApps)
        {
            ScrollTaskbarApps(e.Delta);
            e.Handled = true;
            return;
        }

        if (sender
                is not Button
                {
                    DataContext:
                        TaskbarAppItem task
                }
            || wheelAction
                != TaskbarWheelAction.CycleWindows)
        {
            return;
        }

        if (CycleTaskbarWindows(
                task,
                e.Delta,
                applyThrottle: true))
        {
            e.Handled = true;
        }
    }

    private bool CycleTaskbarWindows(
        TaskbarAppItem task,
        int wheelDelta,
        bool applyThrottle)
    {
        long now = Environment.TickCount64;
        bool sameCycleSession =
            string.Equals(
                _lastTaskbarWindowCycleIdentity,
                task.IdentityKey,
                StringComparison.OrdinalIgnoreCase)
            && _lastTaskbarWindowCycleTick
                >= 0
            && now
                - _lastTaskbarWindowCycleTick
                <= TaskbarWindowCycleMemoryMilliseconds;
        if (applyThrottle
            && sameCycleSession
            && now
                - _lastTaskbarWindowCycleTick
                < TaskbarWindowCycleThrottleMilliseconds)
        {
            return true;
        }

        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                task.Windows,
                wheelDelta,
                sameCycleSession
                    ? _lastTaskbarWindowCycleHandle
                    : IntPtr.Zero);
        if (target == null)
            return false;

        _lastTaskbarWindowCycleIdentity =
            task.IdentityKey;
        _lastTaskbarWindowCycleHandle =
            target.Handle;
        _lastTaskbarWindowCycleTick =
            now;
        _viewModel.ActivateWindowCommand.Execute(
            target);
        return true;
    }

    private void ScrollTaskbarApps(int delta)
    {
        double direction = delta > 0 ? -1 : 1;
        TaskbarAppsScrollViewer.ScrollToVerticalOffset(
            Math.Clamp(
                TaskbarAppsScrollViewer.VerticalOffset
                    + direction * CompactTaskbarScrollStep,
                0,
                TaskbarAppsScrollViewer.ScrollableHeight));
    }

    private void TaskbarApp_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(
                ModifierKeys.Alt)
            || e.Key
                is not (
                    Key.Up
                    or Key.Down)
            || sender
                is not FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem task
                }
            || !task.IsPinned)
        {
            return;
        }

        ICommand command =
            e.Key == Key.Up
                ? _viewModel
                    .MoveTaskbarAppUpCommand
                : _viewModel
                    .MoveTaskbarAppDownCommand;
        if (command.CanExecute(task))
            command.Execute(task);
        e.Handled = true;
    }

    private void TaskbarApp_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        CancelTaskbarHoverPreview(closeMenu: true);
        e.Handled = true;
        if (sender is not Button { DataContext: TaskbarAppItem task } button)
            return;

        // ContextMenuOpening is raised after WPF has started resolving the
        // popup's system theme. Recreate it first, apply our theme while it is
        // detached, then open it on the next input pass.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                PopulateTaskbarAppContextMenu(button, task);
                OpenContextMenu(button);
            }),
            DispatcherPriority.Input);
    }

    private void PopulateTaskbarAppContextMenu(Button button, TaskbarAppItem task)
    {
        ContextMenu menu = CreateTaskbarContextMenu();

        if (TryAddJumpListSection(
                menu,
                task))
        {
            menu.Items.Add(
                new Separator());
        }

        if (task.CreateLaunchItem() != null)
        {
            menu.Items.Add(new MenuItem
            {
                Header = task.IsRunning ? "启动新实例" : "启动",
                Command = _viewModel.LaunchNewTaskbarAppCommand,
                CommandParameter = task
            });
        }
        if (task.CanLaunchElevated)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "以管理员身份运行",
                InputGestureText =
                    "Ctrl+Shift+点击",
                Command =
                    _viewModel
                        .LaunchElevatedTaskbarAppCommand,
                CommandParameter = task
            });
        }
        menu.Items.Add(new MenuItem
        {
            Header = task.IsPinned ? "取消固定" : "固定到任务栏",
            IsEnabled = task.IsPinned || task.CanPin,
            Command = _viewModel.ToggleTaskbarPinCommand,
            CommandParameter = task
        });
        if (task.IsPinned)
        {
            menu.Items.Add(
                new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = "上移固定应用",
                InputGestureText = "Alt+↑",
                Command =
                    _viewModel
                        .MoveTaskbarAppUpCommand,
                CommandParameter = task
            });
            menu.Items.Add(new MenuItem
            {
                Header = "下移固定应用",
                InputGestureText = "Alt+↓",
                Command =
                    _viewModel
                        .MoveTaskbarAppDownCommand,
                CommandParameter = task
            });
        }
        if (task.Windows.Count > 0)
            menu.Items.Add(new Separator());

        foreach (WindowReference window in task.Windows)
        {
            var windowMenu = new MenuItem
            {
                Header = CreateWindowTitle(
                    window,
                    task.DisplayName),
                IsCheckable = true,
                IsChecked = window.IsActive
            };
            AutomationProperties.SetName(
                windowMenu,
                GetWindowAccessibleName(
                    window,
                    task.DisplayName));
            windowMenu.Items.Add(new MenuItem
            {
                Header = "切换到此窗口",
                Command = _viewModel.ActivateWindowCommand,
                CommandParameter = window
            });
            windowMenu.Items.Add(
                new Separator());
            AddWindowStateMenuItems(
                windowMenu,
                window);
            windowMenu.Items.Add(
                new Separator());
            windowMenu.Items.Add(new MenuItem
            {
                Header = "关闭窗口",
                Command = _viewModel.CloseWindowCommand,
                CommandParameter = window
            });
            menu.Items.Add(windowMenu);
        }

        if (task.Windows.Count > 0)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = task.WindowCount > 1 ? "关闭所有窗口" : "关闭窗口",
                Command = _viewModel.CloseTaskCommand,
                CommandParameter = task
            });
        }

        FocusMenuTheme.Apply(menu);
        button.ContextMenu = menu;
    }

    private void AddWindowStateMenuItems(
        ItemsControl windowMenu,
        WindowReference window)
    {
        foreach (WindowStateAction action
                 in WindowStateActionPolicy
                     .GetActions(
                         window.State))
        {
            MenuItem item =
                action switch
                {
                    WindowStateAction.Restore =>
                        new MenuItem
                        {
                            Header = "还原窗口",
                            Command =
                                _viewModel
                                    .RestoreWindowCommand,
                            CommandParameter =
                                window
                        },
                    WindowStateAction.Minimize =>
                        new MenuItem
                        {
                            Header = "最小化窗口",
                            Command =
                                _viewModel
                                    .MinimizeWindowCommand,
                            CommandParameter =
                                window
                        },
                    WindowStateAction.Maximize =>
                        new MenuItem
                        {
                            Header = "最大化窗口",
                            Command =
                                _viewModel
                                    .MaximizeWindowCommand,
                            CommandParameter =
                                window
                        },
                    _ => throw new
                        ArgumentOutOfRangeException(
                            nameof(action))
                };
            windowMenu.Items.Add(
                item);
        }

        windowMenu.Items.Add(
            new MenuItem
            {
                Header = window.IsTopmost
                    ? "取消置顶窗口"
                    : "置顶窗口",
                IsCheckable = true,
                IsChecked = window.IsTopmost,
                Command =
                    _viewModel
                        .ToggleWindowTopmostCommand,
                CommandParameter = window
            });

        Rectangle targetWorkArea =
            ShellDisplayTarget.GetWorkingArea(
                _viewModel.DisplayTargetMode);
        if (_coordinator.Windows
                .CanMoveToDisplay(
                    window.Handle,
                    targetWorkArea))
        {
            windowMenu.Items.Add(
                new MenuItem
                {
                    Header = "移到 Panel 所在屏幕",
                    Command =
                        _viewModel
                            .MoveWindowToPanelDisplayCommand,
                    CommandParameter = window
                });
        }
    }

    private bool TryAddJumpListSection(
        ContextMenu menu,
        TaskbarAppItem task)
    {
        string? applicationUserModelId =
            task.JumpListApplicationUserModelId;
        if (string.IsNullOrWhiteSpace(
                applicationUserModelId))
        {
            return false;
        }

        CancelJumpListLoad();
        var destinationsHeader =
            new MenuItem
            {
                Header = "最近与常用项目",
                IsEnabled = false
            };
        var loadingItem =
            new MenuItem
            {
                Header = "正在读取…",
                IsEnabled = false
            };
        menu.Items.Add(
            destinationsHeader);
        menu.Items.Add(
            loadingItem);

        var cancellation =
            new CancellationTokenSource();
        _jumpListCancellation =
            cancellation;
        long revision =
            Interlocked.Increment(
                ref _jumpListRevision);
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(
                    _jumpListCancellation,
                    cancellation))
            {
                CancelJumpListLoad();
            }
        };
        _ = LoadJumpListSectionAsync(
            menu,
            destinationsHeader,
            loadingItem,
            applicationUserModelId,
            CreateJumpListLaunchSnapshot(
                task),
            revision,
            cancellation);
        return true;
    }

    private async Task
        LoadJumpListSectionAsync(
            ContextMenu menu,
            MenuItem destinationsHeader,
            MenuItem loadingItem,
            string applicationUserModelId,
            AppJumpListApplicationLaunch?
                application,
            long revision,
            CancellationTokenSource
                cancellation)
    {
        IReadOnlyList<AppJumpListGroup>
            groups;
        try
        {
            groups =
                await _coordinator
                    .JumpLists
                    .GetDestinationsAsync(
                        applicationUserModelId,
                        AppJumpListPolicy
                            .MaximumItemCount,
                        cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isExit
            || cancellation
                .IsCancellationRequested
            || revision
                != Volatile.Read(
                    ref _jumpListRevision)
            || !ReferenceEquals(
                _jumpListCancellation,
                cancellation))
        {
            return;
        }

        menu.Items.Remove(
            loadingItem);
        if (groups.Count == 0)
        {
            destinationsHeader.Header =
                "最近与常用项目 · 无可用记录";
            return;
        }

        int insertionIndex =
            menu.Items.IndexOf(
                destinationsHeader);
        bool firstGroup = true;
        foreach (AppJumpListGroup group
                 in groups)
        {
            string categoryName =
                GetJumpListCategoryName(
                    group.Category);
            if (firstGroup)
            {
                destinationsHeader.Header =
                    categoryName + "项目";
                insertionIndex++;
                firstGroup = false;
            }
            else
            {
                menu.Items.Insert(
                    insertionIndex++,
                    new Separator());
                menu.Items.Insert(
                    insertionIndex++,
                    new MenuItem
                    {
                        Header =
                            categoryName
                            + "项目",
                        IsEnabled = false
                    });
            }

            foreach (AppJumpListItem item
                     in group.Items)
            {
                var destinationItem =
                    new MenuItem
                    {
                        Header =
                            item.DisplayName,
                        ToolTip =
                            item.LaunchTarget,
                        Tag =
                            new
                                TaskbarJumpListMenuAction(
                                    item,
                                    application,
                                    group.Category)
                    };
                AutomationProperties.SetName(
                    destinationItem,
                    $"打开{categoryName}项目 {item.DisplayName}");
                destinationItem.Click +=
                    JumpListItem_Click;
                menu.Items.Insert(
                    insertionIndex++,
                    destinationItem);
            }
        }

        FocusMenuTheme.Apply(menu);
    }

    private async void JumpListItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender
                is not FrameworkElement
                {
                    Tag:
                        TaskbarJumpListMenuAction
                            action
                })
        {
            return;
        }

        bool opened =
            await _coordinator
                .JumpLists
                .OpenAsync(
                    action.Item,
                    action.Application);
        if (opened || _isExit)
            return;

        _toastManager.Enqueue(
            new FocusToastNotification(
                "jump-list-open-failed",
                "无法打开"
                + GetJumpListCategoryName(
                    action.Category)
                + "项目",
                $"“{action.Item.DisplayName}”可能已移动、删除，"
                + "或原应用已不再可用。",
                "\uE783",
                FocusToastKind.Warning));
    }

    private static string
        GetJumpListCategoryName(
            AppJumpListCategory category) =>
        category
            == AppJumpListCategory.Frequent
            ? "常用"
            : "最近";

    private void CancelJumpListLoad()
    {
        CancellationTokenSource?
            cancellation =
                _jumpListCancellation;
        _jumpListCancellation = null;
        if (cancellation == null)
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private static
        AppJumpListApplicationLaunch?
        CreateJumpListLaunchSnapshot(
            TaskbarAppItem task)
    {
        AppLaunchItem? launch =
            task.CreateLaunchItem();
        return launch == null
            ? null
            : new
                AppJumpListApplicationLaunch(
                    launch.LaunchKind,
                    launch.LaunchTarget,
                    launch.Arguments);
    }

    private void PopulateTaskbarWindowList(
        Button button,
        TaskbarAppItem task)
    {
        ContextMenu menu = CreateTaskbarContextMenu();

        foreach (WindowReference window in task.Windows)
        {
            var windowItem = new MenuItem
            {
                Header = CreateWindowTitle(
                    window,
                    task.DisplayName),
                IsCheckable = true,
                IsChecked = window.IsActive,
                Command =
                    _viewModel.ActivateWindowCommand,
                CommandParameter = window,
                InputGestureText =
                    "中键 / Del 关闭"
            };
            AutomationProperties.SetName(
                windowItem,
                GetWindowQuickActionAccessibleName(
                    window,
                    task.DisplayName));
            windowItem.Tag =
                new TaskbarWindowMenuAction(
                    menu,
                    window);
            windowItem.PreviewMouseDown +=
                TaskbarWindowItem_PreviewMouseDown;
            windowItem.PreviewKeyDown +=
                TaskbarWindowItem_PreviewKeyDown;
            menu.Items.Add(windowItem);
        }

        FocusMenuTheme.Apply(menu);
        button.ContextMenu = menu;
    }

    private void TaskbarWindowItem_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton
                != MouseButton.Middle
            || sender
                is not MenuItem
                {
                    Tag:
                        TaskbarWindowMenuAction
                        action
                })
        {
            return;
        }

        CloseTaskbarWindowFromPreview(
            action);
        e.Handled = true;
    }

    private void TaskbarWindowItem_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Delete
            || sender
                is not MenuItem
                {
                    Tag:
                        TaskbarWindowMenuAction
                        action
                })
        {
            return;
        }

        CloseTaskbarWindowFromPreview(
            action);
        e.Handled = true;
    }

    private void CloseTaskbarWindowFromPreview(
        TaskbarWindowMenuAction action)
    {
        if (_viewModel.CloseWindowCommand
                .CanExecute(action.Window))
        {
            _viewModel.CloseWindowCommand
                .Execute(action.Window);
        }

        action.Menu.IsOpen = false;
    }

    private void TaskbarApp_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender
                is not Button
                {
                    DataContext:
                        TaskbarAppItem task
                } button)
        {
            return;
        }

        _searchWindowHoverOpenTimer.Stop();
        _searchWindowHoverTarget = null;
        _searchWindowHoverResult = null;

        if (_taskbarWindowPreview?.IsVisible
                == true
            && !ReferenceEquals(
                _taskbarHoverButton,
                button))
        {
            CancelTaskbarHoverPreview(
                closeMenu: true);
        }

        _taskbarHoverCloseTimer.Stop();
        if (!TaskbarHoverPreviewPolicy.ShouldOpen(
                task.WindowCount,
                button.IsMouseOver,
                Mouse.LeftButton
                    == MouseButtonState.Pressed
                || Mouse.RightButton
                    == MouseButtonState.Pressed
                || Mouse.MiddleButton
                    == MouseButtonState.Pressed,
                button.ContextMenu?.IsOpen == true
                || (_taskbarWindowPreview?.IsVisible
                        == true
                    && ReferenceEquals(
                        _taskbarHoverButton,
                        button))))
        {
            return;
        }

        _taskbarHoverButton = button;
        _taskbarHoverTask = task;
        _taskbarHoverOpenTimer.Stop();
        _taskbarHoverOpenTimer.Start();
    }

    private void TaskbarApp_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        _taskbarHoverOpenTimer.Stop();
        ScheduleTaskbarHoverPreviewClose();
    }

    private void TaskbarHoverOpenTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _taskbarHoverOpenTimer.Stop();
        Button? button = _taskbarHoverButton;
        TaskbarAppItem? task =
            _taskbarHoverTask;
        if (button == null
            || task == null
            || !ReferenceEquals(
                button.DataContext,
                task)
            || !TaskbarHoverPreviewPolicy.ShouldOpen(
                task.WindowCount,
                button.IsMouseOver,
                Mouse.LeftButton
                    == MouseButtonState.Pressed
                || Mouse.RightButton
                    == MouseButtonState.Pressed
                || Mouse.MiddleButton
                    == MouseButtonState.Pressed,
                button.ContextMenu?.IsOpen
                    == true))
        {
            return;
        }

        if (TryOpenTaskbarWindowPreview(
                button,
                task))
        {
            return;
        }

        PopulateTaskbarWindowList(
            button,
            task);
        ContextMenu? menu =
            button.ContextMenu;
        if (menu == null)
            return;

        _taskbarHoverMenu = menu;
        menu.MouseEnter +=
            TaskbarHoverMenu_MouseEnter;
        menu.MouseLeave +=
            TaskbarHoverMenu_MouseLeave;
        menu.Closed +=
            TaskbarHoverMenu_Closed;
        OpenContextMenu(button);
    }

    private bool TryOpenTaskbarWindowPreview(
        Button button,
        TaskbarAppItem task) =>
        TryOpenWindowPreview(
            button,
            preview =>
                preview.Configure(task));

    private bool TryOpenSearchWindowPreview(
        FrameworkElement target,
        ShellSearchResult result)
    {
        if (result.Window
            is not WindowReference window)
        {
            return false;
        }

        string applicationName =
            string.IsNullOrWhiteSpace(
                result.WindowApplicationName)
                ? result.DisplayName
                : result.WindowApplicationName;
        return TryOpenWindowPreview(
            target,
            preview =>
                preview.Configure(
                    applicationName,
                    new[] { window },
                    "点击画面直接切换；右侧按钮正常关闭。"));
    }

    private bool TryOpenWindowPreview(
        FrameworkElement anchor,
        Action<TaskbarWindowPreviewWindow>
            configure)
    {
        var preview =
            new TaskbarWindowPreviewWindow();
        configure(preview);
        preview.ActivateRequested +=
            TaskbarWindowPreview_ActivateRequested;
        preview.CloseRequested +=
            TaskbarWindowPreview_CloseRequested;
        preview.MouseEnter +=
            TaskbarWindowPreview_MouseEnter;
        preview.MouseLeave +=
            TaskbarWindowPreview_MouseLeave;
        preview.Closed +=
            TaskbarWindowPreview_Closed;
        _taskbarWindowPreview =
            preview;

        try
        {
            System.Windows.Point topLeft =
                anchor.PointToScreen(
                    new System.Windows.Point(
                        0,
                        0));
            System.Windows.Point bottomRight =
                anchor.PointToScreen(
                    new System.Windows.Point(
                        anchor.ActualWidth,
                        anchor.ActualHeight));
            bool shown =
                preview.TryShowAt(
                    this,
                    GetTargetDisplayBounds(),
                    (int)Math.Round(
                        topLeft.X),
                    (int)Math.Round(
                        (topLeft.Y
                         + bottomRight.Y)
                        / 2));
            if (!shown)
                return false;

            _taskbarPreviewInteractionActive =
                true;
            BeginTransientInteraction();
            return true;
        }
        catch
        {
            try
            {
                preview.Close();
            }
            catch
            {
                TaskbarWindowPreview_Closed(
                    preview,
                    EventArgs.Empty);
            }
            return false;
        }
    }

    private void
        TaskbarWindowPreview_ActivateRequested(
            WindowReference window)
    {
        bool closeWindowOverview =
            _searchWindowHoverTarget != null
            && _viewModel.IsSearchOpen;
        if (_viewModel.ActivateWindowCommand
                .CanExecute(window))
        {
            _viewModel.ActivateWindowCommand
                .Execute(window);
        }
        if (closeWindowOverview)
            _viewModel.IsSearchOpen = false;
    }

    private void
        TaskbarWindowPreview_CloseRequested(
            WindowReference window)
    {
        if (_viewModel.CloseWindowCommand
                .CanExecute(window))
        {
            _viewModel.CloseWindowCommand
                .Execute(window);
        }
    }

    private void
        TaskbarWindowPreview_MouseEnter(
            object sender,
            MouseEventArgs e) =>
        _taskbarHoverCloseTimer.Stop();

    private void
        TaskbarWindowPreview_MouseLeave(
            object sender,
            MouseEventArgs e) =>
        ScheduleTaskbarHoverPreviewClose();

    private void TaskbarWindowPreview_Closed(
        object? sender,
        EventArgs e)
    {
        if (sender
            is not TaskbarWindowPreviewWindow
                preview)
        {
            return;
        }

        preview.ActivateRequested -=
            TaskbarWindowPreview_ActivateRequested;
        preview.CloseRequested -=
            TaskbarWindowPreview_CloseRequested;
        preview.MouseEnter -=
            TaskbarWindowPreview_MouseEnter;
        preview.MouseLeave -=
            TaskbarWindowPreview_MouseLeave;
        preview.Closed -=
            TaskbarWindowPreview_Closed;
        if (ReferenceEquals(
                _taskbarWindowPreview,
                preview))
        {
            _taskbarWindowPreview = null;
        }
        _searchWindowHoverOpenTimer.Stop();
        _searchWindowHoverTarget = null;
        _searchWindowHoverResult = null;

        if (_taskbarPreviewInteractionActive)
        {
            _taskbarPreviewInteractionActive =
                false;
            EndTransientInteraction();
        }
    }

    private void TaskbarHoverMenu_MouseEnter(
        object sender,
        MouseEventArgs e) =>
        _taskbarHoverCloseTimer.Stop();

    private void TaskbarHoverMenu_MouseLeave(
        object sender,
        MouseEventArgs e) =>
        ScheduleTaskbarHoverPreviewClose();

    private void TaskbarHoverMenu_Closed(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            menu.MouseEnter -=
                TaskbarHoverMenu_MouseEnter;
            menu.MouseLeave -=
                TaskbarHoverMenu_MouseLeave;
            menu.Closed -=
                TaskbarHoverMenu_Closed;
            if (ReferenceEquals(
                    _taskbarHoverMenu,
                    menu))
            {
                _taskbarHoverMenu = null;
            }
        }

        _taskbarHoverCloseTimer.Stop();
    }

    private void ScheduleTaskbarHoverPreviewClose()
    {
        if (_taskbarHoverMenu?.IsOpen != true
            && _taskbarWindowPreview?.IsVisible
                != true)
            return;

        _taskbarHoverCloseTimer.Stop();
        _taskbarHoverCloseTimer.Start();
    }

    private void TaskbarHoverCloseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _taskbarHoverCloseTimer.Stop();
        if (_taskbarHoverButton?.IsMouseOver
                == true
            || _searchWindowHoverTarget
                    ?.IsMouseOver
                == true
            || _taskbarHoverMenu?.IsMouseOver
                == true
            || _taskbarWindowPreview?.IsMouseOver
                == true)
        {
            return;
        }

        CancelTaskbarHoverPreview(
            closeMenu: true);
    }

    private void CancelTaskbarHoverPreview(
        bool closeMenu = false)
    {
        _taskbarHoverOpenTimer.Stop();
        _searchWindowHoverOpenTimer.Stop();
        _taskbarHoverCloseTimer.Stop();
        if (closeMenu
            && _taskbarHoverMenu?.IsOpen
                == true)
        {
            _taskbarHoverMenu.IsOpen =
                false;
        }
        if (_taskbarWindowPreview?.IsVisible
                == true)
        {
            _taskbarWindowPreview.Close();
        }

        _taskbarHoverButton = null;
        _taskbarHoverTask = null;
        _searchWindowHoverTarget = null;
        _searchWindowHoverResult = null;
        if (_taskbarHoverMenu?.IsOpen
                != true)
        {
            _taskbarHoverMenu = null;
        }
    }

    private ContextMenu CreateTaskbarContextMenu()
    {
        var menu = new ContextMenu();
        FocusMenuTheme.Apply(menu);
        menu.Opened += TransientContextMenu_Opened;
        menu.Closed += TransientContextMenu_Closed;
        return menu;
    }

    private static TextBlock CreateWindowTitle(
        WindowReference window,
        string fallback) =>
        new()
        {
            Text = GetWindowTitle(
                window,
                fallback),
            MaxWidth = 340,
            TextTrimming =
                TextTrimming.CharacterEllipsis
        };

    private static string GetWindowAccessibleName(
        WindowReference window,
        string fallback)
    {
        string title = GetWindowTitle(
            window,
            fallback);
        string state =
            window.State switch
            {
                TrackedWindowState.Minimized =>
                    "已最小化",
                TrackedWindowState.Maximized =>
                    "已最大化",
                _ => string.Empty
            };
        string prefix =
            window.IsActive
                ? "当前窗口"
                : string.Empty;
        if (state.Length > 0)
        {
            prefix =
                prefix.Length > 0
                    ? prefix + "，" + state
                    : state;
        }
        if (window.IsTopmost)
        {
            prefix =
                prefix.Length > 0
                    ? prefix + "，已置顶"
                    : "已置顶";
        }
        return prefix.Length > 0
            ? prefix + "，" + title
            : title;
    }

    private static string
        GetWindowQuickActionAccessibleName(
            WindowReference window,
            string fallback) =>
        GetWindowAccessibleName(
            window,
            fallback)
        + "；中键或 Delete 关闭";

    private static string GetWindowTitle(
        WindowReference window,
        string fallback) =>
        string.IsNullOrWhiteSpace(window.Title)
            ? fallback
            : window.Title;

    private void OpenContextMenu(
        FrameworkElement button)
    {
        if (button.ContextMenu == null)
            return;

        FocusMenuTheme.Apply(button.ContextMenu);
        _autoHideTimer.Stop();
        button.ContextMenu.Closed -= ButtonContextMenu_Closed;
        button.ContextMenu.Closed += ButtonContextMenu_Closed;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void VolumeButton_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        float step = e.Delta > 0 ? 0.05f : -0.05f;
        _viewModel.AdjustMasterVolume(step);
        e.Handled = true;
    }

    private void StatusCenterButton_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton
            != MouseButton.Middle)
        {
            return;
        }

        if (_viewModel
            .SendMediaCommandCommand
            .CanExecute(
                MediaTransportAction
                    .PlayPause))
        {
            _viewModel
                .SendMediaCommandCommand
                .Execute(
                    MediaTransportAction
                        .PlayPause);
        }
        e.Handled = true;
    }

    private void WorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string destination })
            _viewModel.NavigateCommand.Execute(destination);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleSettingsCommand.Execute(null);
        QueueOverlayFocus(
            SettingsNavigationButton,
            SettingsEnableReplacementButton,
            () => _viewModel.IsSettingsOpen);
    }

    private void PowerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.TogglePowerMenuCommand.Execute(null);
        QueueOverlayFocus(
            StatusCenterButton,
            PowerMenuLockButton,
            () => _viewModel.IsPowerMenuOpen);
    }

    private void CloseOverlayPanels()
    {
        CancelTaskbarHoverPreview(
            closeMenu: true);
        _viewModel.IsSearchOpen = false;
        _viewModel.IsCalendarOpen = false;
        _viewModel.IsStatusCenterOpen = false;
        _viewModel.IsSettingsOpen = false;
        _viewModel.IsPowerMenuOpen = false;
        _overlayReturnFocusTarget = null;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (_viewModel.IsSearchOpen
            || _viewModel.IsCalendarOpen
            || _viewModel.IsStatusCenterOpen
            || _viewModel.IsSettingsOpen
            || _viewModel.IsPowerMenuOpen)
        {
            FrameworkElement? returnTarget =
                _overlayReturnFocusTarget;
            CloseOverlayPanels();
            if (returnTarget != null)
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (!_isExit
                            && IsVisible
                            && returnTarget.IsVisible
                            && returnTarget.IsEnabled)
                        {
                            returnTarget.Focus();
                        }
                    }),
                    DispatcherPriority.Input);
            }
        }
        else if (WorkspaceHost.Visibility == Visibility.Visible)
        {
            CollapseSidebar();
        }
        else
        {
            HideShell();
        }

        e.Handled = true;
    }

    private void EnableTaskbarReplacement()
    {
        if (_coordinator.TryEnableTaskbarReplacement(out string? error))
        {
            _viewModel.MarkReplacementEnabled(true);
            RegisterTaskbarSlotHotkeys();
            return;
        }

        UnregisterTaskbarSlotHotkeys();
        _viewModel.MarkReplacementStopped(
            TaskbarReplacementStopReason.StartupFailure,
            error ?? "无法启用任务栏替代模式。");
        FocusDialogService.Show(
            error ?? "无法启用任务栏替代模式。",
            "已保留 Windows 任务栏",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void DisableTaskbarReplacement()
    {
        UnregisterTaskbarSlotHotkeys();
        _coordinator.RestoreTaskbar();
        _viewModel.MarkReplacementEnabled(false);
    }

    private void Taskbar_ReplacementStopped(TaskbarReplacementStoppedEvent stopped)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_isExit)
                    return;

                UnregisterTaskbarSlotHotkeys();
                _viewModel.MarkReplacementStopped(stopped.Reason, stopped.Message);
            });
        }
        catch (InvalidOperationException)
        {
            // The window is already shutting down.
        }
    }

    private async Task ApplyDownloadedUpdate()
    {
        _hotZoneMonitor?.Stop();
        using FocusDialogInteractionLease
            interaction =
                FocusDialogInteractionLease
                    .Enter(this);
        try
        {
            _viewModel.UpdateStatus =
                "正在后台备份数据库并准备安装…";
            UpdateInstallPreparationCompletion
                preparation =
                    await _updateInstallPreparation
                        .PrepareAsync();
            if (_isExit)
                return;
            if (!preparation.Succeeded)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(
                        preparation.Error)
                        ? "数据库备份准备失败。"
                        : preparation.Error);
            }

            _viewModel.UpdateStatus =
                "备份完成，正在恢复任务栏并启动安装…";
            UnregisterTaskbarSlotHotkeys();
            _coordinator.RestoreTaskbar();
            DesktopHelper.ToggleDesktopIcons(true);
            _coordinator.Updates.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            if (!_hiddenToTray)
                _hotZoneMonitor?.Start();

            _viewModel.UpdateStatus =
                $"更新失败：{ex.Message}";
            FocusDialogService.Show(
                $"更新包已下载，但无法启动安装：{ex.Message}\n"
                + "Windows 任务栏已经恢复，可稍后重新尝试。",
                "无法安装更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        ShellClosingAction action =
            ShellShutdownPolicy.Decide(
                _isExit,
                _shutdownStarted,
                _shutdownCompleted);
        if (action
            == ShellClosingAction.HideToTray)
        {
            e.Cancel = true;
            _hiddenToTray = true;
            _toastManager.DismissAll();
            _hotZoneMonitor?.Stop();
            HideShell();
            DesktopHelper.ToggleDesktopIcons(true);
            return;
        }

        if (action
            != ShellClosingAction.AllowClose)
        {
            e.Cancel = true;
            if (action
                == ShellClosingAction
                    .BeginAsyncShutdown)
            {
                _shutdownStarted = true;
                BeginShutdownUiPhase();
                _ = CompleteShutdownAsync();
            }
            return;
        }
    }

    private void BeginShutdownUiPhase()
    {
        CancelTaskbarHoverPreview(
            closeMenu: true);
        CancelJumpListLoad();
        ClearTaskbarDropCue();
        SetTaskbarFileDropTarget(
            null);
        _taskbarExternalFileDragActive =
            false;
        _autoHideTimer.Stop();
        _transientInteractionDepth = 0;
        UnregisterTaskbarSlotHotkeys();
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (_summonHotkeyRegistered && hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(hwnd, SummonHotkeyId);
            _summonHotkeyRegistered = false;
        }
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _hotZoneMonitor?.Dispose();
        _hotZoneMonitor = null;
        _edgeIndicator?.Close();
        _edgeIndicator = null;
        _toastManager.Dispose();
        _coordinator.Taskbar.ReplacementStopped -= Taskbar_ReplacementStopped;
        _viewModel.RequestClose -= ForceClose;
        _viewModel.RequestEnableReplacement -= EnableTaskbarReplacement;
        _viewModel.RequestDisableReplacement -= DisableTaskbarReplacement;
        _viewModel.RequestApplyUpdate -= ApplyDownloadedUpdate;
        _viewModel.UpdateAvailable -= ViewModel_UpdateAvailable;
        _viewModel.PomodoroCompleted -=
            ViewModel_PomodoroCompleted;
        _viewModel.TaskCaptured -=
            ViewModel_TaskCaptured;
        _viewModel.TaskCompleted -=
            ViewModel_TaskCompleted;
        _viewModel.DisplayTargetChanged -=
            ViewModel_DisplayTargetChanged;
        _viewModel.WorkspacePinChanged -=
            ViewModel_WorkspacePinChanged;
        _viewModel.PropertyChanged -=
            ViewModel_PropertyChanged;
        HideShell();
    }

    private async Task CompleteShutdownAsync()
    {
        try
        {
            await Task.WhenAll(
                _viewModel.DisposeAsync(),
                _updateInstallPreparation
                    .CompleteAsync(),
                _coordinator.DisposeAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "异步退出排空失败："
                + ex.Message);
        }

        if (Dispatcher.HasShutdownStarted
            || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _shutdownCompleted = true;
        Close();
        Application.Current.Shutdown();
    }

    public void ShowFromTray()
    {
        if (!_shellStartupReady)
            return;

        _hiddenToTray = false;
        EnsureHotZoneMonitor();
        _hotZoneMonitor?.Start();
        ExpandSidebar();
        Activate();
        FocusCompactDock();
    }

    public void ForceClose()
    {
        if (_isExit)
            return;

        _isExit = true;
        _coordinator.RestoreTaskbar();
        MyNotifyIcon.Dispose();
        Close();
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CancelTaskbarHoverPreview(
                closeMenu: true);
            _viewModel
                .RefreshDisplayTargetOptions();
            PositionAtTargetRightEdge();
            _toastManager.Reposition();
            _hotZoneMonitor?.RefreshDisplayBounds();
            _edgeIndicator?.Reposition();
        });
    }

    private void ViewModel_DisplayTargetChanged()
    {
        CancelTaskbarHoverPreview(
            closeMenu: true);
        EnsureEdgeIndicator();
        if (_edgeIndicator != null)
        {
            _edgeIndicator.TargetValue =
                _viewModel.DisplayTargetMode;
        }
        PositionAtTargetRightEdge();
        _hotZoneMonitor?.RefreshDisplayBounds();
        _edgeIndicator?.Reposition();
    }

    private Rectangle GetTargetDisplayBounds() =>
        ShellDisplayTarget.GetBounds(
            _viewModel.DisplayTargetMode);

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CancelTaskbarHoverPreview(
                closeMenu: true);
            ThemeService.ApplyCurrentTheme();
            ApplyDwmBackdrop();
        });
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == ShellMessages.ShowMainWindow)
        {
            ShowFromTray();
            handled = true;
        }
        else if (message == WmHotkey && wParam.ToInt32() == SummonHotkeyId)
        {
            OpenSearchFromSummonHotkey();
            handled = true;
        }
        else if (message == WmHotkey
                 && _taskbarSlotHotkeySession
                     ?.TryResolve(
                         wParam.ToInt32(),
                         out TaskbarSlotHotkeyBinding
                             binding)
                 == true)
        {
            ExecuteTaskbarSlotHotkey(
                binding);
            handled = true;
        }
        else if (message == WmDpiChanged)
        {
            _ = Dispatcher.BeginInvoke(
                new Action(PositionAtTargetRightEdge),
                DispatcherPriority.Loaded);
        }

        return IntPtr.Zero;
    }

    private void RegisterTaskbarSlotHotkeys()
    {
        UnregisterTaskbarSlotHotkeys(
            updateStatus: false);
        if (!_viewModel
                .EnableTaskbarSlotHotkeys)
        {
            _viewModel
                .SetTaskbarSlotShortcutDisabled();
            return;
        }

        IntPtr hwnd =
            new WindowInteropHelper(this)
                .Handle;
        if (hwnd == IntPtr.Zero
            || !_coordinator.Taskbar
                .IsReplacementEnabled)
        {
            _viewModel
                .SetTaskbarSlotShortcutDisabled();
            return;
        }

        var session =
            new TaskbarSlotHotkeySession(
                binding =>
                    NativeMethods.RegisterHotKey(
                        hwnd,
                        binding.Id,
                        binding.Modifiers,
                        binding.VirtualKey),
                hotkeyId =>
                    NativeMethods.UnregisterHotKey(
                        hwnd,
                        hotkeyId));
        TaskbarSlotHotkeyRegistration
            registration =
                session.RegisterAvailable();
        _taskbarSlotHotkeySession =
            session;
        _viewModel
            .SetTaskbarSlotShortcutStatus(
                registration);
    }

    private void UnregisterTaskbarSlotHotkeys(
        bool updateStatus = true)
    {
        _taskbarSlotHotkeySession?.Dispose();
        _taskbarSlotHotkeySession = null;
        if (updateStatus)
        {
            _viewModel
                .SetTaskbarSlotShortcutDisabled();
        }
    }

    private void ExecuteTaskbarSlotHotkey(
        TaskbarSlotHotkeyBinding binding)
    {
        if (_isExit
            || !_coordinator.Taskbar
                .IsReplacementEnabled)
        {
            return;
        }

        TaskbarAppItem? task =
            binding.SlotIndex
                < _viewModel.TaskbarApps.Count
                ? _viewModel.TaskbarApps[
                    binding.SlotIndex]
                : null;
        TaskbarSlotInvocationKind invocation =
            TaskbarSlotHotkeyPolicy
                .GetInvocation(
                    _viewModel.TaskbarApps.Count,
                    binding,
                    task?.CanLaunchNewInstance
                        == true);
        switch (invocation)
        {
            case TaskbarSlotInvocationKind
                .ActivateOrLaunch:
                _viewModel
                    .ActivateTaskbarAppCommand
                    .Execute(task);
                break;
            case TaskbarSlotInvocationKind
                .LaunchNewInstance:
                _viewModel
                    .LaunchNewTaskbarAppCommand
                    .Execute(task);
                break;
            default:
                ShowTaskbarSlotHotkeyUnavailable(
                    binding,
                    task);
                break;
        }
    }

    private void
        ShowTaskbarSlotHotkeyUnavailable(
            TaskbarSlotHotkeyBinding
                binding,
            TaskbarAppItem? task)
    {
        string message =
            task == null
                ? $"第 {binding.SlotNumber} 个应用槽当前为空。"
                : $"“{task.DisplayName}”没有可靠的"
                  + "新实例启动目标。";
        _toastManager.Enqueue(
            new FocusToastNotification(
                $"taskbar-slot-{binding.Id}",
                "快速应用未执行",
                message,
                "\uE783",
                FocusToastKind.Warning));
    }

    private void FocusCompactDock()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isExit || !IsVisible)
                return;

            SearchButton.Focus();
        }, DispatcherPriority.Input);
    }

    private void OpenSearchFromSummonHotkey()
    {
        if (_isExit || !_shellStartupReady)
        {
            return;
        }

        _hiddenToTray = false;
        ExpandSidebar();
        Activate();
        ApplySearchEntryState(
            ShellSearchEntryPolicy
                .PrepareUnifiedSearch(
                    _viewModel.SearchQuery));
        if (!_viewModel.IsSearchOpen)
        {
            _viewModel.ToggleSearchCommand
                .Execute(null);
        }

        QueueOverlayFocus(
            SearchButton,
            SearchBox,
            () => _viewModel.IsSearchOpen);
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_viewModel.IsSearchOpen
                    && SearchBox.IsKeyboardFocusWithin)
                {
                    SearchBox.SelectAll();
                }
            }),
            DispatcherPriority.Input);
    }

    private void UpdateEdgeIndicatorVisibility()
    {
        if (_hiddenToTray || IsVisible || !_isHotZoneAvailable)
        {
            _edgeIndicator?.HideIndicator();
            return;
        }

        EnsureEdgeIndicator();
        _edgeIndicator?.ShowIndicator();
    }

    private void Sidebar_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(DesktopFile))
            || e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            BeginDesktopFileDrag(
                e.Data.GetDataPresent(
                    typeof(DesktopFile)));
        }
    }

    private void Sidebar_DragLeave(object sender, DragEventArgs e)
    {
        if (!IsCursorInsideShell()
            && _desktopDragSession.EndExternal())
        {
            ScheduleAutoHide();
        }
    }

    public void BeginDesktopFileDrag(
        bool ownedByPanel = true)
    {
        _desktopDragSession.Begin(
            ownedByPanel);
        _viewModel.NavigateCommand.Execute("Files");
        ExpandSidebar();
    }

    public void EndDesktopFileDrag()
    {
        _desktopDragSession.End();
        ScheduleAutoHide();
    }

    public void EndExternalDesktopFileDrag()
    {
        if (_desktopDragSession.EndExternal())
            ScheduleAutoHide();
    }

    private void TaskbarApp_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pinnedDragStart = e.GetPosition(this);
    }

    private void TaskbarApp_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || sender is not FrameworkElement { DataContext: TaskbarAppItem app }
            || !app.CanPin)
        {
            return;
        }

        System.Windows.Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _pinnedDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _pinnedDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        BeginTransientInteraction();
        _lastTaskbarDragScrollTick = -1;
        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                new DataObject(
                    typeof(TaskbarAppItem),
                    app),
                DragDropEffects.Move);
        }
        finally
        {
            ClearTaskbarDropCue();
            _lastTaskbarDragScrollTick = -1;
            EndTransientInteraction();
        }
    }

    private void TaskbarAppsHost_PreviewDragOver(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(
                typeof(TaskbarAppItem)))
        {
            return;
        }

        long currentTick =
            Environment.TickCount64;
        CompactTaskbarScrollState state =
            CompactTaskbarScrollPolicy.GetState(
                TaskbarAppsScrollViewer
                    .VerticalOffset,
                TaskbarAppsScrollViewer
                    .ScrollableHeight);
        CompactTaskbarDragScrollDecision
            decision =
                CompactTaskbarDragScrollPolicy
                    .GetDecision(
                        TaskbarAppsScrollViewer
                            .VerticalOffset,
                        TaskbarAppsScrollViewer
                            .ScrollableHeight,
                        e.GetPosition(
                                TaskbarAppsScrollViewer)
                            .Y,
                        TaskbarAppsScrollViewer
                            .ViewportHeight,
                        state.CanScrollUp,
                        state.CanScrollDown,
                        CompactTaskbarDragScrollPolicy
                            .IsScrollDue(
                                _lastTaskbarDragScrollTick,
                                currentTick));
        if (!decision.ShouldScroll)
            return;

        _lastTaskbarDragScrollTick =
            currentTick;
        TaskbarAppsScrollViewer
            .ScrollToVerticalOffset(
                decision.TargetOffset);
    }

    private void TaskbarAppsHost_PreviewDragEnter(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(
                typeof(TaskbarAppItem))
            || !e.Data.GetDataPresent(
                DataFormats.FileDrop))
        {
            return;
        }

        BeginTaskbarExternalFileDrag();
        e.Effects =
            DragDropEffects.Copy;
        e.Handled = true;
    }

    private void TaskbarAppsHost_PreviewDragLeave(
        object sender,
        DragEventArgs e)
    {
        System.Windows.Point position =
            e.GetPosition(TaskbarAppsHost);
        if (position.X < 0
            || position.Y < 0
            || position.X
                > TaskbarAppsHost.ActualWidth
            || position.Y
                > TaskbarAppsHost.ActualHeight)
        {
            ClearTaskbarDropCue();
            EndTaskbarExternalFileDrag();
        }
    }

    private void TaskbarAppsHost_PreviewDrop(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(
                DataFormats.FileDrop))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            EndTaskbarExternalFileDrag,
            DispatcherPriority.Input);
    }

    private void TaskbarApp_DragOver(object sender, DragEventArgs e)
    {
        if (sender
                is FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem
                            fileTarget
                }
            && e.Data.GetDataPresent(
                DataFormats.FileDrop))
        {
            bool canOpen =
                fileTarget
                    .CreateLaunchItem()
                != null;
            SetTaskbarFileDropTarget(
                canOpen
                    ? fileTarget
                    : null);
            ClearTaskbarDropCue();
            e.Effects =
                canOpen
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (sender
                is not FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem target
                } element
            || e.Data.GetData(
                    typeof(TaskbarAppItem))
                is not TaskbarAppItem source
            || ReferenceEquals(
                source,
                target))
        {
            ClearTaskbarDropCue();
            e.Effects =
                DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool isFirstUnpinned =
            !target.IsPinned
            && ReferenceEquals(
                _viewModel.TaskbarApps
                    .FirstOrDefault(item =>
                        !item.IsPinned),
                target);
        TaskbarDropPlacement? cuePlacement =
            TaskbarAppDropPolicy
                .GetCuePlacement(
                    target.IsPinned,
                    isFirstUnpinned,
                    e.GetPosition(element).Y,
                    element.ActualHeight);
        if (cuePlacement.HasValue)
        {
            SetTaskbarDropCue(
                target,
                cuePlacement.Value);
        }
        else
        {
            ClearTaskbarDropCue();
        }
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void TaskbarApp_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (sender
                is FrameworkElement
                    fileElement
            && fileElement.DataContext
                is TaskbarAppItem
                    fileTarget
            && ReferenceEquals(
                fileTarget,
                _taskbarFileDropTarget))
        {
            System.Windows.Point
                filePosition =
                    e.GetPosition(
                        fileElement);
            if (filePosition.X < 0
                || filePosition.Y < 0
                || filePosition.X
                    > fileElement.ActualWidth
                || filePosition.Y
                    > fileElement.ActualHeight)
            {
                SetTaskbarFileDropTarget(
                    null);
            }
        }

        if (sender
                is not FrameworkElement element
            || element.DataContext
                is not TaskbarAppItem target
            || !ReferenceEquals(
                target,
                _taskbarDropCueItem))
        {
            return;
        }

        System.Windows.Point position =
            e.GetPosition(element);
        if (position.X < 0
            || position.Y < 0
            || position.X
                > element.ActualWidth
            || position.Y
                > element.ActualHeight)
        {
            ClearTaskbarDropCue();
        }
    }

    private void TaskbarApp_Drop(object sender, DragEventArgs e)
    {
        if (sender
                is FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem
                            fileTarget
                }
            && e.Data.GetDataPresent(
                DataFormats.FileDrop))
        {
            AppLaunchItem? launch =
                fileTarget
                    .CreateLaunchItem();
            string[] paths =
                TryGetFileDropPaths(
                    e.Data);
            EndTaskbarExternalFileDrag();
            if (launch != null
                && paths.Length > 0)
            {
                StartTaskbarFileDrop(
                    fileTarget.DisplayName,
                    launch,
                    paths);
            }

            e.Handled = true;
            return;
        }

        if (sender
                is FrameworkElement
                {
                    DataContext:
                        TaskbarAppItem target
                } element
            && e.Data.GetData(typeof(TaskbarAppItem)) is TaskbarAppItem source)
        {
            TaskbarDropPlacement placement =
                TaskbarAppDropPolicy.GetPlacement(
                    e.GetPosition(element).Y,
                    element.ActualHeight);
            ClearTaskbarDropCue();
            StartTaskbarAppDrop(
                source,
                target,
                placement);
        }

        e.Handled = true;
    }

    private void BeginTaskbarExternalFileDrag()
    {
        if (_taskbarExternalFileDragActive)
            return;

        _taskbarExternalFileDragActive =
            true;
        BeginTransientInteraction();
    }

    private void EndTaskbarExternalFileDrag()
    {
        SetTaskbarFileDropTarget(
            null);
        if (!_taskbarExternalFileDragActive)
            return;

        _taskbarExternalFileDragActive =
            false;
        EndTransientInteraction();
    }

    private void SetTaskbarFileDropTarget(
        TaskbarAppItem? target)
    {
        if (ReferenceEquals(
                _taskbarFileDropTarget,
                target))
        {
            return;
        }

        _taskbarFileDropTarget
            ?.SetFileDropTarget(false);
        _taskbarFileDropTarget =
            target;
        _taskbarFileDropTarget
            ?.SetFileDropTarget(true);
    }

    private static string[]
        TryGetFileDropPaths(
            IDataObject data)
    {
        try
        {
            return data.GetData(
                       DataFormats.FileDrop)
                       is string[] paths
                ? paths.ToArray()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void StartTaskbarFileDrop(
        string displayName,
        AppLaunchItem launch,
        IReadOnlyList<string> paths)
    {
        BeginTransientInteraction();
        AsyncInteractionRunner.Start(
            async () =>
            {
                AppFileLaunchResult result =
                    await _coordinator
                        .AppFiles
                        .OpenAsync(
                            launch,
                            paths);
                if (_isExit
                    || result
                        .IsCompleteSuccess)
                {
                    return;
                }

                if (result.LaunchSucceeded)
                {
                    _toastManager.Enqueue(
                        new FocusToastNotification(
                            "taskbar-file-drop-partial",
                            "部分项目已打开",
                            $"已交给“{displayName}”打开 "
                            + $"{result.OpenedCount} 个项目；"
                            + $"{result.IgnoredCount} 个路径"
                            + "无效、不可访问或超过 32 项上限。",
                            "\uE7C3",
                            FocusToastKind.Warning));
                    return;
                }

                _toastManager.Enqueue(
                    new FocusToastNotification(
                        "taskbar-file-drop-failed",
                        "无法用目标应用打开",
                        $"“{displayName}”未能接收这些项目。"
                        + (string.IsNullOrWhiteSpace(
                                result.FailureReason)
                            ? string.Empty
                            : " "
                              + result
                                  .FailureReason),
                        "\uE783",
                        FocusToastKind.Warning));
            },
            ex =>
                _toastManager.Enqueue(
                    new FocusToastNotification(
                        "taskbar-file-drop-error",
                        "文件拖放失败",
                        ex.Message,
                        "\uE783",
                        FocusToastKind.Warning)),
            EndTransientInteraction);
    }

    private void StartTaskbarAppDrop(
        TaskbarAppItem source,
        TaskbarAppItem target,
        TaskbarDropPlacement placement)
    {
        BeginTransientInteraction();
        AsyncInteractionRunner.Start(
            () =>
                _viewModel.MoveTaskbarApp(
                    source,
                    target,
                    placement),
            ex =>
                FocusDialogService.Show(
                    $"无法保存应用栏顺序：{ex.Message}",
                    "应用栏排序失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning),
            EndTransientInteraction);
    }

    private void SetTaskbarDropCue(
        TaskbarAppItem target,
        TaskbarDropPlacement placement)
    {
        if (!ReferenceEquals(
                _taskbarDropCueItem,
                target))
        {
            _taskbarDropCueItem
                ?.SetDropPlacement(null);
            _taskbarDropCueItem =
                target;
        }

        target.SetDropPlacement(
            placement);
    }

    private void ClearTaskbarDropCue()
    {
        _taskbarDropCueItem
            ?.SetDropPlacement(null);
        _taskbarDropCueItem = null;
    }

    private sealed record
        TaskbarWindowMenuAction(
            ContextMenu Menu,
            WindowReference Window);

    private sealed record
        TaskbarJumpListMenuAction(
            AppJumpListItem Item,
            AppJumpListApplicationLaunch?
                Application,
            AppJumpListCategory Category);

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
