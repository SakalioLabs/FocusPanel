using System;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;

namespace FocusPanel.ViewModels;

public sealed class PomodoroCompletedEventArgs : EventArgs
{
    public PomodoroCompletedEventArgs(int durationMinutes)
    {
        DurationMinutes = durationMinutes;
    }

    public int DurationMinutes { get; }
}

public partial class PomodoroViewModel
    : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly PomodoroCountdown _countdown;
    private DateTime? _sessionStartedAt;
    private bool _disposed;

    [ObservableProperty]
    private string timerDisplay = "25:00";

    [ObservableProperty]
    private string elapsedDisplay = "已专注 00:00";

    [ObservableProperty]
    private string statusMessage = "准备专注 · 25 分钟";

    [ObservableProperty]
    private double progress = 100;

    [ObservableProperty]
    private int completedPomodoros;

    [ObservableProperty]
    private double totalFocusMinutes;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private int selectedDurationMinutes = 25;

    public PomodoroViewModel()
    {
        _countdown =
            new PomodoroCountdown(
                TimeSpan.FromMinutes(
                    SelectedDurationMinutes));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        SyncCountdownState();
        LoadStats();
    }

    public event EventHandler<PomodoroCompletedEventArgs>?
        SessionCompleted;

    public bool CanStart => !IsRunning;
    public bool CanPause => IsRunning;
    public bool CanChangeDuration =>
        !IsRunning
        && (!_countdown.HasElapsed
            || _countdown.IsCompleted);

    private void LoadStats()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            CompletedPomodoros =
                context.PomodoroSessions.Count(
                    session =>
                        session.Status == "Completed");
            TotalFocusMinutes =
                context.PomodoroSessions
                    .Where(
                        session =>
                            session.Status == "Completed")
                    .Select(
                        session =>
                            (double)session.DurationMinutes)
                    .DefaultIfEmpty()
                    .Sum();
        }
        catch
        {
            StatusMessage =
                "统计暂时不可用，计时仍可正常使用";
        }
    }

    private bool SaveCompletedSession(
        int durationMinutes)
    {
        try
        {
            DateTime endedAt = DateTime.Now;
            using var context = new AppDbContext();
            context.PomodoroSessions.Add(
                new PomodoroSession
                {
                    StartTime =
                        _sessionStartedAt
                        ?? endedAt.AddMinutes(
                            -durationMinutes),
                    EndTime = endedAt,
                    DurationMinutes = durationMinutes,
                    Status = "Completed"
                });
            context.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        bool completed = _countdown.Tick();
        SyncCountdownState();
        if (!completed)
            return;

        _timer.Stop();
        int durationMinutes =
            SelectedDurationMinutes;
        bool saved =
            SaveCompletedSession(durationMinutes);
        CompletedPomodoros++;
        TotalFocusMinutes += durationMinutes;
        StatusMessage = saved
            ? $"本轮 {durationMinutes} 分钟专注已完成"
            : "本轮已完成，但统计记录保存失败";
        _sessionStartedAt = null;
        CloseOverlayWindows();
        SessionCompleted?.Invoke(
            this,
            new PomodoroCompletedEventArgs(
                durationMinutes));
    }

    private void SyncCountdownState()
    {
        TimeSpan remaining =
            TimeSpan.FromSeconds(
                _countdown.RemainingSeconds);
        TimeSpan elapsed =
            TimeSpan.FromSeconds(
                _countdown.ElapsedSeconds);
        TimerDisplay =
            FormatDuration(remaining);
        ElapsedDisplay =
            $"已专注 {FormatDuration(elapsed)}";
        Progress =
            _countdown.ProgressPercent;
        IsRunning =
            _countdown.IsRunning;
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(
            nameof(CanChangeDuration));
    }

    private static string FormatDuration(
        TimeSpan value)
        => value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");

    [RelayCommand]
    private void SetDuration(string? minutesText)
    {
        if (!CanChangeDuration
            || !int.TryParse(
                minutesText,
                out int minutes)
            || minutes is < 1 or > 180)
        {
            return;
        }

        SelectedDurationMinutes = minutes;
        _countdown.Configure(
            TimeSpan.FromMinutes(minutes));
        StatusMessage =
            $"准备专注 · {minutes} 分钟";
        SyncCountdownState();
    }

    [RelayCommand]
    private void Start()
    {
        if (IsRunning)
            return;

        if (_countdown.IsCompleted)
            _countdown.Reset();
        _sessionStartedAt ??= DateTime.Now;
        _countdown.Start();
        _timer.Start();
        StatusMessage = "正在专注";
        SyncCountdownState();
        OpenOverlayWindows();
    }

    [RelayCommand]
    private void Pause()
    {
        if (!IsRunning)
            return;

        _timer.Stop();
        _countdown.Pause();
        StatusMessage = "已暂停，可随时继续";
        SyncCountdownState();
    }

    [RelayCommand]
    private void Reset()
    {
        _timer.Stop();
        _countdown.Reset();
        _sessionStartedAt = null;
        StatusMessage =
            $"准备专注 · {SelectedDurationMinutes} 分钟";
        SyncCountdownState();
        CloseOverlayWindows();
    }

    private void OpenOverlayWindows()
        => PomodoroWindowManager.OpenWindows(this);

    private static void CloseOverlayWindows()
        => PomodoroWindowManager.CloseWindows();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        CloseOverlayWindows();
    }
}
