using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace FocusPanel.Views;

public partial class EdgeIndicatorWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int SwShowNoActivate = 4;
    private const double IndicatorPhysicalWidth = 3;

    public EdgeIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyClickThroughStyles();
        Loaded += (_, _) => Reposition();
    }

    public void ShowIndicator()
    {
        if (!IsVisible)
            Show();

        Reposition();
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            NativeMethods.ShowWindow(hwnd, SwShowNoActivate);
    }

    public void HideIndicator()
    {
        if (IsVisible)
            Hide();
    }

    public void Reposition()
    {
        Forms.Screen? primary = Forms.Screen.PrimaryScreen;
        if (primary == null)
            return;

        var source = PresentationSource.FromVisual(this);
        double dipPerPixelX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dipPerPixelY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
        Width = IndicatorPhysicalWidth * dipPerPixelX;
        Left = primary.Bounds.Right * dipPerPixelX - Width;
        Top = primary.Bounds.Top * dipPerPixelY;
        Height = primary.Bounds.Height * dipPerPixelY;
    }

    private void ApplyClickThroughStyles()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        long styles = NativeMethods.GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        NativeMethods.SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(styles));
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hwnd, int command);
    }
}
