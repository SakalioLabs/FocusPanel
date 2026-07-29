using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
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
    private const double CompactTaskbarOverflowInset = 30;
    private const int TaskbarHoverOpenDelayMilliseconds = 420;
    private const int TaskbarHoverCloseDelayMilliseconds = 260;
    private const int TaskbarWindowCycleThrottleMilliseconds = 90;
    private const int TaskbarWindowCycleMemoryMilliseconds = 2000;
    private const int SwShowNoActivate = 4;
    private const int WmHotkey = 0x0312;
    private const int WmDpiChanged = 0x02E0;
    private const int SummonHotkeyId = 0x4650;

    private readonly ShellCoordinator _coordinator;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _taskbarHoverOpenTimer;
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
    private bool _isHotZoneAvailable;
    private bool _shellStartupReady;
    private bool _autoHideIgnoresInputFocus;
    private FrameworkElement? _overlayReturnFocusTarget;
    private int _transientInteractionDepth;
    private System.Windows.Point _pinnedDragStart;
    private long _lastTaskbarDragScrollTick = -1;
    private TaskbarAppItem? _taskbarDropCueItem;
    private Button? _taskbarHoverButton;
    private TaskbarAppItem? _taskbarHoverTask;
    private ContextMenu? _taskbarHoverMenu;
    private TaskbarWindowPreviewWindow?
        _taskbarWindowPreview;
    private bool _taskbarPreviewInteractionActive;
    private TaskbarSlotHotkeySession?
        _taskbarSlotHotkeySession;
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
            _coordinator.Updates);

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
            bool shouldHide = ShellAutoHidePolicy.ShouldHide(
                _viewModel.IsWorkspacePinned,
                _desktopDragSession.IsActive,
                transientSurfaceActive,
                IsCursorInsideShell(),
                IsInputFocusActive(),
                _autoHideIgnoresInputFocus);
            if (!shouldHide)
            {
                _autoHideTimer.Start();
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
            HideShell();
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

    private void OpenUpdateSettings()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        CloseOverlayPanels();
        _viewModel.IsSettingsOpen = true;
        Activate();
    }

    private void OpenPomodoroWorkspace()
    {
        _hiddenToTray = false;
        ExpandSidebar();
        _viewModel.NavigateCommand.Execute("Pomodoro");
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
            GetTargetDisplayBounds);
        _hotZoneMonitor.OpenRequested += (_, _) => OpenCompactDock();
        _hotZoneMonitor.AvailabilityChanged += isAvailable =>
        {
            _isHotZoneAvailable = isAvailable;
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
        int delayMilliseconds = 350,
        bool ignoreInputFocus = false)
    {
        if (_desktopDragSession.IsActive)
            return;

        _autoHideTimer.Stop();
        _autoHideIgnoresInputFocus = ignoreInputFocus;
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
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
        ExpandSidebar();
        _overlayReturnFocusTarget =
            SearchButton;
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel.IsSearchOpen)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }
            else
            {
                _overlayReturnFocusTarget =
                    null;
            }
        }, DispatcherPriority.Input);
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
            || SearchResultsList.Items[launchIndex] is not AppLaunchItem app
            || !_viewModel.LaunchAppCommand.CanExecute(app))
        {
            return;
        }

        _viewModel.LaunchAppCommand.Execute(app);
        e.Handled = true;
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e) => ExpandSidebar();
    private void FocusCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleFocusCenterCommand.Execute(null);
        QueueOverlayFocus(
            FocusCenterButton,
            FocusCenterLastWorkspaceButton,
            () => _viewModel.IsFocusCenterOpen);
    }

    private void StatusCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleStatusCenterCommand.Execute(null);
        QueueOverlayFocus(
            StatusCenterButton,
            StatusCenterQuickSettingsButton,
            () => _viewModel.IsStatusCenterOpen);
    }

    private void CalendarPanelButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleCalendarCommand.Execute(null);
        if (_viewModel.IsCalendarOpen)
        {
            _overlayReturnFocusTarget =
                TimeButton;
        }
        else
        {
            _overlayReturnFocusTarget =
                null;
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
            task.CanLaunchNewInstance);
        if (action == TaskbarAppClickAction.LaunchNewInstance)
        {
            _viewModel.LaunchNewTaskbarAppCommand.Execute(task);
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
        if (sender
                is not Button
                {
                    DataContext:
                        TaskbarAppItem task
                }
            || task.WindowCount < 2)
        {
            return;
        }

        long now =
            Environment.TickCount64;
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
        if (sameCycleSession
            && now
                - _lastTaskbarWindowCycleTick
                < TaskbarWindowCycleThrottleMilliseconds)
        {
            e.Handled = true;
            return;
        }

        WindowReference? target =
            TaskbarWindowCyclePolicy.SelectTarget(
                task.Windows,
                e.Delta,
                sameCycleSession
                    ? _lastTaskbarWindowCycleHandle
                    : IntPtr.Zero);
        if (target == null)
            return;

        _lastTaskbarWindowCycleIdentity =
            task.IdentityKey;
        _lastTaskbarWindowCycleHandle =
            target.Handle;
        _lastTaskbarWindowCycleTick =
            now;
        _viewModel.ActivateWindowCommand.Execute(
            target);
        e.Handled = true;
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

        if (task.CreateLaunchItem() != null)
        {
            menu.Items.Add(new MenuItem
            {
                Header = task.IsRunning ? "启动新实例" : "启动",
                Command = _viewModel.LaunchNewTaskbarAppCommand,
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
        TaskbarAppItem task)
    {
        var preview =
            new TaskbarWindowPreviewWindow();
        preview.Configure(task);
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
                button.PointToScreen(
                    new System.Windows.Point(
                        0,
                        0));
            System.Windows.Point bottomRight =
                button.PointToScreen(
                    new System.Windows.Point(
                        button.ActualWidth,
                        button.ActualHeight));
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
        if (_viewModel.ActivateWindowCommand
                .CanExecute(window))
        {
            _viewModel.ActivateWindowCommand
                .Execute(window);
        }
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
        return window.IsActive
            ? $"当前窗口，{title}"
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

    private void OpenContextMenu(Button button)
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

    private void VolumeButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _viewModel.ToggleMuteCommand.Execute(null);
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
            FocusCenterButton,
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
        _viewModel.IsSearchOpen = false;
        _viewModel.IsCalendarOpen = false;
        _viewModel.IsFocusCenterOpen = false;
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
            || _viewModel.IsFocusCenterOpen
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
            _hiddenToTray = false;
            ExpandSidebar();
            Activate();
            FocusCompactDock();
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
        }
    }

    private void TaskbarApp_DragOver(object sender, DragEventArgs e)
    {
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
