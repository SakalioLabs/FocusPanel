using System;
using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public enum AppLaunchKind
{
    Shortcut = 0,
    Executable = 1,
    ShellApp = 2
}

public class PinnedApp
{
    [Key]
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public AppLaunchKind LaunchKind { get; set; }
    public string LaunchTarget { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? IconKey { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
