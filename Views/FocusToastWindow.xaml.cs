using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class FocusToastWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    public FocusToastWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            ApplyNoActivateStyle();
            WindowBackdropService.Apply(this);
        };
    }

    public event EventHandler? DismissRequested;
    public event EventHandler? ActionRequested;

    public void Configure(FocusToastNotification notification)
    {
        TitleText.Text = notification.Title;
        MessageText.Text = notification.Message;
        IconText.Text = notification.Glyph;
        ActionButton.Content = notification.ActionLabel;
        ActionButton.Visibility =
            string.IsNullOrWhiteSpace(notification.ActionLabel)
                ? Visibility.Collapsed
                : Visibility.Visible;
        AutomationProperties.SetName(
            this,
            $"{notification.Title}，{notification.Message}");

        string brushKey = notification.Kind switch
        {
            FocusToastKind.Success => "FocusAccentBrightBrush",
            FocusToastKind.Warning => "FocusWarningBrush",
            _ => "FocusAccentBrightBrush"
        };
        IconText.SetResourceReference(
            ForegroundProperty,
            brushKey);
    }

    private void ApplyNoActivateStyle()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        long styles = NativeMethods
            .GetWindowLongPtr(hwnd, GwlExStyle)
            .ToInt64();
        styles |= WsExToolWindow | WsExNoActivate;
        NativeMethods.SetWindowLongPtr(
            hwnd,
            GwlExStyle,
            new IntPtr(styles));
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e) =>
        DismissRequested?.Invoke(this, EventArgs.Empty);

    private void ActionButton_Click(
        object sender,
        RoutedEventArgs e) =>
        ActionRequested?.Invoke(this, EventArgs.Empty);

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(
            IntPtr hwnd,
            int index);

        [DllImport(
            "user32.dll",
            EntryPoint = "SetWindowLongPtrW")]
        internal static extern IntPtr SetWindowLongPtr(
            IntPtr hwnd,
            int index,
            IntPtr value);
    }
}
