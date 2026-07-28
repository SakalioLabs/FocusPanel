using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FocusPanel.Views;

public partial class MainWindow : Window
{
    private const double CompactWidth = 76;
    private const double ExpandedWidth = 720;
    private const double ScreenMargin = 12;
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaWindowCornerPreference = 33;
    private const int DwmaBorderColor = 34;
    private const int DwmaSystemBackdropType = 38;
    private const int DwmcpRound = 2;
    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3;
    private const int SwShowNoActivate = 4;
    private const int WmHotkey = 0x0312;
    private const int SummonHotkeyId = 0x4650;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkSpace = 0x20;

    private readonly ShellCoordinator _coordinator;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _autoHideTimer;
    private EdgeHotZoneMonitor? _hotZoneMonitor;
    private EdgeIndicatorWindow? _edgeIndicator;
    private HwndSource? _windowSource;
    private bool _summonHotkeyRegistered;
    private bool _isExit;
    private bool _hiddenToTray;
    private bool _isDesktopFileDragging;
    private bool _isHotZoneAvailable;
    private bool _autoHideIgnoresKeyboardFocus;
    private int _transientInteractionDepth;
    private System.Windows.Point _pinnedDragStart;

    public MainWindow()
    {
        _coordinator = new ShellCoordinator();
        _viewModel = new MainViewModel(
            _coordinator.Apps,
            _coordinator.Windows,
            _coordinator.SystemStatus,
            _coordinator.Updates);

        InitializeComponent();
        DataContext = _viewModel;
        MyNotifyIcon.Icon = SystemIcons.Application;

        _viewModel.RequestClose += ForceClose;
        _viewModel.RequestEnableReplacement += EnableTaskbarReplacement;
        _viewModel.RequestDisableReplacement += DisableTaskbarReplacement;
        _viewModel.RequestApplyUpdate += ApplyDownloadedUpdate;
        _viewModel.UpdateAvailable += ViewModel_UpdateAvailable;
        _viewModel.WorkspaceRequested += _ => ExpandSidebar();
        _coordinator.Taskbar.ReplacementStopped += Taskbar_ReplacementStopped;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            bool shouldHide = ShellAutoHidePolicy.ShouldHide(
                _isDesktopFileDragging,
                _transientInteractionDepth > 0 || Mouse.Captured != null,
                IsCursorInsideShell(),
                ShellBorder.IsKeyboardFocusWithin,
                _autoHideIgnoresKeyboardFocus);
            if (!shouldHide)
            {
                _autoHideTimer.Start();
                return;
            }

            HideShell();
        };

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = CompactWidth;
        ShowInTaskbar = false;

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        Deactivated += (_, _) => ScheduleAutoHide(220, ignoreKeyboardFocus: true);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAtPrimaryRightEdge();
        EnsureEdgeIndicator();
        EnsureHotZoneMonitor();
        _hotZoneMonitor?.Start();

        if (_viewModel.IsOnboardingVisible)
        {
            ExpandSidebar();
            Activate();
        }
        else
        {
            HideShell();
            if (_viewModel.IsReplacementEnabled)
                Dispatcher.BeginInvoke(EnableTaskbarReplacement, DispatcherPriority.ApplicationIdle);
        }

