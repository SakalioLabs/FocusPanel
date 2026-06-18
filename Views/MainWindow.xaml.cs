using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using FocusPanel.ViewModels;
using FocusPanel.Services;
using Microsoft.Win32;

namespace FocusPanel.Views
{
    public partial class MainWindow : Window
    {
        private bool _isExit = false;
        private Screen _currentScreen = null!;

        // The panel is a right-edge drawer; keep the host window square and let XAML round only the exposed left edge.
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;

        // Foreground window detection
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private IntPtr _winEventHook;
        private WinEventDelegate _winEventDelegate;
        private bool _hiddenToTray;
        private DesktopOverlayWindow? _desktopOverlay;
        private bool _isDesktopFileDragging;
        private readonly DispatcherTimer _foregroundGuardTimer;
        private bool _foregroundMonitorStarted;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            viewModel.RequestClose += () => this.ForceClose();
            DataContext = viewModel;

            MyNotifyIcon.Icon = SystemIcons.Application;

            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowState = WindowState.Normal;

            // Initial size based on mouse-position screen (refined in Loaded)
            var initScreen = GetScreenFromMouse();
            Height = initScreen.WorkingArea.Height * 0.85;
            Width = 80;
            PositionAtRightEdge(initScreen);

            ShowInTaskbar = false;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Deactivated += MainWindow_Deactivated;
            LocationChanged += MainWindow_LocationChanged;
            _foregroundGuardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _foregroundGuardTimer.Tick += (_, _) => UpdateVisibilityForForeground();
            StartForegroundMonitor();
            Dispatcher.BeginInvoke(() =>
            {
                EnsureDesktopOverlay();
                UpdateVisibilityForForeground();
            }, DispatcherPriority.ApplicationIdle);

            SystemEvents.DisplaySettingsChanged += (s, e) =>
            {
                _currentScreen = null!;
                RefreshScreenAndReposition();
                _desktopOverlay?.RefreshOverlay();
            };
        }

