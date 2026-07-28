using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public class OkrKeyResult : ObservableObject
{
    private string _name = string.Empty;
    private double _currentValue;
    private double _startValue;
    private double _targetValue = 100;
    private double _progress;
    private double _weight = 1.0;
    private string _unit = "%";
    private OkrSyncStatus _syncStatus =
        OkrSyncStatus.Synced;
    private bool _isDeleted;

    [Key]
    public int Id { get; set; }
    public string? FeishuKrId { get; set; }
    public int ObjectiveId { get; set; }
    public OkrObjective Objective { get; set; } = null!;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public double CurrentValue
    {
        get => _currentValue;
        set => SetProperty(
            ref _currentValue,
            value);
    }
    public double StartValue
    {
        get => _startValue;
        set => SetProperty(
            ref _startValue,
            value);
    }
    public double TargetValue
    {
        get => _targetValue;
        set => SetProperty(
            ref _targetValue,
            value);
    }
    public double Progress
    {
        get => _progress;
        set => SetProperty(
            ref _progress,
            value);
    }
    public double Weight
    {
        get => _weight;
        set => SetProperty(
            ref _weight,
            value);
    }
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? FeishuUpdatedAt { get; set; }
    public OkrSyncStatus SyncStatus
    {
        get => _syncStatus;
        set => SetProperty(
            ref _syncStatus,
            value);
    }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted
    {
        get => _isDeleted;
        set => SetProperty(
            ref _isDeleted,
            value);
    }
}
