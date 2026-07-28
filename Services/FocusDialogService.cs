using System.Linq;
using System.Windows;
using FocusPanel.Views;

namespace FocusPanel.Services;

public static class FocusDialogService
{
    public static MessageBoxResult Show(
        string message,
        string caption = "FocusPanel",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
    {
        Application? application = Application.Current;
        if (application == null)
        {
            return MessageBox.Show(
                message,
                caption,
                buttons,
                image);
        }

        if (!application.Dispatcher.CheckAccess())
        {
            return application.Dispatcher.Invoke(
                () => Show(
                    message,
                    caption,
                    buttons,
                    image));
        }

        var dialog = new FocusDialogWindow();
        dialog.Configure(
            message,
            caption,
            buttons,
            image);

        Window? owner = application.Windows
            .OfType<Window>()
            .Where(window =>
                window != dialog
                && window.IsVisible)
            .OrderByDescending(window => window.IsActive)
            .FirstOrDefault();
        if (owner != null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation =
                WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation =
                WindowStartupLocation.CenterScreen;
        }

        IFocusDialogInteractionHost? shell =
            application.Windows
                .OfType<MainWindow>()
                .FirstOrDefault(window =>
                    window.IsVisible);
        using FocusDialogInteractionLease interaction =
            FocusDialogInteractionLease.Enter(shell);
        dialog.ShowDialog();
        return dialog.Result;
    }
}
