using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.ViewModels;

public partial class OkrViewModel : ObservableObject, IOkrDataProvider
{
    private readonly OkrSyncService _syncService;

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
    private string syncStatusText = string.Empty;

    [ObservableProperty]
    private bool isConfigured;

    [ObservableProperty]
    private int syncIntervalMinutes = 30;

    [ObservableProperty]
    private DateTime? lastSyncTime;

    [ObservableProperty]
    private string lastSyncResultText = string.Empty;

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

    partial void OnSelectedObjectiveChanged(OkrObjective? value)
    {
        OnPropertyChanged(nameof(IsObjectiveSelected));
    }

    public bool IsObjectiveSelected => SelectedObjective != null;

    public OkrViewModel()
    {
        _syncService = new OkrSyncService();
        _syncService.ProgressChanged += OnSyncProgress;
        _syncService.SyncCompleted += OnSyncCompleted;

        IsConfigured = _syncService.IsConfigured;
        SyncIntervalMinutes = _syncService.GetSyncIntervalMinutes();
        LastSyncTime = _syncService.GetLastSyncTime();

        if (IsConfigured)
        {
            LoadObjectivesCommand.Execute(null);
            _syncService.StartAutoSync();
        }
        else
        {
            SyncStatusText = "Feishu credentials not configured. Click Settings to set up.";
        }
    }

