using System;
using System.Security;
using System.Windows;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class WifiCredentialWindow : Window
{
    private SecureString? _acceptedPassword;

    public WifiCredentialWindow(string networkName)
    {
        InitializeComponent();
        NetworkNameText.Text = networkName;
        SourceInitialized += (_, _) =>
            WindowBackdropService.Apply(this);
        Loaded += (_, _) =>
            PasswordInput.Focus();
    }

    public bool Accepted { get; private set; }

    public SecureString TakePassword()
    {
        SecureString password =
            _acceptedPassword
            ?? throw new InvalidOperationException(
                "No Wi-Fi password was accepted.");
        _acceptedPassword = null;
        return password;
    }

    protected override void OnClosed(EventArgs e)
    {
        PasswordInput.Clear();
        if (!Accepted)
        {
            _acceptedPassword?.Dispose();
            _acceptedPassword = null;
        }
        base.OnClosed(e);
    }

    private void PasswordInput_PasswordChanged(
        object sender,
        RoutedEventArgs e)
    {
        using SecureString current =
            PasswordInput.SecurePassword;
        int length = current.Length;
        ConnectButton.IsEnabled =
            length is >= 8 and <= 63;
        PasswordHintText.Text = length == 0
            ? "输入 8–63 个字符；密码只交给 Windows，本应用不保存"
            : length is < 8 or > 63
                ? "密码长度应为 8–63 个字符"
                : "密码长度有效；按 Enter 连接";
    }

    private void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _acceptedPassword?.Dispose();
        using SecureString current =
            PasswordInput.SecurePassword;
        _acceptedPassword = current.Copy();
        _acceptedPassword.MakeReadOnly();
        Accepted = true;
        PasswordInput.Clear();
        DialogResult = true;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Accepted = false;
        PasswordInput.Clear();
        DialogResult = false;
    }
}
