using System;
using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class OkrKeyResult
{
    [Key]
    public int Id { get; set; }
    public string? FeishuKrId { get; set; }
    public int ObjectiveId { get; set; }
    public OkrObjective Objective { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double StartValue { get; set; }
    public double TargetValue { get; set; } = 100;
    public double Progress { get; set; }
    public double Weight { get; set; } = 1.0;
    public string Unit { get; set; } = "%";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? FeishuUpdatedAt { get; set; }
    public OkrSyncStatus SyncStatus { get; set; } = OkrSyncStatus.Synced;
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}
