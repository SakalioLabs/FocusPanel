using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FocusPanel.Models;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class TaskbarWindowPreviewWindow :
    Window
{
    private const int MaximumPreviewCount = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int WmMouseActivate = 0x0021;
    private const int MouseActivateNoActivate = 3;
    private const int PreviewGapPhysical = 12;

    private readonly DwmThumbnailSession
        _thumbnailSession = new();
    private IReadOnlyList<WindowReference>
        _allWindows =
            Array.Empty<WindowReference>();
    private IReadOnlyList<WindowReference>
        _windows =
            Array.Empty<WindowReference>();
    private string _displayName =
        string.Empty;
    private HwndSource? _source;
    private bool _disposed;

    public TaskbarWindowPreviewWindow()
    {
        InitializeComponent();
        SourceInitialized +=
            TaskbarWindowPreviewWindow_SourceInitialized;
        Closed += (_, _) => DisposeNativeResources();
    }

    public event Action<WindowReference>?
        ActivateRequested;
    public event Action<WindowReference>?
        CloseRequested;

    internal void Configure(
        TaskbarAppItem task)
    {
        _displayName =
            task.DisplayName;
        _allWindows =
            task.Windows
                .Select(window =>
                    string.IsNullOrWhiteSpace(
                        window.Title)
                        ? window with
                        {
                            Title =
                                _displayName
                        }
                        : window)
                .ToArray();
        AppTitleText.Text =
            _displayName;
        WindowCountText.Text =
            $"{task.WindowCount} 个运行窗口";
        ApplyPreviewLimit(
            MaximumPreviewCount);
    }

    private void ApplyPreviewLimit(
        int maximumCount)
    {
        _windows =
            _allWindows
                .Take(maximumCount)
                .ToArray();
        WindowItems.ItemsSource =
            _windows;

        int remaining =
            _allWindows.Count
            - _windows.Count;
        RemainingText.Text =
            remaining > 0
                ? $"另有 {remaining} 个窗口；左键应用图标可打开完整文字列表。"
                : "滚轮可在这些窗口间快速循环。";
        RemainingText.Visibility =
            Visibility.Visible;
    }

    internal bool TryShowAt(
        Window owner,
        Rectangle displayBounds,
        int anchorLeftPhysical,
        int anchorCenterYPhysical)
    {
        if (_windows.Count == 0
            || _disposed)
        {
            return false;
        }

        uint targetDpi =
            ShellWindowPlacement
                .GetTargetDpi(
                    displayBounds,
                    IntPtr.Zero);
        double availableHeightDip =
            displayBounds.Height
            / (targetDpi / 96.0);
        ApplyPreviewLimit(
            DwmThumbnailLayout
                .GetVisiblePreviewCount(
                    _allWindows.Count,
                    availableHeightDip));
        if (_windows.Count == 0)
            return false;

        Owner = owner;
        Show();
        UpdateLayout();
        PositionAtAnchor(
            displayBounds,
            anchorLeftPhysical,
            anchorCenterYPhysical);
        UpdateLayout();

        if (!RegisterThumbnails())
        {
            Close();
            return false;
        }

        return true;
    }

    private void
        TaskbarWindowPreviewWindow_SourceInitialized(
            object? sender,
            EventArgs e)
    {
        IntPtr hwnd =
            new WindowInteropHelper(this)
                .Handle;
        long styles = NativeMethods
            .GetWindowLongPtr(
                hwnd,
                GwlExStyle)
            .ToInt64();
        styles |=
            WsExToolWindow
            | WsExNoActivate;
        NativeMethods.SetWindowLongPtr(
            hwnd,
            GwlExStyle,
            new IntPtr(styles));
        _source =
            HwndSource.FromHwnd(hwnd);
        _source?.AddHook(
            WindowMessageHook);
        WindowBackdropService.Apply(this);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message
            == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(
                MouseActivateNoActivate);
        }

        return IntPtr.Zero;
    }

    private void PositionAtAnchor(
        Rectangle displayBounds,
        int anchorLeftPhysical,
        int anchorCenterYPhysical)
    {
        IntPtr hwnd =
            new WindowInteropHelper(this)
                .Handle;
        if (hwnd == IntPtr.Zero
            || !NativeMethods.GetWindowRect(
                hwnd,
                out NativeRect bounds))
        {
            return;
        }

        int width =
            Math.Max(
                1,
                bounds.Right
                - bounds.Left);
        int height =
            Math.Max(
                1,
                bounds.Bottom
                - bounds.Top);
        int left = Math.Max(
            displayBounds.Left
                + PreviewGapPhysical,
            anchorLeftPhysical
                - width
                - PreviewGapPhysical);
        int top = Math.Clamp(
            anchorCenterYPhysical
                - height / 2,
            displayBounds.Top
                + PreviewGapPhysical,
            Math.Max(
                displayBounds.Top
                    + PreviewGapPhysical,
                displayBounds.Bottom
                    - height
                    - PreviewGapPhysical));
        ShellWindowPlacement.Apply(
            hwnd,
            new PhysicalWindowBounds(
                left,
                top,
                width,
                height));
    }

    private bool RegisterThumbnails()
    {
        IntPtr hwnd =
            new WindowInteropHelper(this)
                .Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        var clientOrigin =
            new NativePoint();
        if (!NativeMethods.ClientToScreen(
                hwnd,
                ref clientOrigin))
        {
            return false;
        }

        int registered = 0;
        foreach (WindowReference window
                 in _windows)
        {
            if (WindowItems
                    .ItemContainerGenerator
                    .ContainerFromItem(window)
                is not DependencyObject
                    container)
            {
                continue;
            }

            FrameworkElement? surface =
                FindVisualChild(
                    container,
                    "ThumbnailSurface");
            FrameworkElement? fallbackContent =
                FindVisualChild(
                    container,
                    "FallbackContent");
            TextBlock? fallbackStatus =
                FindVisualChild(
                    container,
                    "FallbackStatusText")
                as TextBlock;
            if (surface == null
                || surface.ActualWidth <= 0
                || surface.ActualHeight <= 0)
            {
                continue;
            }

            System.Windows.Point topLeft =
                surface.PointToScreen(
                    new System.Windows.Point(
                        0,
                        0));
            System.Windows.Point bottomRight =
                surface.PointToScreen(
                    new System.Windows.Point(
                        surface.ActualWidth,
                        surface.ActualHeight));
            var available =
                new DwmThumbnailRect(
                    (int)Math.Round(
                        topLeft.X)
                    - clientOrigin.X,
                    (int)Math.Round(
                        topLeft.Y)
                    - clientOrigin.Y,
                    (int)Math.Round(
                        bottomRight.X)
                    - clientOrigin.X,
                    (int)Math.Round(
                        bottomRight.Y)
                    - clientOrigin.Y);
            bool added =
                _thumbnailSession.TryAdd(
                    hwnd,
                    window.Handle,
                    available);
            if (added)
            {
                if (fallbackContent != null)
                {
                    fallbackContent.Visibility =
                        Visibility.Collapsed;
                }
                registered++;
            }
            else if (fallbackStatus != null)
            {
                fallbackStatus.Text =
                    "此窗口不允许实时预览";
            }
        }

        return registered > 0;
    }

    private static FrameworkElement?
        FindVisualChild(
            DependencyObject parent,
            string name)
    {
        int count =
            VisualTreeHelper
                .GetChildrenCount(parent);
        for (int index = 0;
             index < count;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child
                    is FrameworkElement
                    {
                        Name: var childName
                    } element
                && string.Equals(
                    childName,
                    name,
                    StringComparison.Ordinal))
            {
                return element;
            }

            FrameworkElement? nested =
                FindVisualChild(
                    child,
                    name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void
        ThumbnailSurface_PreviewMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
    {
        if (sender
                is FrameworkElement
                {
                    DataContext:
                        WindowReference window
                })
        {
            ActivateRequested?.Invoke(
                window);
            Close();
            e.Handled = true;
        }
    }

    private void CloseWindowButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender
                is FrameworkElement
                {
                    DataContext:
                        WindowReference window
                })
        {
            CloseRequested?.Invoke(
                window);
            Close();
            e.Handled = true;
        }
    }

    private void DisposeNativeResources()
    {
        if (_disposed)
            return;

        _disposed = true;
        _source?.RemoveHook(
            WindowMessageHook);
        _source = null;
        _thumbnailSession.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            EntryPoint =
                "GetWindowLongPtrW")]
        internal static extern IntPtr
            GetWindowLongPtr(
                IntPtr hwnd,
                int index);

        [DllImport(
            "user32.dll",
            EntryPoint =
                "SetWindowLongPtrW")]
        internal static extern IntPtr
            SetWindowLongPtr(
                IntPtr hwnd,
                int index,
                IntPtr value);

        [DllImport("user32.dll")]
        [return:
            MarshalAs(
                UnmanagedType.Bool)]
        internal static extern bool
            GetWindowRect(
                IntPtr hwnd,
                out NativeRect rect);

        [DllImport("user32.dll")]
        [return:
            MarshalAs(
                UnmanagedType.Bool)]
        internal static extern bool
            ClientToScreen(
                IntPtr hwnd,
                ref NativePoint point);
    }
}
