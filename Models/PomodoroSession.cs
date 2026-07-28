using System;
using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class PomodoroSession
{
    [Key]
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty; // "Completed", "Interrupted"
}
