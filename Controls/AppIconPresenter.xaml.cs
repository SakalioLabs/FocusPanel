using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FocusPanel.Services;

namespace FocusPanel.Controls;

public partial class AppIconPresenter : UserControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(ImageSource),
            typeof(AppIconPresenter));

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(
            nameof(DisplayName),
            typeof(string),
            typeof(AppIconPresenter),
            new PropertyMetadata(
                null,
                OnDisplayNameChanged));

    public AppIconPresenter()
    {
        InitializeComponent();
    }

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? DisplayName
    {
        get => (string?)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    private static void OnDisplayNameChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var presenter = (AppIconPresenter)dependencyObject;
        presenter.FallbackTextBlock.Text =
            AppIconFallback.GetText(args.NewValue as string);
    }
}
