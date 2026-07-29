using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

internal sealed record OkrObjectiveWrite(
    int Id,
    string? FeishuObjectiveId,
    string? UserId,
    string Name,
    string? Note,
    double Progress,
    string? Period,
    double Weight,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    OkrSyncStatus SyncStatus)
{
    internal static OkrObjectiveWrite Capture(
        OkrObjective objective) =>
        new(
            objective.Id,
            objective.FeishuObjectiveId,
            objective.UserId,
            objective.Name,
            objective.Note,
            objective.Progress,
            objective.Period,
            objective.Weight,
            objective.CreatedAt,
            objective.UpdatedAt,
            objective.SyncStatus);
}

internal sealed record OkrKeyResultWrite(
    int Id,
    string? FeishuKrId,
    int ObjectiveId,
    string Name,
    double CurrentValue,
    double StartValue,
    double TargetValue,
    double Progress,
    double Weight,
    string Unit,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    OkrSyncStatus SyncStatus)
{
    internal static OkrKeyResultWrite Capture(
        OkrKeyResult keyResult) =>
        new(
            keyResult.Id,
            keyResult.FeishuKrId,
            keyResult.ObjectiveId,
            keyResult.Name,
            keyResult.CurrentValue,
            keyResult.StartValue,
            keyResult.TargetValue,
            keyResult.Progress,
            keyResult.Weight,
            keyResult.Unit,
            keyResult.CreatedAt,
            keyResult.UpdatedAt,
            keyResult.SyncStatus);
}

internal sealed record OkrObjectiveAggregateWrite(
    int Id,
    double Progress,
    OkrSyncStatus SyncStatus,
    DateTime UpdatedAt);

internal interface IOkrWorkspaceRepository
{
    OkrWorkspaceSnapshot Load();

    Task<int> AddObjectiveAsync(
        OkrObjectiveWrite objective);

    Task DeleteObjectiveAsync(int objectiveId);

    Task UpdateObjectiveAsync(
        OkrObjectiveWrite objective);

    Task<int> AddKeyResultAsync(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective);

    Task DeleteKeyResultAsync(
        int keyResultId,
        OkrObjectiveAggregateWrite objective);

    Task UpdateKeyResultAsync(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective);

    Task<OkrObjective> SaveDraftAsync(
        OkrObjective objective);
}

internal interface IOkrWorkspacePersistence
{
    OkrWorkspaceSnapshot Load();
    int AddObjective(OkrObjectiveWrite objective);
    void DeleteObjective(int objectiveId);
    void UpdateObjective(OkrObjectiveWrite objective);
    int AddKeyResult(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective);
    void DeleteKeyResult(
        int keyResultId,
        OkrObjectiveAggregateWrite objective);
    void UpdateKeyResult(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective);
    OkrObjective SaveDraft(OkrObjective objective);
}

