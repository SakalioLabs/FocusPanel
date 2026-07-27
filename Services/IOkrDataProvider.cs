using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

/// <summary>
/// Interface exposing OKR data for AI assistant consumption (future phase).
/// Implemented by OkrViewModel.
/// </summary>
public interface IOkrDataProvider
{
    string GetOkrContextForAI();
    OkrObjective CreateDraftFromAI(string name, string? note,
        List<(string name, double start, double target, string unit)> krs);
    List<OkrObjective> GetAllObjectives();
    OkrSyncResult TriggerSync();
}