        private Screen GetScreenFromMouse()
        {
            var mousePos = System.Windows.Forms.Control.MousePosition;
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Bounds.Contains(mousePos.X, mousePos.Y))
                    return screen;
            }
            return Screen.PrimaryScreen!;
        }

        private Screen GetCurrentScreen()
        {
            if (_currentScreen != null)
                return _currentScreen;

            var hwnd = GetWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                _currentScreen = Screen.FromHandle(hwnd);
                return _currentScreen;
            }

            return GetScreenFromMouse();
        }

        private IntPtr GetWindowHandle()
        {
            try { return new System.Windows.Interop.WindowInteropHelper(this).Handle; }
            catch { return IntPtr.Zero; }
        }

        private void RefreshScreenAndReposition()
        {
            var hwnd = GetWindowHandle();
            if (hwnd != IntPtr.Zero)
                _currentScreen = Screen.FromHandle(hwnd);
            else
                _currentScreen = GetScreenFromMouse();
            PositionAtRightEdge(_currentScreen);
        }

        private void PositionAtRightEdge(Screen screen)
        {
            Left = screen.WorkingArea.Right - Width;
            Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;
        }

        private double GetRightEdgeTarget()
        {
            var screen = GetCurrentScreen();
            return screen.WorkingArea.Right;
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            double expectedLeft = GetRightEdgeTarget() - ActualWidth;
            if (Math.Abs(Left - expectedLeft) > 10)
            {
                Left = expectedLeft;
            }
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get accurate screen from window handle and reposition
            var hwnd = GetWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                _currentScreen = Screen.FromHandle(hwnd);
                PositionAtRightEdge(_currentScreen);

                int cornerPref = DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
            }

            Visibility = Visibility.Collapsed;
            Topmost = false;
        }

        private void StartForegroundMonitor()
        {
            if (_foregroundMonitorStarted) return;

            _winEventDelegate = new WinEventDelegate(WinEventProc);
            _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            _foregroundGuardTimer.Start();
            _foregroundMonitorStarted = true;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }

            _foregroundGuardTimer.Stop();

            if (_isExit)
            {
                FileOrganizerService.OverlayRefreshRequested -= RefreshDesktopOverlay;
                _desktopOverlay?.Close();
                _desktopOverlay = null;
            }

            if (!_isExit)
            {
                e.Cancel = true;
                _hiddenToTray = true;
                this.Hide();
            }
        }

        public void ShowFromTray()
        {
            _hiddenToTray = false;
            Show();
            WindowState = WindowState.Normal;
            UpdateVisibilityForForeground();
        }

        public void ForceClose()
        {
            _isExit = true;
            if (MyNotifyIcon != null) MyNotifyIcon.Dispose();
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void EnsureDesktopOverlay()
        {
            if (_desktopOverlay != null) return;

            _desktopOverlay = new DesktopOverlayWindow();
            FileOrganizerService.OverlayRefreshRequested += RefreshDesktopOverlay;
        }

        private void RefreshDesktopOverlay()
        {
            Dispatcher.BeginInvoke(() => _desktopOverlay?.RefreshOverlay());
        }

        private void Sidebar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            double rightEdge = GetRightEdgeTarget();

            SidebarBorder.Width = 800;
            Width = 800;
            Left = rightEdge - 800;
            HeaderGrid.Opacity = 1;
            ContentArea.Opacity = 1;
        }

        private void Sidebar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (SidebarBorder.IsKeyboardFocusWithin) return;
            CollapseSidebar();
        }

        private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
        {
            CollapseSidebar();
        }

        public void CollapseSidebar()
        {
            if (_isDesktopFileDragging) return;

            double rightEdge = GetRightEdgeTarget();

            SidebarBorder.Width = 80;
            Width = 80;
            Left = rightEdge - 80;
            HeaderGrid.Opacity = 0;
            ContentArea.Opacity = 0;
        }

        private void Sidebar_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            Sidebar_MouseEnter(sender, null!);
        }

        private void Sidebar_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (_isDesktopFileDragging) return;

            var pos = e.GetPosition(SidebarBorder);
            if (pos.X < 0 || pos.X >= SidebarBorder.ActualWidth || pos.Y < 0 || pos.Y >= SidebarBorder.ActualHeight)
            {
                CollapseSidebar();
            }
        }

        // --- Desktop-only visibility ---

        private int _pendingUpdates;

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (Interlocked.Increment(ref _pendingUpdates) > 1)
            {
                Interlocked.Decrement(ref _pendingUpdates);
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _pendingUpdates, 0);
                UpdateVisibilityForForeground();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private void UpdateVisibilityForForeground()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
            {
                ShowOnDesktop();
                return;
            }

            if (fg == GetWindowHandle())
            {
                ShowOnDesktop();
                return;
            }

            GetWindowThreadProcessId(fg, out uint foregroundProcessId);
            if (foregroundProcessId == Environment.ProcessId)
            {
                ShowOnDesktop();
                return;
            }

            var sb = new StringBuilder(256);
            GetClassName(fg, sb, 256);
            string cls = sb.ToString();

            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd")
            {
                ShowOnDesktop();
            }
            else
            {
                HideFromApps();
            }
        }

        private void ShowOnDesktop()
        {
            if (_hiddenToTray) return;

            _desktopOverlay?.ShowOnDesktop();

            if (Visibility == Visibility.Visible) return;

            Topmost = false;
            Visibility = Visibility.Visible;
            CollapseSidebar();
        }

        private void HideFromApps()
        {
            _desktopOverlay?.HideFromApps();

            if (Visibility != Visibility.Visible) return;

            Topmost = false;
            Visibility = Visibility.Collapsed;
        }

        public void BeginDesktopFileDrag()
        {
            _isDesktopFileDragging = true;
            if (DataContext is MainViewModel vm && vm.NavigateCommand.CanExecute("Files"))
                vm.NavigateCommand.Execute("Files");

            _hiddenToTray = false;
            Visibility = Visibility.Visible;
            Show();
            Topmost = false;
            ExpandSidebar();
        }

        public void EndDesktopFileDrag()
        {
            _isDesktopFileDragging = false;
            UpdateVisibilityForForeground();
        }

        private void ExpandSidebar()
        {
            double rightEdge = GetRightEdgeTarget();

            SidebarBorder.Width = 800;
            Width = 800;
            Left = rightEdge - 800;
            HeaderGrid.Opacity = 1;
            ContentArea.Opacity = 1;
        }
    }
}
