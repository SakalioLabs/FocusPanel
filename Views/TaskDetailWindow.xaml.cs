using System.Windows;
using System.Windows.Input;
using FocusPanel.Services;

namespace FocusPanel.Views;

public partial class TaskDetailWindow : Window
{
    public TaskDetailWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            WindowBackdropService.Apply(this);
    }

    private void Header_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