internal sealed class OkrWorkspaceRepository
    : IOkrWorkspaceRepository
{
    private readonly IOkrWorkspacePersistence _persistence;
    private readonly SemaphoreSlim _operationGate =
        new(1, 1);

    internal OkrWorkspaceRepository()
        : this(new AppDbOkrWorkspacePersistence())
    {
    }

    internal OkrWorkspaceRepository(
        IOkrWorkspacePersistence persistence)
    {
        _persistence =
            persistence
            ?? throw new ArgumentNullException(
                nameof(persistence));
    }

    public OkrWorkspaceSnapshot Load()
    {
        _operationGate.Wait();
        try
        {
            return Normalize(_persistence.Load());
        }
        catch
        {
            return OkrWorkspaceSnapshot.Invalid;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<int> AddObjectiveAsync(
        OkrObjectiveWrite objective) =>
        RunAsync(() =>
            _persistence.AddObjective(objective));

    public Task DeleteObjectiveAsync(int objectiveId) =>
        RunAsync(
            () =>
            {
                _persistence.DeleteObjective(objectiveId);
                return true;
            });

    public Task UpdateObjectiveAsync(
        OkrObjectiveWrite objective) =>
        RunAsync(
            () =>
            {
                _persistence.UpdateObjective(objective);
                return true;
            });

    public Task<int> AddKeyResultAsync(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective) =>
        RunAsync(() =>
            _persistence.AddKeyResult(
                keyResult,
                objective));

    public Task DeleteKeyResultAsync(
        int keyResultId,
        OkrObjectiveAggregateWrite objective) =>
        RunAsync(
            () =>
            {
                _persistence.DeleteKeyResult(
                    keyResultId,
                    objective);
                return true;
            });

    public Task UpdateKeyResultAsync(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective) =>
        RunAsync(
            () =>
            {
                _persistence.UpdateKeyResult(
                    keyResult,
                    objective);
                return true;
            });

    public Task<OkrObjective> SaveDraftAsync(
        OkrObjective objective)
    {
        ArgumentNullException.ThrowIfNull(objective);
        OkrObjective detachedCopy =
            AppDbOkrWorkspacePersistence
                .CloneObjectiveGraph(objective);
        return RunAsync(() =>
            _persistence.SaveDraft(detachedCopy));
    }

    private async Task<T> RunAsync<T>(
        Func<T> operation)
    {
        await _operationGate
            .WaitAsync()
            .ConfigureAwait(false);
        try
        {
            return await Task.Run(operation)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
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
}

internal sealed class AppDbOkrWorkspacePersistence
    : IOkrWorkspacePersistence
{
    private const string AppIdKey = "feishu_app_id";
    private const string AppSecretKey = "feishu_app_secret";
    private const string SyncIntervalKey =
        "feishu_okr_sync_interval_minutes";
    private const string LastSyncKey =
        "feishu_okr_last_sync";

    public OkrWorkspaceSnapshot Load()
    {
        using var context = CreateContext();
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
                .Where(objective =>
                    !objective.IsDeleted
                    && objective.SyncStatus
                        != OkrSyncStatus.LocalDeleted)
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

    public int AddObjective(
        OkrObjectiveWrite objective)
    {
        using var context = CreateContext();
        OkrObjective entity = CreateObjective(objective);
        context.OkrObjectives.Add(entity);
        context.SaveChanges();
        return entity.Id;
    }

    public void DeleteObjective(int objectiveId)
    {
        using var context = CreateContext();
        OkrObjective objective =
            context.OkrObjectives.Find(objectiveId)
            ?? throw new InvalidOperationException(
                "数据库中找不到该目标。");
        if (objective.SyncStatus
                == OkrSyncStatus.LocalCreated
            || objective.FeishuObjectiveId == null)
        {
            context.OkrObjectives.Remove(objective);
        }
        else
        {
            objective.SyncStatus =
                OkrSyncStatus.LocalDeleted;
            objective.UpdatedAt = DateTime.Now;
        }
        context.SaveChanges();
    }

    public void UpdateObjective(
        OkrObjectiveWrite objective)
    {
        using var context = CreateContext();
        OkrObjective stored =
            context.OkrObjectives.Find(objective.Id)
            ?? throw new InvalidOperationException(
                "数据库中找不到该目标。");
        stored.Name = objective.Name;
        stored.Note = objective.Note;
        stored.Period = objective.Period;
        stored.Weight = objective.Weight;
        stored.UpdatedAt = objective.UpdatedAt;
        stored.SyncStatus = objective.SyncStatus;
        context.SaveChanges();
    }

    public int AddKeyResult(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective)
    {
        using var context = CreateContext();
        OkrObjective storedObjective =
            GetObjective(context, objective.Id);
        OkrKeyResult entity =
            CreateKeyResult(keyResult);
        context.Set<OkrKeyResult>().Add(entity);
        ApplyAggregate(storedObjective, objective);
        context.SaveChanges();
        return entity.Id;
    }

    public void DeleteKeyResult(
        int keyResultId,
        OkrObjectiveAggregateWrite objective)
    {
        using var context = CreateContext();
        OkrKeyResult keyResult =
            context.Set<OkrKeyResult>()
                .Find(keyResultId)
            ?? throw new InvalidOperationException(
                "数据库中找不到该关键结果。");
        if (keyResult.SyncStatus
                == OkrSyncStatus.LocalCreated
            || keyResult.FeishuKrId == null)
        {
            context.Set<OkrKeyResult>().Remove(keyResult);
        }
        else
        {
            keyResult.SyncStatus =
                OkrSyncStatus.LocalDeleted;
            keyResult.IsDeleted = true;
            keyResult.UpdatedAt = DateTime.Now;
        }
        ApplyAggregate(
            GetObjective(context, objective.Id),
            objective);
        context.SaveChanges();
    }

    public void UpdateKeyResult(
        OkrKeyResultWrite keyResult,
        OkrObjectiveAggregateWrite objective)
    {
        using var context = CreateContext();
        OkrKeyResult stored =
            context.Set<OkrKeyResult>()
                .Find(keyResult.Id)
            ?? throw new InvalidOperationException(
                "数据库中找不到该关键结果。");
        stored.Name = keyResult.Name;
        stored.CurrentValue = keyResult.CurrentValue;
        stored.StartValue = keyResult.StartValue;
        stored.TargetValue = keyResult.TargetValue;
        stored.Unit = keyResult.Unit;
        stored.Weight = keyResult.Weight;
        stored.Progress = keyResult.Progress;
        stored.UpdatedAt = keyResult.UpdatedAt;
        stored.SyncStatus = keyResult.SyncStatus;
        ApplyAggregate(
            GetObjective(context, objective.Id),
            objective);
        context.SaveChanges();
    }

    public OkrObjective SaveDraft(
        OkrObjective objective)
    {
        using var context = CreateContext();
        context.OkrObjectives.Add(objective);
        context.SaveChanges();
        return objective;
    }

    internal static OkrObjective CloneObjectiveGraph(
        OkrObjective source)
    {
        var clone = new OkrObjective
        {
            FeishuObjectiveId =
                source.FeishuObjectiveId,
            UserId = source.UserId,
            Name = source.Name,
            Note = source.Note,
            Progress = source.Progress,
            Period = source.Period,
            Weight = source.Weight,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            FeishuCreatedAt =
                source.FeishuCreatedAt,
            FeishuUpdatedAt =
                source.FeishuUpdatedAt,
            SyncStatus = source.SyncStatus,
            LastSyncedAt = source.LastSyncedAt,
            IsDeleted = source.IsDeleted
        };
        foreach (OkrKeyResult keyResult
                 in source.KeyResults)
        {
            clone.KeyResults.Add(
                new OkrKeyResult
                {
                    FeishuKrId =
                        keyResult.FeishuKrId,
                    Name = keyResult.Name,
                    CurrentValue =
                        keyResult.CurrentValue,
                    StartValue =
                        keyResult.StartValue,
                    TargetValue =
                        keyResult.TargetValue,
                    Progress = keyResult.Progress,
                    Weight = keyResult.Weight,
                    Unit = keyResult.Unit,
                    CreatedAt = keyResult.CreatedAt,
                    UpdatedAt = keyResult.UpdatedAt,
                    FeishuUpdatedAt =
                        keyResult.FeishuUpdatedAt,
                    SyncStatus =
                        keyResult.SyncStatus,
                    LastSyncedAt =
                        keyResult.LastSyncedAt,
                    IsDeleted =
                        keyResult.IsDeleted
                });
        }
        return clone;
    }

    private static AppDbContext CreateContext()
    {
        var context = new AppDbContext();
        context.EnsureSchema();
        return context;
    }

    private static OkrObjective CreateObjective(
        OkrObjectiveWrite source) =>
        new()
        {
            FeishuObjectiveId =
                source.FeishuObjectiveId,
            UserId = source.UserId,
            Name = source.Name,
            Note = source.Note,
            Progress = source.Progress,
            Period = source.Period,
            Weight = source.Weight,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            SyncStatus = source.SyncStatus
        };

    private static OkrKeyResult CreateKeyResult(
        OkrKeyResultWrite source) =>
        new()
        {
            ObjectiveId = source.ObjectiveId,
            FeishuKrId = source.FeishuKrId,
            Name = source.Name,
            CurrentValue = source.CurrentValue,
            StartValue = source.StartValue,
            TargetValue = source.TargetValue,
            Progress = source.Progress,
            Weight = source.Weight,
            Unit = source.Unit,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            SyncStatus = source.SyncStatus
        };

    private static OkrObjective GetObjective(
        AppDbContext context,
        int objectiveId) =>
        context.OkrObjectives.Find(objectiveId)
        ?? throw new InvalidOperationException(
            "数据库中找不到所属目标。");

    private static void ApplyAggregate(
        OkrObjective stored,
        OkrObjectiveAggregateWrite source)
    {
        stored.Progress = source.Progress;
        stored.SyncStatus = source.SyncStatus;
        stored.UpdatedAt = source.UpdatedAt;
    }
}