    private void OnSyncProgress(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SyncStatusText = message;
        });
    }

    private void OnSyncCompleted(OkrSyncResult result)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            IsSyncing = false;
            LastSyncTime = _syncService.GetLastSyncTime();
            LastSyncResultText = result.Success ? $"OK: {result.Message}" : $"Error: {result.Message}";
            if (result.ObjectivesPulled > 0 || result.KeyResultsPulled > 0)
            {
                await LoadObjectives();
            }
        });
    }

    // --- Data Loading ---

    [RelayCommand]
    private async Task LoadObjectives()
    {
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                using var context = new AppDbContext();
                context.EnsureSchema();
                var items = context.OkrObjectives
                    .Include(o => o.KeyResults)
                    .Where(o => !o.IsDeleted)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Objectives.Clear();
                    foreach (var item in items)
                    {
                        Objectives.Add(item);
                    }
                });
            });
        }
        catch (Exception ex)
        {
            SyncStatusText = $"Error loading OKRs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- Objective CRUD ---

    [RelayCommand]
    private async Task AddObjective()
    {
        if (string.IsNullOrWhiteSpace(NewObjectiveName)) return;

        var obj = new OkrObjective
        {
            Name = NewObjectiveName.Trim(),
            Note = string.IsNullOrWhiteSpace(NewObjectiveNote) ? null : NewObjectiveNote.Trim(),
            Period = string.IsNullOrWhiteSpace(NewObjectivePeriod) ? null : NewObjectivePeriod.Trim(),
            UserId = GetUserId(),
            SyncStatus = OkrSyncStatus.LocalCreated,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            context.OkrObjectives.Add(obj);
            await context.SaveChangesAsync();
        }

        Objectives.Insert(0, obj);
        NewObjectiveName = string.Empty;
        NewObjectiveNote = string.Empty;
        NewObjectivePeriod = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteObjective(OkrObjective? obj)
    {
        if (obj == null) return;

        var result = MessageBox.Show(
            $"Delete objective '{obj.Name}' and all its key results?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        Objectives.Remove(obj);

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            var dbObj = await context.OkrObjectives.FindAsync(obj.Id);
            if (dbObj != null)
            {
                if (dbObj.SyncStatus == OkrSyncStatus.LocalCreated || dbObj.FeishuObjectiveId == null)
                {
                    context.OkrObjectives.Remove(dbObj);
                }
                else
                {
                    dbObj.SyncStatus = OkrSyncStatus.LocalDeleted;
                }
                await context.SaveChangesAsync();
            }
        }

        SelectedObjective = null;
    }

    [RelayCommand]
    private async Task UpdateObjective(OkrObjective? obj)
    {
        if (obj == null || string.IsNullOrWhiteSpace(obj.Name)) return;

        obj.UpdatedAt = DateTime.Now;
        if (obj.SyncStatus == OkrSyncStatus.Synced)
            obj.SyncStatus = OkrSyncStatus.LocalModified;

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            var dbObj = await context.OkrObjectives.FindAsync(obj.Id);
            if (dbObj != null)
            {
                dbObj.Name = obj.Name;
                dbObj.Note = obj.Note;
                dbObj.Period = obj.Period;
                dbObj.Weight = obj.Weight;
                dbObj.UpdatedAt = obj.UpdatedAt;
                dbObj.SyncStatus = obj.SyncStatus;
                await context.SaveChangesAsync();
            }
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
        if (obj == null || string.IsNullOrWhiteSpace(NewKrName)) return;

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

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            context.Set<OkrKeyResult>().Add(kr);
            await context.SaveChangesAsync();
        }

        obj.KeyResults.Add(kr);
        RecalculateObjectiveProgress(obj);
        NewKrName = string.Empty;
        NewKrStartValue = 0;
        NewKrTargetValue = 100;
        NewKrUnit = "%";
    }

    [RelayCommand]
    private async Task DeleteKeyResult(OkrKeyResult? kr)
    {
        if (kr == null) return;

        var parent = Objectives.FirstOrDefault(o => o.KeyResults.Contains(kr));
        parent?.KeyResults.Remove(kr);

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            var dbKr = await context.Set<OkrKeyResult>().FindAsync(kr.Id);
            if (dbKr != null)
            {
                if (dbKr.SyncStatus == OkrSyncStatus.LocalCreated || dbKr.FeishuKrId == null)
                {
                    context.Set<OkrKeyResult>().Remove(dbKr);
                }
                else
                {
                    dbKr.SyncStatus = OkrSyncStatus.LocalDeleted;
                    dbKr.IsDeleted = true;
                    dbKr.UpdatedAt = DateTime.Now;
                }
                await context.SaveChangesAsync();
            }
        }

        if (parent != null) RecalculateObjectiveProgress(parent);
    }

    [RelayCommand]
    private async Task UpdateKeyResult(OkrKeyResult? kr)
    {
        if (kr == null || string.IsNullOrWhiteSpace(kr.Name)) return;

        kr.UpdatedAt = DateTime.Now;
        kr.Progress = kr.TargetValue > 0 ? (kr.CurrentValue - kr.StartValue) / (kr.TargetValue - kr.StartValue) * 100 : 0;
        kr.Progress = Math.Max(0, Math.Min(100, kr.Progress));

        if (kr.SyncStatus == OkrSyncStatus.Synced)
            kr.SyncStatus = OkrSyncStatus.LocalModified;

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            var dbKr = await context.Set<OkrKeyResult>().FindAsync(kr.Id);
            if (dbKr != null)
            {
                dbKr.Name = kr.Name;
                dbKr.CurrentValue = kr.CurrentValue;
                dbKr.StartValue = kr.StartValue;
                dbKr.TargetValue = kr.TargetValue;
                dbKr.Unit = kr.Unit;
                dbKr.Weight = kr.Weight;
                dbKr.Progress = kr.Progress;
                dbKr.UpdatedAt = kr.UpdatedAt;
                dbKr.SyncStatus = kr.SyncStatus;
                await context.SaveChangesAsync();
            }
        }

        var parent = Objectives.FirstOrDefault(o => o.Id == kr.ObjectiveId);
        if (parent != null) RecalculateObjectiveProgress(parent);
    }

    private void RecalculateObjectiveProgress(OkrObjective obj)
    {
        var activeKrs = obj.KeyResults.Where(k => !k.IsDeleted).ToList();
        if (activeKrs.Count == 0) return;

        double totalWeight = activeKrs.Sum(k => k.Weight);
        obj.Progress = totalWeight > 0
            ? activeKrs.Sum(k => k.Progress * k.Weight) / totalWeight
            : activeKrs.Average(k => k.Progress);

        if (obj.SyncStatus == OkrSyncStatus.Synced)
            obj.SyncStatus = OkrSyncStatus.LocalModified;
    }

    // --- Sync ---

    [RelayCommand]
    private async Task SyncNow()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncStatusText = "Starting sync...";
        try
        {
            var result = await _syncService.SyncAsync();
            if (!result.Success)
            {
                IsSyncing = false;
                LastSyncResultText = $"Error: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            IsSyncing = false;
            LastSyncResultText = $"Error: {ex.Message}";
            SyncStatusText = $"Sync error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetSyncInterval(int minutes)
    {
        SyncIntervalMinutes = minutes;
        _syncService.SetSyncIntervalMinutes(minutes);
    }

    // --- Settings ---

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        if (ShowSettings)
        {
            var auth = new FeishuAuthService();
            SettingsAppId = auth.GetAppId() ?? string.Empty;
            SettingsAppSecret = auth.GetAppSecret() ?? string.Empty;
            SettingsValidationMessage = string.Empty;
            SettingsValidationSuccess = false;
        }
    }

    [RelayCommand]
    private async Task ValidateAndSaveCredentials()
    {
        if (string.IsNullOrWhiteSpace(SettingsAppId) || string.IsNullOrWhiteSpace(SettingsAppSecret))
        {
            SettingsValidationMessage = "App ID and App Secret are required.";
            SettingsValidationSuccess = false;
            return;
        }

        SettingsValidationMessage = "Validating...";
        var auth = new FeishuAuthService();
        var valid = await auth.ValidateCredentialsAsync(SettingsAppId.Trim(), SettingsAppSecret.Trim());

        if (valid)
        {
            auth.SaveCredentials(SettingsAppId.Trim(), SettingsAppSecret.Trim());
            SettingsValidationMessage = "Credentials validated and saved.";
            SettingsValidationSuccess = true;
            IsConfigured = true;
            await LoadObjectives();
            _syncService.StartAutoSync();
        }
        else
        {
            SettingsValidationMessage = "Validation failed. Check your App ID and App Secret.";
            SettingsValidationSuccess = false;
        }
    }

    [RelayCommand]
    private void ClearCredentials()
    {
        var auth = new FeishuAuthService();
        auth.ClearCredentials();
        IsConfigured = false;
        SettingsAppId = string.Empty;
        SettingsAppSecret = string.Empty;
        SettingsValidationMessage = "Credentials removed.";
        SettingsValidationSuccess = false;
        ShowSettings = false;
        _syncService.StopAutoSync();
        Objectives.Clear();
    }

    // --- Helpers ---

    private static string? GetUserId()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            return context.OkrObjectives
                .Where(o => o.UserId != null)
                .Select(o => o.UserId)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    // --- IOkrDataProvider implementation (AI hooks, future use) ---

    public string GetOkrContextForAI()
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

    public OkrObjective CreateDraftFromAI(string name, string? note,
        List<(string name, double start, double target, string unit)> krs)
    {
        var obj = new OkrObjective
        {
            Name = name,
            Note = note,
            Period = DateTime.Now.Year + " Q" + ((DateTime.Now.Month - 1) / 3 + 1),
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

        Application.Current.Dispatcher.Invoke(() =>
        {
            Objectives.Insert(0, obj);
        });

        using (var context = new AppDbContext())
        {
            context.EnsureSchema();
            context.OkrObjectives.Add(obj);
            context.SaveChanges();
        }

        return obj;
    }

    public List<OkrObjective> GetAllObjectives()
    {
        return Objectives.ToList();
    }

    public OkrSyncResult TriggerSync()
    {
        var task = _syncService.SyncAsync();
        task.Wait();
        return task.Result;
    }
}
