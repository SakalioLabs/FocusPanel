using System.Windows.Controls;
using System.Windows.Threading;

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

    private void ManageObjective_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            new System.Action(
                () =>
                {
                    if (ObjectiveEditor.IsVisible)
                        ObjectiveEditor.BringIntoView();
                }),
            DispatcherPriority.Background);
    }
}
