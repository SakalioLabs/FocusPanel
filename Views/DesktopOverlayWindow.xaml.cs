using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.ViewModels;
using Forms = System.Windows.Forms;

namespace FocusPanel.Views;

public partial class DesktopOverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int DWMWA_CLOAKED = 14;
    private Point _dragStartPoint;
    private Point _dragOffset;
    private DesktopFile? _manualDragFile;
    private FrameworkElement? _manualDragElement;
    private bool _isManualDragging;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public DesktopOverlayWindow()
    {
        InitializeComponent();
        var viewModel = new DesktopOverlayViewModel();
        DataContext = viewModel;
        viewModel.Files.CollectionChanged += DesktopFiles_CollectionChanged;

        StateChanged += DesktopOverlayWindow_StateChanged;
        Loaded += (_, _) =>
        {
            ApplyWindowStyles();
            PositionOnDesktop();
            SyncNativeDesktopIcons();
        };
        Closed += (_, _) => DesktopHelper.ToggleDesktopIcons(true);
    }

    private async void DesktopOverlayWindow_StateChanged(object? sender, EventArgs e)
    {
        SyncNativeDesktopIcons();

        if (WindowState != WindowState.Minimized) return;

        await Task.Delay(350);
        if (WindowState == WindowState.Minimized && IsVisible)
            ShowOnDesktop();
    }

    public void RefreshOverlay()
    {
        if (DataContext is DesktopOverlayViewModel vm)
            vm.Refresh();

        Dispatcher.BeginInvoke(SyncNativeDesktopIcons);
    }

    public void ShowOnDesktop()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        if (!IsVisible)
            Show();

        Visibility = Visibility.Visible;
        KeepAboveShowDesktop();
        SyncNativeDesktopIcons();
    }

    public void HideFromApps()
    {
        if (IsVisible)
            Hide();

        Topmost = false;
        SyncNativeDesktopIcons();
    }

    private void DesktopFiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(SyncNativeDesktopIcons);
    }

    private void SyncNativeDesktopIcons()
    {
        bool overlayHasIcons = IsVisible
            && WindowState != WindowState.Minimized
            && !IsCloaked()
            && DataContext is DesktopOverlayViewModel vm
            && vm.Files.Count > 0;

        DesktopHelper.ToggleDesktopIcons(!overlayHasIcons);
    }

    private bool IsCloaked()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return true;

        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0;
    }

    private void KeepAboveShowDesktop()
    {
        Topmost = true;
    }

    private void PositionOnDesktop()
    {
        var bounds = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.AllScreens[0].WorkingArea;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    private void ApplyWindowStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int styles = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, styles | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private void FileIcon_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not DesktopFile file) return;

        _dragStartPoint = e.GetPosition(this);
        _dragOffset = e.GetPosition(element);
        _manualDragFile = file;
        _manualDragElement = element;
        _isManualDragging = false;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void DesktopCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (FindDesktopFileAncestor(e.OriginalSource as DependencyObject) != null) return;

        if (DataContext is DesktopOverlayViewModel vm
            && vm.ToggleDesktopIconVisibilityCommand.CanExecute(null))
        {
            vm.ToggleDesktopIconVisibilityCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static FrameworkElement? FindDesktopFileAncestor(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is FrameworkElement element && element.DataContext is DesktopFile)
                return element;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void FileIcon_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_manualDragElement == null || _manualDragFile == null) return;

        var current = e.GetPosition(this);
        if (!_isManualDragging
            && Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _isManualDragging = true;

        var desktopPosition = e.GetPosition(DesktopCanvas);

        if (desktopPosition.X >= Math.Max(0, ActualWidth - 90))
        {
            StartPanelDrag(_manualDragElement, _manualDragFile);
            e.Handled = true;
            return;
        }

        _manualDragFile.DesktopX = Clamp(desktopPosition.X - _dragOffset.X, 0, Math.Max(0, DesktopCanvas.ActualWidth - _manualDragElement.ActualWidth));
        _manualDragFile.DesktopY = Clamp(desktopPosition.Y - _dragOffset.Y, 0, Math.Max(0, DesktopCanvas.ActualHeight - _manualDragElement.ActualHeight));
        e.Handled = true;
    }

    private void FileIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SaveManualDragPosition();
        e.Handled = true;
    }

    private void StartPanelDrag(FrameworkElement element, DesktopFile file)
    {
        element.ReleaseMouseCapture();
        ClearManualDragState();

        var data = new DataObject();
        data.SetData(typeof(DesktopFile), file);

        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.BeginDesktopFileDrag();

        try
        {
            DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
        }
        finally
        {
            if (Application.Current.MainWindow is MainWindow owner)
                owner.EndDesktopFileDrag();
        }
    }

    private void SaveManualDragPosition()
    {
        if (_manualDragElement != null)
            _manualDragElement.ReleaseMouseCapture();

        if (_isManualDragging
            && _manualDragFile != null
            && DataContext is DesktopOverlayViewModel vm)
        {
            var request = new DesktopDropRequest
            {
                File = _manualDragFile,
                X = _manualDragFile.DesktopX + (_manualDragElement?.ActualWidth ?? vm.DesktopIconWidth) / 2,
                Y = _manualDragFile.DesktopY + (_manualDragElement?.ActualHeight ?? vm.DesktopIconHeight) / 2
            };

            if (vm.RestoreOrMoveToDesktopCommand.CanExecute(request))
                vm.RestoreOrMoveToDesktopCommand.Execute(request);
        }

        ClearManualDragState();
    }

    private void ClearManualDragState()
    {
        _manualDragFile = null;
        _manualDragElement = null;
        _isManualDragging = false;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private void Desktop_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(DesktopFile)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Desktop_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not DesktopOverlayViewModel vm) return;
        if (e.Data.GetData(typeof(DesktopFile)) is not DesktopFile file) return;

        var position = e.GetPosition(DesktopCanvas);
        var request = new DesktopDropRequest
        {
            File = file,
            X = position.X,
            Y = position.Y
        };

        if (vm.RestoreOrMoveToDesktopCommand.CanExecute(request))
            vm.RestoreOrMoveToDesktopCommand.Execute(request);

        e.Handled = true;
    }
}
