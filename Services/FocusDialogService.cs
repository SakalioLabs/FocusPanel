using System.Linq;
using System;
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
        try
        {
            return ShowCore(
                message,
                caption,
                buttons,
                image);
        }
        catch (Exception ex)
        {
            // A notification must never become more destructive than
            // the operation it describes. In particular, organizer
            // completion/error dialogs can be created while Explorer,
            // the display topology, or the shell window is changing.
            // If the custom dialog cannot be built, record the failure
            // and choose the non-destructive result instead of letting
            // the exception reach WPF's fatal dispatcher boundary.
            new CrashLogService().TryAppend(
                new InvalidOperationException(
                    $"Focus dialog '{caption}' could not be shown.",
                    ex));
            return GetSafeFallbackResult(buttons);
        }
    }

    internal static MessageBoxResult GetSafeFallbackResult(
        MessageBoxButton buttons) =>
        buttons switch
        {
            MessageBoxButton.OK =>
                MessageBoxResult.OK,
            MessageBoxButton.YesNo =>
                MessageBoxResult.No,
            _ =>
                MessageBoxResult.Cancel
        };

    private static MessageBoxResult ShowCore(
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage image)
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
                () => ShowCore(
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
