using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly Dispatcher _uiDispatcher;
    private readonly IPomodoroSessionRepository
        _sessionRepository;
    private readonly IPomodoroOverlayHost
        _overlayHost;
    private readonly CoalescingBackgroundRefresh<
        PendingPomodoroStats> _statsRefresh;
    private readonly CoalescingAsyncSaveQueue<
        PendingPomodoroSave> _sessionSaveQueue;
    private DateTime? _sessionStartedAt;
    private long _statsRevision;
    private long _sessionUiRevision;
    private bool _disposed;
    private Task? _disposeTask;

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
        : this(
            new PomodoroSessionRepository(),
            Dispatcher.CurrentDispatcher)
    {
    }

    internal PomodoroViewModel(
        IPomodoroSessionRepository
            sessionRepository,
        Dispatcher dispatcher,
        IPomodoroOverlayHost?
            overlayHost = null)
    {
        _sessionRepository = sessionRepository
            ?? throw new ArgumentNullException(
                nameof(sessionRepository));
        _uiDispatcher = dispatcher
            ?? throw new ArgumentNullException(
                nameof(dispatcher));
        _overlayHost =
            overlayHost
            ?? new PomodoroOverlayHost();
        _countdown =
            new PomodoroCountdown(
                TimeSpan.FromMinutes(
                    SelectedDurationMinutes));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _statsRefresh =
            new CoalescingBackgroundRefresh<
                PendingPomodoroStats>(
                CaptureStats,
                ApplyStatsAsync);
        _sessionSaveQueue =
            new CoalescingAsyncSaveQueue<
                PendingPomodoroSave>(
                SaveSessionAsync,
                TimeSpan.Zero);
        _sessionSaveQueue.ItemSaved +=
            OnSessionSaved;
        _sessionSaveQueue.ItemSaveFailed +=
            OnSessionSaveFailed;
        SyncCountdownState();
        _statsRefresh.Request();
    }

    public event EventHandler<PomodoroCompletedEventArgs>?
        SessionCompleted;
    public event EventHandler? SessionPersisted;

    public bool CanStart => !IsRunning;
    public bool CanPause => IsRunning;
    public bool CanChangeDuration =>
        !IsRunning
        && (!_countdown.HasElapsed
            || _countdown.IsCompleted);

    internal PomodoroQuickStartResult
        TryStartQuickSession(
            int durationMinutes)
    {
        if (_disposed)
        {
            return PomodoroQuickStartResult
                .Unavailable;
        }

        if (durationMinutes
            is < 1 or > 180)
        {
            return PomodoroQuickStartResult
                .InvalidDuration;
        }

        if (IsRunning)
        {
            return PomodoroQuickStartResult
                .AlreadyRunning;
        }

        if (_sessionStartedAt.HasValue
            || !CanChangeDuration)
        {
            return PomodoroQuickStartResult
                .SessionInProgress;
        }

        SetDuration(
            durationMinutes.ToString(
                System.Globalization
                    .CultureInfo
                    .InvariantCulture));
        Start();
        return PomodoroQuickStartResult
            .Started;
    }

    private PendingPomodoroStats CaptureStats()
    {
        long revision = Volatile.Read(
            ref _statsRevision);
        return new PendingPomodoroStats(
            _sessionRepository.LoadStats(),
            revision);
    }

    private async Task ApplyStatsAsync(
        PendingPomodoroStats pending,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (_disposed
                    || cancellationToken
                        .IsCancellationRequested)
                {
                    return;
                }

                long currentRevision =
                    Volatile.Read(
                        ref _statsRevision);
                if (pending.Revision
                    != currentRevision)
                {
                    return;
                }
                if (!pending.Snapshot.IsValid)
                {
                    ReportInitialStatsUnavailable();
                    return;
                }
                if (!PomodoroStatsApplyPolicy
                    .ShouldApply(
                        pending.Snapshot,
                        pending.Revision,
                        currentRevision))
                {
                    return;
                }

                CompletedPomodoros =
                    pending.Snapshot
                        .CompletedSessions;
                TotalFocusMinutes =
                    pending.Snapshot
                        .TotalFocusMinutes;
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private Task SaveSessionAsync(
        PendingPomodoroSave pending) =>
        Task.Run(
            () => _sessionRepository.Save(
                pending.Session));

    private void OnSessionSaved(
        PendingPomodoroSave pending)
    {
        DispatchSaveResult(
            () =>
            {
                if (PomodoroSaveResultPolicy
                    .ShouldUpdateCompletionMessage(
                        pending.Revision,
                        Volatile.Read(
                            ref _sessionUiRevision),
                        IsRunning))
                {
                    StatusMessage =
                        $"本轮 {pending.Session.DurationMinutes} 分钟专注已完成";
                }

                SessionPersisted?.Invoke(
                    this,
                    EventArgs.Empty);
            });
    }

    private void OnSessionSaveFailed(
        PendingPomodoroSave pending,
        Exception error)
    {
        _ = error;
        DispatchSaveResult(
            () =>
            {
                StatusMessage = IsRunning
                    ? "正在专注 · 上一轮统计记录保存失败"
                    : "本轮已完成，但统计记录保存失败";
            });
    }

    private void DispatchSaveResult(
        Action apply)
    {
        if (_disposed
            || _uiDispatcher.HasShutdownStarted
            || _uiDispatcher.HasShutdownFinished)
        {
            return;
        }

        _uiDispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!_disposed)
                    apply();
            }),
            DispatcherPriority.Background);
    }

    private void ReportInitialStatsUnavailable()
    {
        if (!IsRunning)
        {
            StatusMessage =
                "统计暂时不可用，计时仍可正常使用";
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
        DateTime endedAt = DateTime.Now;
        Interlocked.Increment(
            ref _statsRevision);
        long uiRevision = Interlocked.Increment(
            ref _sessionUiRevision);
        var pending =
            new PendingPomodoroSave(
                new CompletedPomodoroSession(
                    _sessionStartedAt
                    ?? endedAt.AddMinutes(
                        -durationMinutes),
                    endedAt,
                    durationMinutes),
                uiRevision);
        bool saveQueued =
            _sessionSaveQueue.Enqueue(
                pending);
        CompletedPomodoros++;
        TotalFocusMinutes += durationMinutes;
        StatusMessage = saveQueued
            ? $"本轮 {durationMinutes} 分钟专注已完成 · 正在保存统计"
            : "本轮已完成，但统计记录未能进入保存队列";
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

        Interlocked.Increment(
            ref _sessionUiRevision);
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

        Interlocked.Increment(
            ref _sessionUiRevision);
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
        Interlocked.Increment(
            ref _sessionUiRevision);
        _timer.Stop();
        _countdown.Reset();
        _sessionStartedAt = null;
        StatusMessage =
            $"准备专注 · {SelectedDurationMinutes} 分钟";
        SyncCountdownState();
        CloseOverlayWindows();
    }

    private void OpenOverlayWindows()
        => _overlayHost.Open(this);

    private void CloseOverlayWindows()
        => _overlayHost.Close();

    internal Task DisposeAsync()
    {
        if (_disposeTask != null)
            return _disposeTask;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _statsRefresh.Dispose();
        _sessionSaveQueue.ItemSaved -=
            OnSessionSaved;
        _sessionSaveQueue.ItemSaveFailed -=
            OnSessionSaveFailed;
        CloseOverlayWindows();
        _disposeTask =
            _sessionSaveQueue.CompleteAsync();
        return _disposeTask;
    }

    public void Dispose() =>
        DisposeAsync()
            .GetAwaiter()
            .GetResult();

    private readonly record struct
        PendingPomodoroStats(
            PomodoroStatsSnapshot Snapshot,
            long Revision);

    private sealed record PendingPomodoroSave(
        CompletedPomodoroSession Session,
        long Revision);
}

internal enum PomodoroQuickStartResult
{
    Started,
    AlreadyRunning,
    SessionInProgress,
    InvalidDuration,
    Unavailable
}
