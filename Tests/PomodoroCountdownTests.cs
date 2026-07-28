using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PomodoroCountdownTests
{
    [Fact]
    public void NewCountdownStartsFullAndStopped()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(10));

        Assert.Equal(10, countdown.TotalSeconds);
        Assert.Equal(10, countdown.RemainingSeconds);
        Assert.Equal(100, countdown.ProgressPercent);
        Assert.False(countdown.IsRunning);
        Assert.False(countdown.HasElapsed);
    }

    [Fact]
    public void RunningTickUpdatesRemainingElapsedAndProgress()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(10));

        countdown.Start();
        bool completed = countdown.Tick();

        Assert.False(completed);
        Assert.Equal(9, countdown.RemainingSeconds);
        Assert.Equal(1, countdown.ElapsedSeconds);
        Assert.Equal(90, countdown.ProgressPercent);
        Assert.True(countdown.IsRunning);
    }

    [Fact]
    public void TickWhilePausedDoesNotConsumeTime()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(3));
        countdown.Start();
        countdown.Pause();

        Assert.False(countdown.Tick());
        Assert.Equal(3, countdown.RemainingSeconds);
        Assert.False(countdown.IsRunning);
    }

    [Fact]
    public void CompletionIsRaisedOnlyOnTransitionToZero()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(1));
        countdown.Start();

        Assert.True(countdown.Tick());
        Assert.False(countdown.Tick());
        Assert.True(countdown.IsCompleted);
        Assert.False(countdown.IsRunning);
        Assert.Equal(0, countdown.ProgressPercent);
    }

    [Fact]
    public void StartAfterCompletionBeginsFreshRound()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(1));
        countdown.Start();
        Assert.True(countdown.Tick());

        countdown.Start();

        Assert.True(countdown.IsRunning);
        Assert.Equal(1, countdown.RemainingSeconds);
        Assert.Equal(100, countdown.ProgressPercent);
    }

    [Fact]
    public void ResetRestoresConfiguredDuration()
    {
        var countdown =
            new PomodoroCountdown(
                TimeSpan.FromSeconds(20));
        countdown.Start();
        countdown.Tick();

        countdown.Reset();

        Assert.Equal(20, countdown.RemainingSeconds);
        Assert.False(countdown.HasElapsed);
        Assert.False(countdown.IsRunning);
    }

    [Fact]
    public void ConfigureRejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PomodoroCountdown(
                TimeSpan.Zero));
    }
}
