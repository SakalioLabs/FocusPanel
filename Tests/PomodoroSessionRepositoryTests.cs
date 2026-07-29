using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PomodoroSessionRepositoryTests
{
    [Fact]
    public void LoadStats_NormalizesInvalidCountsAndMinutes()
    {
        var repository =
            new PomodoroSessionRepository(
                () => new PomodoroStatsSnapshot(
                    true,
                    -3,
                    -25),
                _ => { });

        PomodoroStatsSnapshot snapshot =
            repository.LoadStats();

        Assert.True(snapshot.IsValid);
        Assert.Equal(0, snapshot.CompletedSessions);
        Assert.Equal(0, snapshot.TotalFocusMinutes);
    }

    [Fact]
    public void LoadStatsFailure_ReturnsInvalidSnapshot()
    {
        var repository =
            new PomodoroSessionRepository(
                () =>
                    throw new InvalidOperationException(
                        "database is busy"),
                _ => { });

        Assert.Equal(
            PomodoroStatsSnapshot.Invalid,
            repository.LoadStats());
    }

    [Fact]
    public void Save_ForwardsCompleteSession()
    {
        CompletedPomodoroSession? saved = null;
        var repository =
            new PomodoroSessionRepository(
                () => PomodoroStatsSnapshot.Invalid,
                session => saved = session);
        var session = new CompletedPomodoroSession(
            new DateTime(2026, 7, 29, 9, 0, 0),
            new DateTime(2026, 7, 29, 9, 25, 0),
            25);

        repository.Save(session);

        Assert.Same(session, saved);
    }

    [Fact]
    public void Save_RejectsNonPositiveDuration()
    {
        var repository =
            new PomodoroSessionRepository(
                () => PomodoroStatsSnapshot.Invalid,
                _ => { });

        Assert.Throws<
            ArgumentOutOfRangeException>(
            () => repository.Save(
                new CompletedPomodoroSession(
                    DateTime.Now,
                    DateTime.Now,
                    0)));
    }
}
