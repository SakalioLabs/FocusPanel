using System.Windows;
using System.Windows.Input;
using FocusPanel.Services;
using FocusPanel.ViewModels;

namespace FocusPanel.Views
{
    public partial class PomodoroFloatingWindow : Window
    {
        public PomodoroFloatingWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) =>
                WindowBackdropService.Apply(this);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
