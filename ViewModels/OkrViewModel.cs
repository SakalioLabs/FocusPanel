using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.ViewModels;

internal sealed record PendingOkrWorkspaceSnapshot(
    long Revision,
    OkrWorkspaceSnapshot Snapshot);

public partial class OkrViewModel
    : ObservableObject, IOkrDataProvider, IDisposable
{
    private readonly OkrSyncService _syncService;
    private readonly IOkrWorkspaceRepository
        _workspaceRepository;
    private readonly Dispatcher _uiDispatcher;
    private readonly CoalescingBackgroundRefresh<
        PendingOkrWorkspaceSnapshot> _workspaceRefresh;
    private bool _syncSettingsReady;
    private bool _workspaceInitialized;
    private bool _disposed;
    private long _dataRevision;
    private string? _cachedUserId;

    [ObservableProperty]
    private ObservableCollection<OkrObjective> objectives = new();

    [ObservableProperty]
    private OkrObjective? selectedObjective;

    [ObservableProperty]
    private string newObjectiveName = string.Empty;

    [ObservableProperty]
    private string newObjectiveNote = string.Empty;

    [ObservableProperty]
    private string newObjectivePeriod = string.Empty;

    [ObservableProperty]
    private string newKrName = string.Empty;

    [ObservableProperty]
    private double newKrStartValue;

    [ObservableProperty]
    private double newKrTargetValue = 100;

    [ObservableProperty]
    private string newKrUnit = "%";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private string syncStatusText = "正在准备 OKR 工作区…";

    [ObservableProperty]
    private bool isConfigured;

    [ObservableProperty]
    private int syncIntervalMinutes = 30;

    [ObservableProperty]
    private DateTime? lastSyncTime;

    [ObservableProperty]
    private string lastSyncResultText = "尚未同步";

    [ObservableProperty]
    private bool showSettings;

    [ObservableProperty]
    private string settingsAppId = string.Empty;

    [ObservableProperty]
    private string settingsAppSecret = string.Empty;

    [ObservableProperty]
    private string settingsValidationMessage = string.Empty;

    [ObservableProperty]
    private bool settingsValidationSuccess;

    public List<int> SyncIntervalOptions { get; } = new() { 5, 15, 30, 60 };
    public bool HasObjectives => Objectives.Count > 0;

    partial void OnSelectedObjectiveChanged(OkrObjective? value)
    {
        OnPropertyChanged(nameof(IsObjectiveSelected));
    }

    public bool IsObjectiveSelected => SelectedObjective != null;

    public OkrViewModel()
        : this(
            new OkrSyncService(),
            new OkrWorkspaceRepository(),
            Dispatcher.CurrentDispatcher)
    {
    }

    internal OkrViewModel(
        OkrSyncService syncService,
        IOkrWorkspaceRepository workspaceRepository,
        Dispatcher uiDispatcher)
    {
        _syncService =
            syncService
            ?? throw new ArgumentNullException(
                nameof(syncService));
        _workspaceRepository =
            workspaceRepository
            ?? throw new ArgumentNullException(
                nameof(workspaceRepository));
        _uiDispatcher =
            uiDispatcher
            ?? throw new ArgumentNullException(
                nameof(uiDispatcher));
        _syncService.ProgressChanged += OnSyncProgress;
        _syncService.SyncCompleted += OnSyncCompleted;
        _workspaceRefresh =
            new CoalescingBackgroundRefresh<
                PendingOkrWorkspaceSnapshot>(
                CaptureWorkspace,
                ApplyWorkspaceAsync,
                ReportWorkspaceLoadFailure);
        IsLoading = true;
        _workspaceRefresh.Request();
    }

    partial void OnSyncIntervalMinutesChanged(
        int value)
    {
        if (!_syncSettingsReady
            || value < 1)
        {
            return;
        }

        _ = PersistSyncIntervalAsync(value);
    }

    private async Task PersistSyncIntervalAsync(
        int value)
    {
        try
        {
            await Task.Run(
                () =>
                    _syncService
                        .SetSyncIntervalMinutes(value));
            if (_disposed)
                return;

            SettingsValidationMessage =
                $"自动同步间隔已设为 {value} 分钟。";
            SettingsValidationSuccess = true;
        }
        catch (Exception ex)
        {
            if (_disposed)
                return;

            SettingsValidationMessage =
                $"无法保存同步间隔：{ex.Message}";
            SettingsValidationSuccess = false;
        }
    }

    private void OnSyncProgress(string message)
    {
        DispatchToUi(() =>
            SyncStatusText = message);
    }

    private void OnSyncCompleted(OkrSyncResult result)
    {
        DispatchToUi(() =>
            _ = HandleSyncCompletedAsync(result));
    }

    private void DispatchToUi(Action action)
    {
        if (_disposed)
            return;

        System.Windows.Threading.Dispatcher? dispatcher =
            Application.Current?.Dispatcher;
        if (dispatcher == null
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            if (!_disposed)
                action();
            return;
        }

        dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!_disposed)
                    action();
            }));
    }

    private async Task HandleSyncCompletedAsync(
        OkrSyncResult result)
    {
        IsSyncing = false;
        if (result.Success)
            LastSyncTime = DateTime.Now;
        LastSyncResultText = result.Success
            ? $"同步成功：{result.Message}"
            : $"同步失败：{result.Message}";
        SyncStatusText = LastSyncResultText;
        if (result.ObjectivesPulled > 0
            || result.KeyResultsPulled > 0
            || result.ObjectivesPushed > 0
            || result.KeyResultsPushed > 0)
        {
            Interlocked.Increment(
                ref _dataRevision);
            RequestWorkspaceRefresh();
        }
        await Task.CompletedTask;
    }

    // --- Data Loading ---

    [RelayCommand]
    private void LoadObjectives()
        => RequestWorkspaceRefresh();

    private void RequestWorkspaceRefresh()
    {
        if (_disposed)
            return;

        IsLoading = true;
        _workspaceRefresh.Request();
    }

    private PendingOkrWorkspaceSnapshot
        CaptureWorkspace() =>
        new(
            Volatile.Read(ref _dataRevision),
            _workspaceRepository.Load());

    private async Task ApplyWorkspaceAsync(
        PendingOkrWorkspaceSnapshot pending,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () => ApplyWorkspace(
                pending,
                cancellationToken),
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ApplyWorkspace(
        PendingOkrWorkspaceSnapshot pending,
        CancellationToken cancellationToken)
    {
        if (_disposed
            || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        long currentRevision =
            Volatile.Read(ref _dataRevision);
        if (!pending.Snapshot.IsValid)
        {
            IsLoading = false;
            SyncStatusText =
                "暂时无法读取 OKR，本地数据未被修改。";
            return;
        }
        if (!OkrWorkspaceApplyPolicy.CanApply(
                pending.Snapshot,
                pending.Revision,
                currentRevision))
        {
            _workspaceRefresh.Request();
            return;
        }

        OkrWorkspaceSnapshot snapshot =
            pending.Snapshot;
        Objectives.Clear();
        foreach (OkrObjective objective
                 in snapshot.Objectives)
        {
            Objectives.Add(objective);
        }
        OnPropertyChanged(nameof(HasObjectives));
        _cachedUserId = snapshot.UserId;
        IsLoading = false;

        if (_workspaceInitialized)
            return;

        _workspaceInitialized = true;
        IsConfigured = snapshot.IsConfigured;
        SyncIntervalMinutes =
            snapshot.SyncIntervalMinutes;
        LastSyncTime = snapshot.LastSyncTime;
        _syncSettingsReady = true;
        SyncStatusText = IsConfigured
            ? $"已加载 {snapshot.Objectives.Count} 个目标，等待同步。"
            : $"本地共有 {snapshot.Objectives.Count} 个目标；尚未配置飞书凭据。";
        if (IsConfigured)
            _syncService.StartAutoSync(
                snapshot.SyncIntervalMinutes);
    }

    private void ReportWorkspaceLoadFailure(
        Exception error)
    {
        _ = error;
        DispatchToUi(
            () =>
            {
                IsLoading = false;
                SyncStatusText =
                    "暂时无法读取 OKR，本地数据未被修改。";
            });
    }

    // --- Objective CRUD ---

    [RelayCommand]
    private async Task AddObjective()
    {
        if (string.IsNullOrWhiteSpace(NewObjectiveName))
        {
            SyncStatusText = "请先填写目标名称。";
            return;
        }

        var obj = new OkrObjective
        {
            Name = NewObjectiveName.Trim(),
            Note = string.IsNullOrWhiteSpace(NewObjectiveNote) ? null : NewObjectiveNote.Trim(),
            Period = string.IsNullOrWhiteSpace(NewObjectivePeriod) ? null : NewObjectivePeriod.Trim(),
            UserId = _cachedUserId,
            SyncStatus = OkrSyncStatus.LocalCreated,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            context.OkrObjectives.Add(obj);
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);

            Objectives.Insert(0, obj);
            OnPropertyChanged(nameof(HasObjectives));
            SelectedObjective = obj;
            NewObjectiveName = string.Empty;
            NewObjectiveNote = string.Empty;
            NewObjectivePeriod = string.Empty;
            SyncStatusText = "目标已保存到本地。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法创建目标：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteObjective(OkrObjective? obj)
    {
        if (obj == null) return;

        var result = FocusDialogService.Show(
            $"确定删除“{obj.Name}”及其全部关键结果吗？",
            "确认删除目标",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var dbObj = await context.OkrObjectives.FindAsync(obj.Id);
            if (dbObj == null)
                throw new InvalidOperationException(
                    "数据库中找不到该目标。");

            if (dbObj.SyncStatus == OkrSyncStatus.LocalCreated
                || dbObj.FeishuObjectiveId == null)
            {
                context.OkrObjectives.Remove(dbObj);
            }
            else
            {
                dbObj.SyncStatus =
                    OkrSyncStatus.LocalDeleted;
            }
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);

            Objectives.Remove(obj);
            OnPropertyChanged(nameof(HasObjectives));
            SelectedObjective = null;
            SyncStatusText = "目标已删除。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法删除目标：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateObjective(OkrObjective? obj)
    {
        if (obj == null || string.IsNullOrWhiteSpace(obj.Name)) return;

        obj.UpdatedAt = DateTime.Now;
        if (obj.SyncStatus == OkrSyncStatus.Synced)
            obj.SyncStatus = OkrSyncStatus.LocalModified;

        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var dbObj = await context.OkrObjectives.FindAsync(obj.Id);
            if (dbObj == null)
                throw new InvalidOperationException(
                    "数据库中找不到该目标。");

            dbObj.Name = obj.Name;
            dbObj.Note = obj.Note;
            dbObj.Period = obj.Period;
            dbObj.Weight = obj.Weight;
            dbObj.UpdatedAt = obj.UpdatedAt;
            dbObj.SyncStatus = obj.SyncStatus;
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);
            SyncStatusText = "目标信息已保存。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法保存目标：{ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectObjective(OkrObjective? obj)
    {
        SelectedObjective = SelectedObjective?.Id == obj?.Id ? null : obj;
    }

    // --- Key Result CRUD ---

    [RelayCommand]
    private async Task AddKeyResult(OkrObjective? obj)
    {
        if (obj == null
            || string.IsNullOrWhiteSpace(NewKrName))
        {
            SyncStatusText =
                "请选择目标并填写关键结果名称。";
            return;
        }

        var kr = new OkrKeyResult
        {
            ObjectiveId = obj.Id,
            Name = NewKrName.Trim(),
            StartValue = NewKrStartValue,
            CurrentValue = NewKrStartValue,
            TargetValue = NewKrTargetValue,
            Unit = NewKrUnit,
            SyncStatus = OkrSyncStatus.LocalCreated,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        kr.Progress =
            OkrProgressCalculator
                .CalculateKeyResultProgress(
                    kr.StartValue,
                    kr.CurrentValue,
                    kr.TargetValue);

        try
        {
            double objectiveProgress =
                OkrProgressCalculator
                    .CalculateObjectiveProgress(
                        obj.KeyResults.Append(kr));
            OkrSyncStatus objectiveSyncStatus =
                obj.SyncStatus == OkrSyncStatus.Synced
                    ? OkrSyncStatus.LocalModified
                    : obj.SyncStatus;

            using var context = new AppDbContext();
            context.EnsureSchema();
            context.Set<OkrKeyResult>().Add(kr);
            OkrObjective? storedObjective =
                await context.OkrObjectives
                    .FindAsync(obj.Id);
            if (storedObjective == null)
                throw new InvalidOperationException(
                    "数据库中找不到所属目标。");

            storedObjective.Progress =
                objectiveProgress;
            storedObjective.SyncStatus =
                objectiveSyncStatus;
            storedObjective.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);

            obj.KeyResults.Add(kr);
            obj.Progress = objectiveProgress;
            obj.SyncStatus = objectiveSyncStatus;
            NewKrName = string.Empty;
            NewKrStartValue = 0;
            NewKrTargetValue = 100;
            NewKrUnit = "%";
            SyncStatusText = "关键结果已添加。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法添加关键结果：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteKeyResult(OkrKeyResult? kr)
    {
        if (kr == null) return;

        var parent = Objectives.FirstOrDefault(o => o.KeyResults.Contains(kr));
        MessageBoxResult confirmation =
            FocusDialogService.Show(
                $"确定删除关键结果“{kr.Name}”吗？",
                "确认删除关键结果",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
            return;

        if (parent == null)
        {
            SyncStatusText =
                "找不到关键结果所属的目标。";
            return;
        }

        try
        {
            double objectiveProgress =
                OkrProgressCalculator
                    .CalculateObjectiveProgress(
                        parent.KeyResults.Where(
                            result =>
                                !ReferenceEquals(
                                    result,
                                    kr)));
            OkrSyncStatus objectiveSyncStatus =
                parent.SyncStatus
                    == OkrSyncStatus.Synced
                    ? OkrSyncStatus.LocalModified
                    : parent.SyncStatus;

            using var context = new AppDbContext();
            context.EnsureSchema();
            var dbKr = await context.Set<OkrKeyResult>().FindAsync(kr.Id);
            if (dbKr == null)
                throw new InvalidOperationException(
                    "数据库中找不到该关键结果。");

            if (dbKr.SyncStatus == OkrSyncStatus.LocalCreated
                || dbKr.FeishuKrId == null)
            {
                context.Set<OkrKeyResult>().Remove(dbKr);
            }
            else
            {
                dbKr.SyncStatus =
                    OkrSyncStatus.LocalDeleted;
                dbKr.IsDeleted = true;
                dbKr.UpdatedAt = DateTime.Now;
            }

            OkrObjective? storedObjective =
                await context.OkrObjectives
                    .FindAsync(parent.Id);
            if (storedObjective == null)
                throw new InvalidOperationException(
                    "数据库中找不到所属目标。");
            storedObjective.Progress =
                objectiveProgress;
            storedObjective.SyncStatus =
                objectiveSyncStatus;
            storedObjective.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);

            parent.KeyResults.Remove(kr);
            parent.Progress = objectiveProgress;
            parent.SyncStatus = objectiveSyncStatus;
            SyncStatusText = "关键结果已删除。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法删除关键结果：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateKeyResult(OkrKeyResult? kr)
    {
        if (kr == null || string.IsNullOrWhiteSpace(kr.Name)) return;

        kr.UpdatedAt = DateTime.Now;
        kr.Progress =
            OkrProgressCalculator
                .CalculateKeyResultProgress(
                    kr.StartValue,
                    kr.CurrentValue,
                    kr.TargetValue);

        if (kr.SyncStatus == OkrSyncStatus.Synced)
            kr.SyncStatus = OkrSyncStatus.LocalModified;

        OkrObjective? parent =
            Objectives.FirstOrDefault(
                objective =>
                    objective.Id
                    == kr.ObjectiveId);
        if (parent == null)
        {
            SyncStatusText =
                "找不到关键结果所属的目标。";
            return;
        }

        double objectiveProgress =
            OkrProgressCalculator
                .CalculateObjectiveProgress(
                    parent.KeyResults);
        OkrSyncStatus objectiveSyncStatus =
            parent.SyncStatus
                == OkrSyncStatus.Synced
                ? OkrSyncStatus.LocalModified
                : parent.SyncStatus;

        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var dbKr = await context.Set<OkrKeyResult>().FindAsync(kr.Id);
            if (dbKr == null)
                throw new InvalidOperationException(
                    "数据库中找不到该关键结果。");

            dbKr.Name = kr.Name;
            dbKr.CurrentValue = kr.CurrentValue;
            dbKr.StartValue = kr.StartValue;
            dbKr.TargetValue = kr.TargetValue;
            dbKr.Unit = kr.Unit;
            dbKr.Weight = kr.Weight;
            dbKr.Progress = kr.Progress;
            dbKr.UpdatedAt = kr.UpdatedAt;
            dbKr.SyncStatus = kr.SyncStatus;

            OkrObjective? storedObjective =
                await context.OkrObjectives
                    .FindAsync(parent.Id);
            if (storedObjective == null)
                throw new InvalidOperationException(
                    "数据库中找不到所属目标。");
            storedObjective.Progress =
                objectiveProgress;
            storedObjective.SyncStatus =
                objectiveSyncStatus;
            storedObjective.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            Interlocked.Increment(
                ref _dataRevision);

            parent.Progress = objectiveProgress;
            parent.SyncStatus = objectiveSyncStatus;
            SyncStatusText =
                "关键结果进度已保存。";
        }
        catch (Exception ex)
        {
            SyncStatusText =
                $"无法保存关键结果：{ex.Message}";
        }
    }

    // --- Sync ---

    [RelayCommand]
    private async Task SyncNow()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncStatusText = "正在同步飞书 OKR…";
        try
        {
            OkrSyncResult result =
                await Task.Run(
                    () => _syncService.SyncAsync());
            if (!result.Success)
            {
                IsSyncing = false;
                LastSyncResultText =
                    $"同步失败：{result.Message}";
            }
        }
        catch (Exception ex)
        {
            IsSyncing = false;
            LastSyncResultText =
                $"同步失败：{ex.Message}";
            SyncStatusText =
                $"同步异常：{ex.Message}";
        }
    }

    // --- Settings ---

    [RelayCommand]
    private async Task ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        if (ShowSettings)
        {
            try
            {
                var auth = new FeishuAuthService();
                SettingsAppId =
                    await Task.Run(auth.GetAppId)
                    ?? string.Empty;
                SettingsAppSecret = string.Empty;
                SettingsValidationMessage =
                    string.Empty;
                SettingsValidationSuccess = false;
            }
            catch (Exception ex)
            {
                SettingsValidationMessage =
                    $"无法读取飞书设置：{ex.Message}";
                SettingsValidationSuccess = false;
            }
        }
    }

    [RelayCommand]
    private async Task ValidateAndSaveCredentials()
    {
        if (string.IsNullOrWhiteSpace(SettingsAppId) || string.IsNullOrWhiteSpace(SettingsAppSecret))
        {
            SettingsValidationMessage =
                "App ID 和 App Secret 均不能为空。";
            SettingsValidationSuccess = false;
            return;
        }

        try
        {
            SettingsValidationMessage =
                "正在验证飞书凭据…";
            var auth = new FeishuAuthService();
            bool valid =
                await auth.ValidateCredentialsAsync(
                    SettingsAppId.Trim(),
                    SettingsAppSecret.Trim());

            if (valid)
            {
                string appId = SettingsAppId.Trim();
                string appSecret =
                    SettingsAppSecret.Trim();
                await Task.Run(
                    () => auth.SaveCredentials(
                        appId,
                        appSecret));
                SettingsAppSecret = string.Empty;
                SettingsValidationMessage =
                    "凭据验证成功并已安全保存。";
                SettingsValidationSuccess = true;
                IsConfigured = true;
                RequestWorkspaceRefresh();
                _syncService.StartAutoSync(
                    SyncIntervalMinutes);
            }
            else
            {
                SettingsValidationMessage =
                    "验证失败，请检查 App ID、App Secret 和应用权限。";
                SettingsValidationSuccess = false;
            }
        }
        catch (Exception ex)
        {
            SettingsValidationMessage =
                $"无法保存飞书凭据：{ex.Message}";
            SettingsValidationSuccess = false;
        }
    }

    [RelayCommand]
    private async Task ClearCredentials()
    {
        MessageBoxResult confirmation =
            FocusDialogService.Show(
                "确定清除飞书凭据并停止自动同步吗？本地 OKR 数据不会删除。",
                "清除飞书凭据",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
            return;

        try
        {
            var auth = new FeishuAuthService();
            await Task.Run(auth.ClearCredentials);
            IsConfigured = false;
            SettingsAppId = string.Empty;
            SettingsAppSecret = string.Empty;
            SettingsValidationMessage =
                "飞书凭据已清除，本地 OKR 数据已保留。";
            SettingsValidationSuccess = true;
            ShowSettings = false;
            _syncService.StopAutoSync();
            Objectives.Clear();
            OnPropertyChanged(nameof(HasObjectives));
            RequestWorkspaceRefresh();
        }
        catch (Exception ex)
        {
            SettingsValidationMessage =
                $"无法清除飞书凭据：{ex.Message}";
            SettingsValidationSuccess = false;
        }
    }

    // --- IOkrDataProvider implementation (AI hooks, future use) ---

    public async Task<string>
        GetOkrContextForAIAsync(
            CancellationToken cancellationToken = default)
    {
        return await _uiDispatcher.InvokeAsync(
            BuildOkrContextForAI,
            DispatcherPriority.Background,
            cancellationToken);
    }

    private string BuildOkrContextForAI()
    {
        var lines = new List<string> { "=== Current OKRs ===" };
        foreach (var obj in Objectives.Where(o => !o.IsDeleted))
        {
            lines.Add($"\n[Objective] {obj.Name} (Progress: {obj.Progress:F0}%, Period: {obj.Period})");
            if (!string.IsNullOrEmpty(obj.Note))
                lines.Add($"  Note: {obj.Note}");

            foreach (var kr in obj.KeyResults.Where(k => !k.IsDeleted))
            {
                lines.Add($"  - KR: {kr.Name} ({kr.CurrentValue} / {kr.StartValue} -> {kr.TargetValue} {kr.Unit}, Progress: {kr.Progress:F0}%)");
            }
        }

        if (Objectives.Count == 0)
            lines.Add("No OKRs configured.");

        return string.Join("\n", lines);
    }

    public async Task<OkrObjective>
        CreateDraftFromAIAsync(
            string name,
            string? note,
            List<(
                string name,
                double start,
                double target,
                string unit)> krs,
            CancellationToken cancellationToken = default)
    {
        var obj = new OkrObjective
        {
            Name = name,
            Note = note,
            Period = DateTime.Now.Year + " Q" + ((DateTime.Now.Month - 1) / 3 + 1),
            UserId = _cachedUserId,
            SyncStatus = OkrSyncStatus.LocalCreated,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        foreach (var (krName, start, target, unit) in krs)
        {
            obj.KeyResults.Add(new OkrKeyResult
            {
                Name = krName,
                StartValue = start,
                CurrentValue = start,
                TargetValue = target,
                Unit = unit,
                SyncStatus = OkrSyncStatus.LocalCreated,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }

        await Task.Run(
            () =>
                _workspaceRepository.SaveDraft(obj),
            cancellationToken);
        Interlocked.Increment(
            ref _dataRevision);
        await _uiDispatcher.InvokeAsync(
            () =>
            {
                if (_disposed)
                    return;

                Objectives.Insert(0, obj);
                OnPropertyChanged(
                    nameof(HasObjectives));
            },
            DispatcherPriority.Background,
            cancellationToken);
        return obj;
    }

    public async Task<IReadOnlyList<OkrObjective>>
        GetAllObjectivesAsync(
            CancellationToken cancellationToken = default) =>
        await _uiDispatcher.InvokeAsync(
            () =>
                (IReadOnlyList<OkrObjective>)
                    Objectives.ToList(),
            DispatcherPriority.Background,
            cancellationToken);

    public Task<OkrSyncResult> TriggerSyncAsync(
        CancellationToken cancellationToken = default)
        => Task.Run(
            () => _syncService.SyncAsync(),
            cancellationToken);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _workspaceRefresh.Dispose();
        _syncService.ProgressChanged -= OnSyncProgress;
        _syncService.SyncCompleted -= OnSyncCompleted;
        _syncService.Dispose();
    }
}
