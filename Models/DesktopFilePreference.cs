using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class DesktopFilePreference
{
    [Key]
    public int Id { get; set; }
    public string FilePath { get; set; } = "";
    public string PartitionName { get; set; } = "";
    public bool IsHiddenFromDesktop { get; set; }
    public double? DesktopX { get; set; }
    public double? DesktopY { get; set; }
    public string? ManagedPath { get; set; }
    public long? OriginalAttributes { get; set; }
    public string? FileIdentity { get; set; }
    public DesktopCollectionMode CollectionMode { get; set; }
    public DesktopVisibilityOperation OperationState { get; set; }
}

public enum DesktopCollectionMode
{
    None = 0,
    Attribute = 1,
    LegacyStorage = 2
}

public enum DesktopVisibilityOperation
{
    Stable = 0,
    Collecting = 1,
    Restoring = 2,
    RecoveryRequired = 3
}
