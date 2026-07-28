using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class FocusDialogWindow : Window
{
    private MessageBoxButton _buttons =
        MessageBoxButton.OK;
    private MessageBoxImage _image =
        MessageBoxImage.None;

    public FocusDialogWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            WindowBackdropService.Apply(this);
        Loaded += (_, _) =>
            FindDefaultButton()?.Focus();
    }

    public MessageBoxResult Result { get; private set; } =
        MessageBoxResult.None;

    public void Configure(
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage image)
    {
        Title = string.IsNullOrWhiteSpace(caption)
            ? "FocusPanel"
            : caption;
        TitleText.Text = Title;
        MessageText.Text = message ?? string.Empty;
        AutomationProperties.SetName(
            this,
            $"{Title} 对话框");
        _buttons = buttons;
        _image = image;
        ConfigureIcon(image);
        ConfigureButtons(buttons);
    }

    private void ConfigureIcon(MessageBoxImage image)
    {
        string glyph;
        string brushKey;
        switch (image)
        {
            case MessageBoxImage.Error:
                glyph = "\uEA39";
                brushKey = "FocusDangerBrush";
                break;
            case MessageBoxImage.Warning:
                glyph = "\uE7BA";
                brushKey = "FocusWarningBrush";
                break;
            case MessageBoxImage.Question:
                glyph = "\uE897";
                brushKey = "FocusAccentBrightBrush";
                break;
            default:
                glyph = "\uE946";
                brushKey = "FocusAccentBrightBrush";
                break;
        }

        IconText.Text = glyph;
        IconText.SetResourceReference(
            ForegroundProperty,
            brushKey);
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        OkButton.Visibility = Visibility.Collapsed;
        YesButton.Visibility = Visibility.Collapsed;
        NoButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;
        OkButton.IsDefault = false;
        OkButton.IsCancel = false;
        YesButton.IsDefault = false;
        NoButton.IsDefault = false;
        NoButton.IsCancel = false;
        CancelButton.IsCancel = false;
        YesButton.Style = (Style)FindResource(
            _image == MessageBoxImage.Warning
                ? "FocusDangerButton"
                : "FocusPrimaryButton");

        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                CancelButton.IsCancel = true;
                break;
            case MessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                if (_image == MessageBoxImage.Warning)
                    NoButton.IsDefault = true;
                else
                    YesButton.IsDefault = true;
                NoButton.IsCancel = true;
                break;
            case MessageBoxButton.YesNoCancel:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                CancelButton.IsCancel = true;
                break;
            default:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                OkButton.IsCancel = true;
                break;
        }
    }

    private Button? FindDefaultButton() =>
        new[]
            {
                OkButton,
                YesButton,
                NoButton,
                CancelButton
            }
            .FirstOrDefault(
                button =>
                    button.Visibility
                        == Visibility.Visible
                    && button.IsDefault);

    private void Complete(MessageBoxResult result)
    {
        Result = result;
        Close();
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (Result == MessageBoxResult.None)
        {
            Result = _buttons switch
            {
                MessageBoxButton.OK =>
                    MessageBoxResult.OK,
                MessageBoxButton.YesNo =>
                    MessageBoxResult.No,
                _ => MessageBoxResult.Cancel
            };
        }

        base.OnClosing(e);
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Close();

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Complete(MessageBoxResult.OK);

    private void YesButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Complete(MessageBoxResult.Yes);

    private void NoButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Complete(MessageBoxResult.No);

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Complete(MessageBoxResult.Cancel);
}
