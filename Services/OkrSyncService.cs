using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public class OkrSyncService : IDisposable
{
    private readonly FeishuAuthService _authService;
    private readonly FeishuOkrApiService _apiService;
    private readonly object _syncLock = new();
    private bool _isSyncing;
    private System.Timers.Timer? _timer;

    /// <summary>
    /// Fires when sync completes (for UI status updates).
    /// </summary>
    public event Action<OkrSyncResult>? SyncCompleted;

    /// <summary>
    /// Fires progress messages during sync.
    /// </summary>
    public event Action<string>? ProgressChanged;

    public bool IsConfigured => _authService.IsConfigured;

    public OkrSyncService()
    {
        _authService = new FeishuAuthService();
        _apiService = new FeishuOkrApiService(_authService);
    }

    // --- Sync interval config ---
    public int GetSyncIntervalMinutes()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var config = context.AppConfigs.Find("feishu_okr_sync_interval_minutes");
            if (config != null && int.TryParse(config.Value, out var interval) && interval >= 1)
                return interval;
        }
        catch { }
        return 30;
    }

    public void SetSyncIntervalMinutes(int minutes)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        var config = context.AppConfigs.Find("feishu_okr_sync_interval_minutes");
        if (config == null)
        {
            context.AppConfigs.Add(new AppConfig { Key = "feishu_okr_sync_interval_minutes", Value = minutes.ToString() });
        }
        else
        {
            config.Value = minutes.ToString();
        }
        context.SaveChanges();

        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromMinutes(minutes).TotalMilliseconds;
        }
    }

    public DateTime? GetLastSyncTime()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var config = context.AppConfigs.Find("feishu_okr_last_sync");
            if (config != null && DateTime.TryParse(config.Value, out var dt))
                return dt;
        }
        catch { }
        return null;
    }

    private void SetLastSyncTime(DateTime dt)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        var config = context.AppConfigs.Find("feishu_okr_last_sync");
        if (config == null)
        {
            context.AppConfigs.Add(new AppConfig { Key = "feishu_okr_last_sync", Value = dt.ToString("o") });
        }
        else
        {
            config.Value = dt.ToString("o");
        }
        context.SaveChanges();
    }

    // --- Main Sync ---

    public async Task<OkrSyncResult> SyncAsync()
    {
        if (!_authService.IsConfigured)
        {
            var notConfigured = new OkrSyncResult { Success = false, Message = "Feishu credentials not configured." };
            SyncCompleted?.Invoke(notConfigured);
            return notConfigured;
        }

        if (!TryStartSync())
        {
            var inProgress = new OkrSyncResult { Success = false, Message = "Sync already in progress." };
            SyncCompleted?.Invoke(inProgress);
            return inProgress;
        }

        var result = new OkrSyncResult();
        try
        {
            Report("Getting user ID...");
            var userId = await _apiService.GetCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                // Try to get from stored objectives
                userId = GetStoredUserId();
            }
            if (string.IsNullOrEmpty(userId))
            {
                result.Message = "Could not determine Feishu user ID. Please ensure the app has OKR permissions.";
                result.Errors.Add(result.Message);
                Report(result.Message);
                SyncCompleted?.Invoke(result);
                return result;
            }

            using var logContext = new AppDbContext();
            logContext.EnsureSchema();

            // 1. PULL: Fetch from Feishu
            Report("Pulling objectives from Feishu...");
            var serverObjectives = await _apiService.GetObjectivesAsync(userId);
            await PullObjectives(serverObjectives, userId, result, logContext);

            // 2. PUSH: Push local changes
            Report("Pushing local changes...");
            await PushChanges(result, logContext);

            result.Success = result.Errors.Count == 0;
            result.Message = result.Errors.Count == 0
                ? $"Synced: {result.ObjectivesPulled} objectives, {result.KeyResultsPulled} KRs pulled; {result.ObjectivesPushed} objectives, {result.KeyResultsPushed} KRs pushed."
                : $"Sync completed with {result.Errors.Count} errors.";

            SetLastSyncTime(DateTime.Now);
            Report(result.Message);
        }
        catch (FeishuApiException ex)
        {
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
            Report($"API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
            Report($"Sync error: {ex.Message}");
        }
        finally
        {
            EndSync();
        }

        SyncCompleted?.Invoke(result);
        return result;
    }

    private string? GetStoredUserId()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            return context.OkrObjectives
                .Where(o => o.FeishuObjectiveId != null && o.UserId != null)
                .Select(o => o.UserId)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private async Task PullObjectives(
        List<FeishuObjectiveDto> serverObjectives,
        string userId,
        OkrSyncResult result,
        AppDbContext logContext)
    {
        using var db = new AppDbContext();
        db.EnsureSchema();

        var localObjectives = db.OkrObjectives
            .Include(o => o.KeyResults)
            .Where(o => !o.IsDeleted)
            .ToList();

        var serverIdSet = new HashSet<string>(serverObjectives.Select(o => o.Id));

        foreach (var serverObj in serverObjectives)
        {
            var localObj = localObjectives.FirstOrDefault(lo => lo.FeishuObjectiveId == serverObj.Id);

            if (localObj == null)
            {
                // New from server — add locally
                localObj = CreateLocalObjective(serverObj, userId);
                db.OkrObjectives.Add(localObj);
                result.ObjectivesPulled++;
                LogSync(logContext, "PullCreated", "Objective", null, serverObj.Id, $"Created: {serverObj.Name}");
            }
            else
            {
                if (localObj.SyncStatus == OkrSyncStatus.LocalModified || localObj.SyncStatus == OkrSyncStatus.LocalCreated)
                {
                    // Conflict: server wins (overwrite local)
                    result.Conflicts++;
                    LogSync(logContext, "Conflict", "Objective", localObj.Id, serverObj.Id, $"Conflict resolved (server wins): {serverObj.Name}");
                }
                UpdateLocalObjective(localObj, serverObj, userId);
                result.ObjectivesPulled++;
            }

            // Sync KRs
            await PullKeyResults(localObj, serverObj.KeyResults, result, logContext, db);
        }

        // Handle server-deleted items
        foreach (var localObj in localObjectives.Where(lo =>
            lo.FeishuObjectiveId != null && !serverIdSet.Contains(lo.FeishuObjectiveId) && lo.SyncStatus == OkrSyncStatus.Synced))
        {
            localObj.IsDeleted = true;
            LogSync(logContext, "PullDeleted", "Objective", localObj.Id, localObj.FeishuObjectiveId, $"Deleted by server: {localObj.Name}");
        }

        await TrySaveChangesAsync(db);
    }

    private async Task PullKeyResults(
        OkrObjective localObj,
        List<FeishuKrDto> serverKRs,
        OkrSyncResult result,
        AppDbContext logContext,
        AppDbContext db)
    {
        var serverKrIds = new HashSet<string>(serverKRs.Select(k => k.Id));

        foreach (var serverKr in serverKRs)
        {
            var localKr = localObj.KeyResults.FirstOrDefault(k => k.FeishuKrId == serverKr.Id);

            if (localKr == null)
            {
                localKr = CreateLocalKr(serverKr, localObj);
                db.Set<OkrKeyResult>().Add(localKr);
                result.KeyResultsPulled++;
                LogSync(logContext, "PullCreated", "KeyResult", null, serverKr.Id, $"Created KR: {serverKr.Name}");
            }
            else
            {
                if (localKr.SyncStatus == OkrSyncStatus.LocalModified)
                {
                    result.Conflicts++;
                    LogSync(logContext, "Conflict", "KeyResult", localKr.Id, serverKr.Id, $"KR conflict resolved (server wins): {serverKr.Name}");
                }
                UpdateLocalKr(localKr, serverKr);
                result.KeyResultsPulled++;
            }
        }

        // Handle server-deleted KRs
        foreach (var localKr in localObj.KeyResults.Where(k =>
            k.FeishuKrId != null && !serverKrIds.Contains(k.FeishuKrId) && k.SyncStatus == OkrSyncStatus.Synced))
        {
            localKr.IsDeleted = true;
            LogSync(logContext, "PullDeleted", "KeyResult", localKr.Id, localKr.FeishuKrId, $"KR deleted by server: {localKr.Name}");
        }

        await TrySaveChangesAsync(db);
    }

    private async Task PushChanges(OkrSyncResult result, AppDbContext logContext)
    {
        using var db = new AppDbContext();
        db.EnsureSchema();

        // Push objectives
        var pushObjectives = db.OkrObjectives
            .Include(o => o.KeyResults)
            .Where(o => o.SyncStatus != OkrSyncStatus.Synced && !o.IsDeleted)
            .ToList();

        foreach (var obj in pushObjectives)
        {
            try
            {
                if (obj.SyncStatus == OkrSyncStatus.LocalCreated)
                {
                    var request = new CreateObjectiveRequest
                    {
                        UserId = obj.UserId ?? string.Empty,
                        Name = obj.Name,
                        Note = obj.Note,
                        Period = obj.Period,
                        Weight = obj.Weight,
                        KeyResults = obj.KeyResults
                            .Where(k => !k.IsDeleted)
                            .Select(k => new CreateKrRequest
                            {
                                Name = k.Name,
                                StartValue = k.StartValue,
                                TargetValue = k.TargetValue,
                                Unit = k.Unit,
                                Weight = k.Weight
                            })
                            .ToList()
                    };
                    var response = await _apiService.CreateObjectiveAsync(request);
                    obj.FeishuObjectiveId = response.Id;
                    obj.SyncStatus = OkrSyncStatus.Synced;
                    obj.LastSyncedAt = DateTime.Now;
                    obj.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(response.UpdatedAt);
                    ApplyReturnedKeyResults(obj, response, result);
                    result.ObjectivesPushed++;
                    LogSync(logContext, "PushCreated", "Objective", obj.Id, response.Id, $"Pushed: {obj.Name}");
                }
                else if (obj.SyncStatus == OkrSyncStatus.LocalModified && obj.FeishuObjectiveId != null)
                {
                    var request = new UpdateObjectiveRequest
                    {
                        Name = obj.Name,
                        Note = obj.Note,
                        Period = obj.Period,
                        Weight = obj.Weight
                    };
                    var response = await _apiService.UpdateObjectiveAsync(obj.FeishuObjectiveId, request);
                    obj.SyncStatus = OkrSyncStatus.Synced;
                    obj.LastSyncedAt = DateTime.Now;
                    obj.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(response.UpdatedAt);
                    result.ObjectivesPushed++;
                    LogSync(logContext, "PushUpdated", "Objective", obj.Id, obj.FeishuObjectiveId, $"Pushed update: {obj.Name}");
                }
                else if (obj.SyncStatus == OkrSyncStatus.LocalDeleted && obj.FeishuObjectiveId != null)
                {
                    await _apiService.DeleteObjectiveAsync(obj.FeishuObjectiveId);
                    obj.IsDeleted = true;
                    obj.SyncStatus = OkrSyncStatus.Synced;
                    obj.LastSyncedAt = DateTime.Now;
                    result.ObjectivesPushed++;
                    LogSync(logContext, "PushDeleted", "Objective", obj.Id, obj.FeishuObjectiveId, $"Deleted: {obj.Name}");
                }
            }
            catch (FeishuApiException ex)
            {
                result.Errors.Add($"Failed to push objective '{obj.Name}': {ex.Message}");
                LogSync(logContext, "Error", "Objective", obj.Id, obj.FeishuObjectiveId, $"Push error: {ex.Message}");
            }
        }

        // Push KRs
        var pushKRs = db.Set<OkrKeyResult>()
            .Include(k => k.Objective)
            .Where(k => k.SyncStatus != OkrSyncStatus.Synced)
            .ToList();

        foreach (var kr in pushKRs)
        {
            try
            {
                if (kr.SyncStatus == OkrSyncStatus.LocalModified && kr.FeishuKrId != null)
                {
                    var request = new UpdateKrRequest
                    {
                        Name = kr.Name,
                        CurrentValue = kr.CurrentValue,
                        StartValue = kr.StartValue,
                        TargetValue = kr.TargetValue,
                        Unit = kr.Unit,
                        Weight = kr.Weight
                    };
                    var response = await _apiService.UpdateKeyResultAsync(kr.FeishuKrId, request);
                    kr.SyncStatus = OkrSyncStatus.Synced;
                    kr.LastSyncedAt = DateTime.Now;
                    kr.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(response.UpdatedAt);
                    result.KeyResultsPushed++;
                    LogSync(logContext, "PushUpdated", "KeyResult", kr.Id, kr.FeishuKrId, $"Pushed KR: {kr.Name}");
                }
                // LocalCreated KRs that exist under an objective that was created on server:
                // wait until the parent objective has a FeishuId, then create KRs
                else if (kr.SyncStatus == OkrSyncStatus.LocalCreated
                    && kr.Objective?.FeishuObjectiveId != null)
                {
                    result.Errors.Add($"Key result '{kr.Name}' is still pending: Feishu KR create endpoint is not implemented.");
                    LogSync(logContext, "Pending", "KeyResult", kr.Id, kr.FeishuKrId, $"Pending KR create: {kr.Name}");
                }
                else if (kr.SyncStatus == OkrSyncStatus.LocalDeleted && kr.FeishuKrId != null)
                {
                    result.Errors.Add($"Key result '{kr.Name}' is still pending: Feishu KR delete endpoint is not implemented.");
                    LogSync(logContext, "Pending", "KeyResult", kr.Id, kr.FeishuKrId, $"Pending KR delete: {kr.Name}");
                }
            }
            catch (FeishuApiException ex)
            {
                result.Errors.Add($"Failed to push KR '{kr.Name}': {ex.Message}");
                LogSync(logContext, "Error", "KeyResult", kr.Id, kr.FeishuKrId, $"Push error: {ex.Message}");
            }
        }

        await TrySaveChangesAsync(db);
    }

    private static void ApplyReturnedKeyResults(OkrObjective localObjective, FeishuObjectiveDto response, OkrSyncResult result)
    {
        if (response.KeyResults.Count == 0) return;

        var pending = localObjective.KeyResults
            .Where(k => k.SyncStatus == OkrSyncStatus.LocalCreated && !k.IsDeleted)
            .ToList();

        foreach (var kr in pending)
        {
            var returnedKr = response.KeyResults.FirstOrDefault(r =>
                string.Equals(r.Name, kr.Name, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(localObjective.KeyResults
                    .FirstOrDefault(existing => existing.FeishuKrId == r.Id)?.FeishuKrId));

            if (returnedKr == null) continue;

            kr.FeishuKrId = returnedKr.Id;
            kr.CurrentValue = returnedKr.CurrentValue;
            kr.StartValue = returnedKr.StartValue;
            kr.TargetValue = returnedKr.TargetValue;
            kr.Progress = returnedKr.Progress;
            kr.Weight = returnedKr.Weight;
            kr.Unit = returnedKr.Unit ?? kr.Unit;
            kr.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(returnedKr.UpdatedAt);
            kr.SyncStatus = OkrSyncStatus.Synced;
            kr.LastSyncedAt = DateTime.Now;
            result.KeyResultsPushed++;
        }
    }

    private static async Task TrySaveChangesAsync(AppDbContext db)
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch
            {
                if (i < maxRetries - 1)
                    await Task.Delay(200 * (i + 1));
                else
                    throw;
            }
        }
    }

    // --- Auto-sync timer ---

    public void StartAutoSync()
    {
        if (_timer != null) return;
        _timer = new System.Timers.Timer();
        _timer.Elapsed += async (_, _) =>
        {
            if (!_isSyncing)
                await SyncAsync();
        };
        _timer.Interval = TimeSpan.FromMinutes(GetSyncIntervalMinutes()).TotalMilliseconds;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void StopAutoSync()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        StopAutoSync();
    }

    // --- Helpers ---

    private bool TryStartSync()
    {
        lock (_syncLock)
        {
            if (_isSyncing) return false;
            _isSyncing = true;
            return true;
        }
    }

    private void EndSync()
    {
        lock (_syncLock)
        {
            _isSyncing = false;
        }
    }
    private void Report(string message) => ProgressChanged?.Invoke(message);

    private static OkrObjective CreateLocalObjective(FeishuObjectiveDto dto, string userId)
    {
        return new OkrObjective
        {
            FeishuObjectiveId = dto.Id,
            UserId = userId,
            Name = dto.Name,
            Note = dto.Note,
            Progress = dto.Progress,
            Period = dto.Period,
            Weight = dto.Weight,
            FeishuCreatedAt = DateTime.UnixEpoch.AddMilliseconds(dto.CreatedAt),
            FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(dto.UpdatedAt),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            SyncStatus = OkrSyncStatus.Synced,
            LastSyncedAt = DateTime.Now
        };
    }

    private static void UpdateLocalObjective(OkrObjective local, FeishuObjectiveDto dto, string userId)
    {
        local.UserId = userId;
        local.Name = dto.Name;
        local.Note = dto.Note;
        local.Progress = dto.Progress;
        local.Period = dto.Period;
        local.Weight = dto.Weight;
        local.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(dto.UpdatedAt);
        local.UpdatedAt = DateTime.Now;
        local.SyncStatus = OkrSyncStatus.Synced;
        local.LastSyncedAt = DateTime.Now;
    }

    private static OkrKeyResult CreateLocalKr(FeishuKrDto dto, OkrObjective objective)
    {
        return new OkrKeyResult
        {
            FeishuKrId = dto.Id,
            Objective = objective,
            Name = dto.Name,
            CurrentValue = dto.CurrentValue,
            StartValue = dto.StartValue,
            TargetValue = dto.TargetValue,
            Progress = dto.Progress,
            Weight = dto.Weight,
            Unit = dto.Unit ?? "%",
            FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(dto.UpdatedAt),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            SyncStatus = OkrSyncStatus.Synced,
            LastSyncedAt = DateTime.Now
        };
    }

    private static void UpdateLocalKr(OkrKeyResult local, FeishuKrDto dto)
    {
        local.Name = dto.Name;
        local.CurrentValue = dto.CurrentValue;
        local.StartValue = dto.StartValue;
        local.TargetValue = dto.TargetValue;
        local.Progress = dto.Progress;
        local.Weight = dto.Weight;
        local.Unit = dto.Unit ?? "%";
        local.FeishuUpdatedAt = DateTime.UnixEpoch.AddMilliseconds(dto.UpdatedAt);
        local.UpdatedAt = DateTime.Now;
        local.SyncStatus = OkrSyncStatus.Synced;
        local.LastSyncedAt = DateTime.Now;
    }

    private static void LogSync(AppDbContext context, string action, string entityType,
        int? localId, string? feishuId, string message)
    {
        context.OkrSyncLogs.Add(new OkrSyncLog
        {
            Timestamp = DateTime.Now,
            Action = action,
            EntityType = entityType,
            LocalId = localId,
            FeishuId = feishuId,
            Message = message
        });
        // Save log entries immediately so they persist even if sync fails later
        try { context.SaveChanges(); } catch { }
    }
}
