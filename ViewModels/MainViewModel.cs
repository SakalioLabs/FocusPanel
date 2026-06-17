using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Services;
using FocusPanel.Views;
using System.Windows;

namespace FocusPanel.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "FocusPanel";

    [ObservableProperty]
    private object currentViewModel;

    [ObservableProperty]
    private DateTime currentTime;

    private PomodoroViewModel _pomodoroViewModel;
    private FileOrganizerViewModel _fileOrganizerViewModel;
    private OkrViewModel _okrViewModel;

    public MainViewModel()
    {
        CurrentTime = DateTime.Now;
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Interval = System.TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) => CurrentTime = DateTime.Now;
        timer.Start();

        _fileOrganizerViewModel = new FileOrganizerViewModel();
        CurrentViewModel = _fileOrganizerViewModel;
        // Enable auto-startup by default
        AutoStartupService.SetStartup(true);
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        switch (destination)
        {
            case "Tasks":
                CurrentViewModel = new TasksViewModel();
                break;
            case "Pomodoro":
                if (_pomodoroViewModel == null) _pomodoroViewModel = new PomodoroViewModel();
                CurrentViewModel = _pomodoroViewModel;
                break;
            case "Files":
                if (_fileOrganizerViewModel == null) _fileOrganizerViewModel = new FileOrganizerViewModel();
                CurrentViewModel = _fileOrganizerViewModel;
                break;
            case "OKR":
                if (_okrViewModel == null) _okrViewModel = new OkrViewModel();
                CurrentViewModel = _okrViewModel;
                break;
            case "AI":
                CurrentViewModel = new AIAssistantViewModel();
                break;
        }
    }

    public event System.Action RequestClose;

    [RelayCommand]
    private void CloseApp()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void MinimizeApp()
    {
        // 确保窗口保持正常状态，不最小化
        // 窗口应该始终保持在桌面上
        var window = Application.Current.MainWindow;
        if (window != null)
        {
            // 使用Dispatcher来确保在UI线程执行
            window.Dispatcher.Invoke(() =>
            {
                window.WindowState = WindowState.Normal;

                // 调用折叠侧边栏的方法
                if (window is MainWindow mainWindow)
                {
                    mainWindow.CollapseSidebar();
                }
            });
        }
    }

    [RelayCommand]
    private void ShowWindow()
    {
        var window = Application.Current.MainWindow;
        if (window is MainWindow mainWindow)
        {
            mainWindow.ShowFromTray();
        }
        else if (window != null)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        var result = MessageBox.Show(
            "Are you sure you want to restore the database from the latest backup?\n" +
            "This will recover ALL data (Tasks, Pomodoro, Files, etc.).\n" +
            "The application will restart immediately.",
            "Global Database Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                if (exePath.EndsWith(".dll"))
                {
                    exePath = exePath.Replace(".dll", ".exe");
                }
                System.Diagnostics.Process.Start(exePath, "--restore");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Failed to restart: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
