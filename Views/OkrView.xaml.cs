using System.Windows.Controls;

namespace FocusPanel.Views;

public partial class OkrView : UserControl
{
    public OkrView()
    {
        InitializeComponent();
        SettingsAppSecretBox.PasswordChanged += OnSettingsPasswordChanged;
    }

    private void OnSettingsPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.OkrViewModel vm)
        {
            vm.SettingsAppSecret = SettingsAppSecretBox.Password;
        }
    }
}
