using System;
using System.Windows;
using FocusPanel.ViewModels;
using FocusPanel.Views;

namespace FocusPanel.Services
{
    internal interface IPomodoroOverlayHost
    {
        void Open(PomodoroViewModel viewModel);
        void Close();
    }

    internal sealed class PomodoroOverlayHost
        : IPomodoroOverlayHost
    {
        public void Open(
            PomodoroViewModel viewModel) =>
            PomodoroWindowManager
                .OpenWindows(viewModel);

        public void Close() =>
            PomodoroWindowManager
                .CloseWindows();
    }

    public static class PomodoroWindowManager
    {
        private static PomodoroFloatingWindow? _floatingWindow;

        public static void OpenWindows(PomodoroViewModel viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_floatingWindow == null)
                {
                    _floatingWindow = new PomodoroFloatingWindow();
                    _floatingWindow.DataContext = viewModel;
                    _floatingWindow.Closed += (s, e) => _floatingWindow = null;
                    _floatingWindow.Show();
                }
                else
                {
                    _floatingWindow.DataContext = viewModel;
                    if (!_floatingWindow.IsVisible) _floatingWindow.Show();
                }
            });
        }

        public static void CloseWindows()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _floatingWindow?.Close();
                _floatingWindow = null;
            });
        }
    }
}
