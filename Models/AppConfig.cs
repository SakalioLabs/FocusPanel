using System.ComponentModel.DataAnnotations;

namespace FocusPanel.Models;

public class AppConfig
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
