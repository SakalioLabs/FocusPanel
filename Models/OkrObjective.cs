using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class OkrObjective
{
    [Key]
    public int Id { get; set; }
    public string? FeishuObjectiveId { get; set; }
    public string? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public double Progress { get; set; }
    public string? Period { get; set; }
    public double Weight { get; set; } = 1.0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? FeishuCreatedAt { get; set; }
    public DateTime? FeishuUpdatedAt { get; set; }
    public OkrSyncStatus SyncStatus { get; set; } = OkrSyncStatus.Synced;
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<OkrKeyResult> KeyResults { get; set; } = new List<OkrKeyResult>();
}
