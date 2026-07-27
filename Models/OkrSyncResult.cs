using System.Collections.Generic;

namespace FocusPanel.Models;

public class OkrSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ObjectivesPulled { get; set; }
    public int ObjectivesPushed { get; set; }
    public int KeyResultsPulled { get; set; }
    public int KeyResultsPushed { get; set; }
    public int Conflicts { get; set; }
    public List<string> Errors { get; set; } = new();
}
