using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class EdgeIndicatorWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int SwShowNoActivate = 4;
    private const double IndicatorPhysicalWidth = 3;
    private static readonly Duration
        StartingPulseDuration =
        new(TimeSpan.FromMilliseconds(720));
    private bool _isStarting;

    public EdgeIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyClickThroughStyles();
        Loaded += (_, _) => Reposition();
    }

    public void ShowIndicator()
    {
        StopStartingAnimation();
        ShowWithoutActivation();
    }

    internal void ShowStartingIndicator()
    {
        _isStarting = true;
        if (!SystemParameters
                .ClientAreaAnimation
            || SystemParameters.HighContrast)
        {
            IndicatorSurface.Opacity = 0.72;
        }
        else
        {
            IndicatorSurface.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(
                    0.28,
                    0.92,
                    StartingPulseDuration)
                {
                    AutoReverse = true,
                    RepeatBehavior =
                        RepeatBehavior.Forever,
                    EasingFunction =
                        new SineEase
                        {
                            EasingMode =
                                EasingMode.EaseInOut
                        }
                },
                HandoffBehavior
                    .SnapshotAndReplace);
        }
        ShowWithoutActivation();
    }

    internal bool IsStarting =>
        _isStarting;

    internal string TargetValue { get; set; } =
        ShellDisplayTarget.OutermostRightValue;

    private void ShowWithoutActivation()
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
        StopStartingAnimation();
        if (IsVisible)
            Hide();
    }

    public void Reposition()
    {
        Rectangle targetBounds =
            ShellDisplayTarget.GetBounds(
                TargetValue);
        if (targetBounds.Width <= 0
            || targetBounds.Height <= 0)
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        uint dpi =
            ShellWindowPlacement.GetTargetDpi(
                targetBounds,
                hwnd);
        double scale = dpi / 96.0;
        PhysicalWindowBounds bounds =
            ShellWindowPlacement.CalculateIndicator(
                targetBounds,
                (int)IndicatorPhysicalWidth);
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;
        ShellWindowPlacement.Apply(hwnd, bounds);
    }

    private void StopStartingAnimation()
    {
        if (!_isStarting)
            return;

        _isStarting = false;
        IndicatorSurface.BeginAnimation(
            OpacityProperty,
            null);
        IndicatorSurface.Opacity = 1;
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
