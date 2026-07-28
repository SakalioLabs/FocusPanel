using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public class OkrObjective : ObservableObject
{
    private string _name = string.Empty;
    private string? _note;
    private double _progress;
    private string? _period;
    private double _weight = 1.0;
    private OkrSyncStatus _syncStatus =
        OkrSyncStatus.Synced;
    private bool _isDeleted;

    [Key]
    public int Id { get; set; }
    public string? FeishuObjectiveId { get; set; }
    public string? UserId { get; set; }
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }
    public string? Period
    {
        get => _period;
        set => SetProperty(ref _period, value);
    }
    public double Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? FeishuCreatedAt { get; set; }
    public DateTime? FeishuUpdatedAt { get; set; }
    public OkrSyncStatus SyncStatus
    {
        get => _syncStatus;
        set => SetProperty(ref _syncStatus, value);
    }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted
    {
        get => _isDeleted;
        set => SetProperty(ref _isDeleted, value);
    }

    public ICollection<OkrKeyResult> KeyResults { get; set; } =
        new ObservableCollection<OkrKeyResult>();
}
