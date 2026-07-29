using System;
using System.Linq;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public readonly record struct PomodoroStatsSnapshot(
    bool IsValid,
    int CompletedSessions,
    double TotalFocusMinutes)
{
    public static PomodoroStatsSnapshot Invalid { get; } =
        new(false, 0, 0);
}

public sealed record CompletedPomodoroSession(
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationMinutes);

internal interface IPomodoroSessionRepository
{
    PomodoroStatsSnapshot LoadStats();
    void Save(CompletedPomodoroSession session);
}

internal sealed class PomodoroSessionRepository :
    IPomodoroSessionRepository
{
    private readonly Func<PomodoroStatsSnapshot>
        _loadStats;
    private readonly Action<CompletedPomodoroSession>
        _save;

    internal PomodoroSessionRepository()
        : this(
            LoadStatsFromDatabase,
            SaveToDatabase)
    {
    }

    internal PomodoroSessionRepository(
        Func<PomodoroStatsSnapshot> loadStats,
        Action<CompletedPomodoroSession> save)
    {
        _loadStats = loadStats
            ?? throw new ArgumentNullException(
                nameof(loadStats));
        _save = save
            ?? throw new ArgumentNullException(
                nameof(save));
    }

    public PomodoroStatsSnapshot LoadStats()
    {
        try
        {
            PomodoroStatsSnapshot snapshot =
                _loadStats();
            return snapshot.IsValid
                ? new PomodoroStatsSnapshot(
                    true,
                    Math.Max(
                        0,
                        snapshot.CompletedSessions),
                    Math.Max(
                        0,
                        snapshot.TotalFocusMinutes))
                : PomodoroStatsSnapshot.Invalid;
        }
        catch
        {
            return PomodoroStatsSnapshot.Invalid;
        }
    }

    public void Save(
        CompletedPomodoroSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.DurationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(session),
                "Focus duration must be positive.");
        }

        _save(session);
    }

    private static PomodoroStatsSnapshot
        LoadStatsFromDatabase()
    {
        using var context = new AppDbContext();
        int completed = context.PomodoroSessions
            .AsNoTracking()
            .Count(session =>
                session.Status == "Completed");
        double minutes = context.PomodoroSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == "Completed")
            .Select(session =>
                (double)session.DurationMinutes)
            .DefaultIfEmpty()
            .Sum();
        return new PomodoroStatsSnapshot(
            true,
            completed,
            minutes);
    }

    private static void SaveToDatabase(
        CompletedPomodoroSession session)
    {
        using var context = new AppDbContext();
        context.PomodoroSessions.Add(
            new PomodoroSession
            {
                StartTime = session.StartedAt,
                EndTime = session.EndedAt,
                DurationMinutes =
                    session.DurationMinutes,
                Status = "Completed"
            });
        context.SaveChanges();
    }
}
