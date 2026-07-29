using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

/// <summary>
/// Interface exposing OKR data for AI assistant consumption (future phase).
/// Implemented by OkrViewModel.
/// </summary>
public interface IOkrDataProvider
{
    Task<string> GetOkrContextForAIAsync(
        CancellationToken cancellationToken = default);
    Task<OkrObjective> CreateDraftFromAIAsync(
        string name,
        string? note,
        List<(
            string name,
            double start,
            double target,
            string unit)> krs,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OkrObjective>>
        GetAllObjectivesAsync(
            CancellationToken cancellationToken = default);
    Task<OkrSyncResult> TriggerSyncAsync(
        CancellationToken cancellationToken = default);
}
