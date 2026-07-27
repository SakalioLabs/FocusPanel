using System;
using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class OkrSyncLog
{
    [Key]
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? LocalId { get; set; }
    public string? FeishuId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
}
