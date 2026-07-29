using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

internal sealed record OkrWorkspaceSnapshot(
    bool IsValid,
    IReadOnlyList<OkrObjective> Objectives,
    bool IsConfigured,
    int SyncIntervalMinutes,
    DateTime? LastSyncTime,
    string? UserId)
{
    internal static OkrWorkspaceSnapshot Invalid { get; } =
        new(
            false,
            Array.Empty<OkrObjective>(),
            false,
            30,
            null,
            null);
}

internal interface IOkrWorkspaceRepository
{
    OkrWorkspaceSnapshot Load();

    void SaveDraft(OkrObjective objective);
}

internal sealed class OkrWorkspaceRepository
    : IOkrWorkspaceRepository
{
    private const string AppIdKey = "feishu_app_id";
    private const string AppSecretKey = "feishu_app_secret";
    private const string SyncIntervalKey =
        "feishu_okr_sync_interval_minutes";
    private const string LastSyncKey = "feishu_okr_last_sync";

    private readonly Func<OkrWorkspaceSnapshot> _load;
    private readonly Action<OkrObjective> _saveDraft;

    internal OkrWorkspaceRepository()
        : this(LoadCore, SaveDraftCore)
    {
    }

    internal OkrWorkspaceRepository(
        Func<OkrWorkspaceSnapshot> load,
        Action<OkrObjective>? saveDraft = null)
    {
        _load =
            load
            ?? throw new ArgumentNullException(nameof(load));
        _saveDraft =
            saveDraft
            ?? SaveDraftCore;
    }

    public OkrWorkspaceSnapshot Load()
    {
        try
        {
            OkrWorkspaceSnapshot snapshot = _load();
            return Normalize(snapshot);
        }
        catch
        {
            return OkrWorkspaceSnapshot.Invalid;
        }
    }

    public void SaveDraft(OkrObjective objective)
    {
        ArgumentNullException.ThrowIfNull(objective);
        _saveDraft(objective);
    }

    private static OkrWorkspaceSnapshot Normalize(
        OkrWorkspaceSnapshot snapshot)
    {
        if (!snapshot.IsValid)
            return OkrWorkspaceSnapshot.Invalid;

        return snapshot with
        {
            Objectives =
                snapshot.Objectives
                ?? Array.Empty<OkrObjective>(),
            SyncIntervalMinutes =
                snapshot.SyncIntervalMinutes < 1
                    ? 30
                    : snapshot.SyncIntervalMinutes
        };
    }

    private static OkrWorkspaceSnapshot LoadCore()
    {
        using var context = new AppDbContext();
        string[] keys =
        {
            AppIdKey,
            AppSecretKey,
            SyncIntervalKey,
            LastSyncKey
        };
        Dictionary<string, string> config =
            context.AppConfigs
                .AsNoTracking()
                .Where(item => keys.Contains(item.Key))
                .ToDictionary(
                    item => item.Key,
                    item => item.Value);
        List<OkrObjective> objectives =
            context.OkrObjectives
                .AsNoTracking()
                .Include(
                    objective =>
                        objective.KeyResults.Where(
                            result => !result.IsDeleted))
                .Where(objective => !objective.IsDeleted)
                .OrderByDescending(
                    objective => objective.CreatedAt)
                .ToList();

        bool configured =
            config.TryGetValue(
                AppIdKey,
                out string? appId)
            && !string.IsNullOrWhiteSpace(appId)
            && config.TryGetValue(
                AppSecretKey,
                out string? appSecret)
            && !string.IsNullOrWhiteSpace(appSecret);
        int interval =
            config.TryGetValue(
                SyncIntervalKey,
                out string? intervalText)
            && int.TryParse(
                intervalText,
                out int parsedInterval)
            && parsedInterval >= 1
                ? parsedInterval
                : 30;
        DateTime? lastSync =
            config.TryGetValue(
                LastSyncKey,
                out string? lastSyncText)
            && DateTime.TryParse(
                lastSyncText,
                out DateTime parsedLastSync)
                ? parsedLastSync
                : null;
        string? userId = objectives
            .Select(objective => objective.UserId)
            .FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value));

        return new OkrWorkspaceSnapshot(
            true,
            objectives,
            configured,
            interval,
            lastSync,
            userId);
    }

    private static void SaveDraftCore(
        OkrObjective objective)
    {
        using var context = new AppDbContext();
        context.OkrObjectives.Add(objective);
        context.SaveChanges();
    }
}
