using System;

namespace FocusPanel.Services;

internal sealed class PomodoroCountdown
{
    internal PomodoroCountdown(TimeSpan duration)
    {
        Configure(duration);
    }

    internal int TotalSeconds { get; private set; }
    internal int RemainingSeconds { get; private set; }
    internal bool IsRunning { get; private set; }
    internal bool IsCompleted => RemainingSeconds == 0;
    internal bool HasElapsed => RemainingSeconds < TotalSeconds;
    internal int ElapsedSeconds => TotalSeconds - RemainingSeconds;
    internal double ProgressPercent => TotalSeconds == 0
        ? 0
        : RemainingSeconds * 100d / TotalSeconds;

    internal void Configure(TimeSpan duration)
    {
        int seconds = checked((int)Math.Round(duration.TotalSeconds));
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "专注时长必须大于零。");

        TotalSeconds = seconds;
        RemainingSeconds = seconds;
        IsRunning = false;
    }

    internal void Start()
    {
        if (IsCompleted)
            RemainingSeconds = TotalSeconds;

        IsRunning = true;
    }

    internal void Pause()
        => IsRunning = false;

    internal bool Tick()
    {
        if (!IsRunning || RemainingSeconds <= 0)
            return false;

        RemainingSeconds--;
        if (RemainingSeconds > 0)
            return false;

        IsRunning = false;
        return true;
    }

    internal void Reset()
    {
        RemainingSeconds = TotalSeconds;
        IsRunning = false;
    }
}
