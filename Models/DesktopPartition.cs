using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class DesktopPartition
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public int ColumnIndex { get; set; } // 0 or 1
}