        Dispatcher.BeginInvoke(
            new Action(() => _ = _viewModel.CheckForUpdatesInBackgroundAsync()),
            DispatcherPriority.ContextIdle);
    }

    private void ViewModel_UpdateAvailable(AppUpdateInfo update)
    {
        MyNotifyIcon.ShowBalloonTip(
            "FocusPanel 更新可用",
            $"GitHub 已发布 v{update.Version}，打开设置即可一键安装。",
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(hwnd);
        _windowSource?.AddHook(WindowMessageHook);
        _summonHotkeyRegistered = NativeMethods.RegisterHotKey(
            hwnd,
            SummonHotkeyId,
            ModControl | ModAlt,
            VkSpace);
        ApplyDwmBackdrop();
    }

    private void ApplyDwmBackdrop()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // DWM owns both the single outer silhouette and the acrylic backdrop.
        // No WPF or GDI region is layered on top of this outline.
        int cornerPreference = DwmcpRound;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            DwmaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));

        int borderColor = unchecked((int)0xFFFFFFFE);
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            DwmaBorderColor,
            ref borderColor,
            sizeof(int));

        bool useDark = ThemeService.IsDarkTheme;
        int darkMode = useDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            DwmaUseImmersiveDarkMode,
            ref darkMode,
            sizeof(int));

        int backdrop = ThemeService.CanUseTransparency
            ? DwmsbtTransientWindow
            : DwmsbtNone;
        bool backdropActive = false;
        if (backdrop == DwmsbtTransientWindow)
        {
            var margins = new NativeMethods.Margins(-1, -1, -1, -1);
            int frameResult = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            int backdropResult = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                DwmaSystemBackdropType,
                ref backdrop,
                sizeof(int));
            backdropActive = frameResult == 0 && backdropResult == 0;
        }
        else
        {
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                DwmaSystemBackdropType,
                ref backdrop,
                sizeof(int));
        }

        if (HwndSource.FromHwnd(hwnd) is HwndSource source)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        ThemeService.SetNativeBackdropActive(backdropActive);
    }

    private void EnsureHotZoneMonitor()
    {
        if (_hotZoneMonitor != null)
            return;

        _hotZoneMonitor = new EdgeHotZoneMonitor(
            _coordinator.Windows,
            () => _viewModel.DisableHotZoneInFullscreen);
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
    }

    private void PositionAtPrimaryRightEdge()
    {
        Forms.Screen? primary = Forms.Screen.PrimaryScreen;
        if (primary == null)
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        uint dpi = hwnd == IntPtr.Zero ? 96u : NativeMethods.GetDpiForWindow(hwnd);
        double scale = dpi == 0 ? 1.0 : dpi / 96.0;
        Left = primary.Bounds.Right / scale - Width - ScreenMargin;
        Top = primary.Bounds.Top / scale + ScreenMargin;
        Height = Math.Max(320, primary.Bounds.Height / scale - ScreenMargin * 2);
    }

    private void OpenCompactDock()
    {
        if (_hiddenToTray || IsVisible)
            return;

        _autoHideTimer.Stop();
        SetShellWidth(CompactWidth, false, false);
        ShowWithoutActivating();
        ScheduleAutoHide(900);
    }

    public void ExpandSidebar()
    {
        _hiddenToTray = false;
        _autoHideTimer.Stop();
        WorkspaceHost.Visibility = Visibility.Visible;
        ShowWithoutActivating();
        SetShellWidth(ExpandedWidth, true, true);
        ScheduleAutoHide(1200);
    }

    public void CollapseSidebar()
    {
        if (_isDesktopFileDragging)
            return;

        CloseOverlayPanels();
        SetShellWidth(CompactWidth, false, true);
    }

    private void SetShellWidth(double targetWidth, bool workspaceVisible, bool animate)
    {
        Forms.Screen? primary = Forms.Screen.PrimaryScreen;
        if (primary == null)
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        uint dpi = hwnd == IntPtr.Zero ? 96u : NativeMethods.GetDpiForWindow(hwnd);
        double scale = dpi == 0 ? 1.0 : dpi / 96.0;
        double targetLeft = primary.Bounds.Right / scale - targetWidth - ScreenMargin;
        double currentWidth = ActualWidth > 0 ? ActualWidth : Width;
        double currentLeft = Left;
        bool reduceMotion = !SystemParameters.ClientAreaAnimation
            || SystemParameters.HighContrast;

        BeginAnimation(WidthProperty, null);
        BeginAnimation(LeftProperty, null);
        Width = targetWidth;
        Left = targetLeft;

        if (!animate || reduceMotion || Math.Abs(currentWidth - targetWidth) < 0.5)
        {
            WorkspaceHost.Visibility = workspaceVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            return;
        }

        if (workspaceVisible)
            WorkspaceHost.Visibility = Visibility.Visible;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var widthAnimation = new DoubleAnimation(currentWidth, targetWidth, duration)
        {
            EasingFunction = easing
        };
        var leftAnimation = new DoubleAnimation(currentLeft, targetLeft, duration)
        {
            EasingFunction = easing
        };
        widthAnimation.Completed += (_, _) =>
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            Width = targetWidth;
            Left = targetLeft;
            if (!workspaceVisible)
                WorkspaceHost.Visibility = Visibility.Collapsed;
        };

        BeginAnimation(WidthProperty, widthAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(LeftProperty, leftAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void HideShell()
    {
        CloseOverlayPanels();
        Visibility = Visibility.Hidden;
        UpdateEdgeIndicatorVisibility();
    }

    private void ShowWithoutActivating()
    {
        _edgeIndicator?.HideIndicator();
        if (!IsVisible)
            Show();
        Visibility = Visibility.Visible;
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
        bool ignoreKeyboardFocus = false)
    {
        if (_isDesktopFileDragging)
            return;

        _autoHideTimer.Stop();
        _autoHideIgnoresKeyboardFocus = ignoreKeyboardFocus;
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
        _autoHideTimer.Start();
    }

    private void Shell_MouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        _autoHideIgnoresKeyboardFocus = false;
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

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
    {
        CollapseSidebar();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel.IsSearchOpen)
                SearchBox.Focus();
        }, DispatcherPriority.Input);
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e) => ExpandSidebar();
    private void FocusCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleFocusCenterCommand.Execute(null);
    }

    private void StatusCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleStatusCenterCommand.Execute(null);
    }

    private void CalendarPanelButton_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.ToggleCalendarCommand.Execute(null);
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
        => BeginTransientInteraction();

    private void TransientContextMenu_Closed(object sender, RoutedEventArgs e)
        => EndTransientInteraction();

    public void BeginTransientInteraction()
    {
        _transientInteractionDepth++;
        _autoHideTimer.Stop();
        _autoHideIgnoresKeyboardFocus = false;
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
        if (sender is not Button { DataContext: TaskbarAppItem task } button)
            return;

        if (task.WindowCount <= 1)
        {
            _viewModel.ActivateTaskbarAppCommand.Execute(task);
            return;
        }

        PopulateTaskbarAppContextMenu(button, task);
        OpenContextMenu(button);
    }

    private void TaskbarApp_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is Button { DataContext: TaskbarAppItem task } button)
            PopulateTaskbarAppContextMenu(button, task);
    }

    private void PopulateTaskbarAppContextMenu(Button button, TaskbarAppItem task)
    {
        ContextMenu menu = button.ContextMenu ?? new ContextMenu();
        menu.Items.Clear();

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
        if (task.Windows.Count > 0)
            menu.Items.Add(new Separator());

        foreach (WindowReference window in task.Windows)
        {
            var windowMenu = new MenuItem
            {
                Header = string.IsNullOrWhiteSpace(window.Title) ? task.DisplayName : window.Title
            };
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

        button.ContextMenu = menu;
    }

    private void OpenContextMenu(Button button)
    {
        if (button.ContextMenu == null)
            return;

        _autoHideTimer.Stop();
        button.ContextMenu.Closed -= ButtonContextMenu_Closed;
        button.ContextMenu.Closed += ButtonContextMenu_Closed;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void VolumeButton_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        float step = e.Delta > 0 ? 0.05f : -0.05f;
        _viewModel.MasterVolume = Math.Clamp(_viewModel.MasterVolume + step, 0f, 1f);
        if (_viewModel.MasterVolume > 0 && _viewModel.IsMuted)
            _viewModel.IsMuted = false;
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
    }

    private void PowerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExpandSidebar();
        _viewModel.TogglePowerMenuCommand.Execute(null);
    }

    private void CloseOverlayPanels()
    {
        _viewModel.IsSearchOpen = false;
        _viewModel.IsCalendarOpen = false;
        _viewModel.IsFocusCenterOpen = false;
        _viewModel.IsStatusCenterOpen = false;
        _viewModel.IsSettingsOpen = false;
        _viewModel.IsPowerMenuOpen = false;
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
            CloseOverlayPanels();
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
            return;
        }

        _viewModel.MarkReplacementStopped(
            TaskbarReplacementStopReason.StartupFailure,
            error ?? "无法启用任务栏替代模式。");
        MessageBox.Show(
            error ?? "无法启用任务栏替代模式。",
            "已保留 Windows 任务栏",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void DisableTaskbarReplacement()
    {
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

                _viewModel.MarkReplacementStopped(stopped.Reason, stopped.Message);
            });
        }
        catch (InvalidOperationException)
        {
            // The window is already shutting down.
        }
    }

    private void ApplyDownloadedUpdate()
    {
        _hotZoneMonitor?.Stop();

        try
        {
            new DatabaseBackupService().PerformStartupBackup();
            _coordinator.RestoreTaskbar();
            DesktopHelper.ToggleDesktopIcons(true);
            _coordinator.Updates.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            if (!_hiddenToTray)
                _hotZoneMonitor?.Start();

            MessageBox.Show(
                $"更新包已下载，但无法启动安装：{ex.Message}\n"
                + "Windows 任务栏已经恢复，可稍后重新尝试。",
                "无法安装更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExit)
        {
            e.Cancel = true;
            _hiddenToTray = true;
            _hotZoneMonitor?.Stop();
            HideShell();
            DesktopHelper.ToggleDesktopIcons(true);
            return;
        }

        _autoHideTimer.Stop();
        _transientInteractionDepth = 0;
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
        _coordinator.Taskbar.ReplacementStopped -= Taskbar_ReplacementStopped;
        _viewModel.UpdateAvailable -= ViewModel_UpdateAvailable;
        _viewModel.Dispose();
        _coordinator.Dispose();
    }

    public void ShowFromTray()
    {
        _hiddenToTray = false;
        EnsureHotZoneMonitor();
        _hotZoneMonitor?.Start();
        ExpandSidebar();
        Activate();
    }

    public void ForceClose()
    {
        _isExit = true;
        _coordinator.RestoreTaskbar();
        MyNotifyIcon.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            PositionAtPrimaryRightEdge();
            _hotZoneMonitor?.RefreshDisplayBounds();
            _edgeIndicator?.Reposition();
        });
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
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
            handled = true;
        }

        return IntPtr.Zero;
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
            BeginDesktopFileDrag();
        }
    }

    private void Sidebar_DragLeave(object sender, DragEventArgs e)
    {
        if (!_isDesktopFileDragging)
            ScheduleAutoHide();
    }

    public void BeginDesktopFileDrag()
    {
        _isDesktopFileDragging = true;
        _viewModel.NavigateCommand.Execute("Files");
        ExpandSidebar();
    }

    public void EndDesktopFileDrag()
    {
        _isDesktopFileDragging = false;
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

        DragDrop.DoDragDrop(
            (DependencyObject)sender,
            new DataObject(typeof(TaskbarAppItem), app),
            DragDropEffects.Move);
    }

    private void TaskbarApp_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskbarAppItem))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TaskbarApp_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskbarAppItem target }
            && e.Data.GetData(typeof(TaskbarAppItem)) is TaskbarAppItem source)
        {
            _viewModel.MoveTaskbarApp(source, target);
        }

        e.Handled = true;
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Margins
        {
            internal int Left;
            internal int Right;
            internal int Top;
            internal int Bottom;

            internal Margins(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        internal static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);

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
