using System.Windows.Threading;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PomodoroQuickStartTests
{
    [Fact]
    public void IdleSession_StartsRequestedDuration()
    {
        var overlay = new RecordingOverlayHost();
        using var viewModel = CreateViewModel(
            overlay);

        PomodoroQuickStartResult result =
            viewModel.TryStartQuickSession(42);

        Assert.Equal(
            PomodoroQuickStartResult.Started,
            result);
        Assert.True(viewModel.IsRunning);
        Assert.Equal(
            42,
            viewModel.SelectedDurationMinutes);
        Assert.Equal(
            "42:00",
            viewModel.TimerDisplay);
        Assert.Equal(1, overlay.OpenCount);
    }

    [Fact]
    public void RunningSession_IsNeverResetByNewCommand()
    {
        var overlay = new RecordingOverlayHost();
        using var viewModel = CreateViewModel(
            overlay);
        Assert.Equal(
            PomodoroQuickStartResult.Started,
            viewModel.TryStartQuickSession(25));

        PomodoroQuickStartResult result =
            viewModel.TryStartQuickSession(60);

        Assert.Equal(
            PomodoroQuickStartResult
                .AlreadyRunning,
            result);
        Assert.Equal(
            25,
            viewModel.SelectedDurationMinutes);
        Assert.True(viewModel.IsRunning);
        Assert.Equal(1, overlay.OpenCount);
    }

    [Fact]
    public void PausedSession_IsNeverReconfigured()
    {
        var overlay = new RecordingOverlayHost();
        using var viewModel = CreateViewModel(
            overlay);
        viewModel.TryStartQuickSession(25);
        viewModel.PauseCommand.Execute(null);

        PomodoroQuickStartResult result =
            viewModel.TryStartQuickSession(60);

        Assert.Equal(
            PomodoroQuickStartResult
                .SessionInProgress,
            result);
        Assert.Equal(
            25,
            viewModel.SelectedDurationMinutes);
        Assert.False(viewModel.IsRunning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(181)]
    public void InvalidDuration_DoesNotOpenOverlay(
        int minutes)
    {
        var overlay = new RecordingOverlayHost();
        using var viewModel = CreateViewModel(
            overlay);

        Assert.Equal(
            PomodoroQuickStartResult
                .InvalidDuration,
            viewModel.TryStartQuickSession(
                minutes));
        Assert.False(viewModel.IsRunning);
        Assert.Equal(0, overlay.OpenCount);
    }

    [Fact]
    public void DisposedTimer_CannotBeRestarted()
    {
        var overlay = new RecordingOverlayHost();
        var viewModel = CreateViewModel(
            overlay);
        viewModel.Dispose();

        Assert.Equal(
            PomodoroQuickStartResult.Unavailable,
            viewModel.TryStartQuickSession(25));
        Assert.False(viewModel.IsRunning);
        Assert.Equal(0, overlay.OpenCount);
        Assert.Equal(1, overlay.CloseCount);
    }

    private static PomodoroViewModel
        CreateViewModel(
            IPomodoroOverlayHost overlay) =>
        new(
            new PomodoroSessionRepository(
                () => new PomodoroStatsSnapshot(
                    true,
                    0,
                    0),
                _ => { }),
            Dispatcher.CurrentDispatcher,
            overlay);

    private sealed class RecordingOverlayHost
        : IPomodoroOverlayHost
    {
        internal int OpenCount { get; private set; }
        internal int CloseCount { get; private set; }

        public void Open(
            PomodoroViewModel viewModel)
        {
            _ = viewModel;
            OpenCount++;
        }

        public void Close()
        {
            CloseCount++;
        }
    }
}
