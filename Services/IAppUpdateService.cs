using System;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

public interface IAppUpdateService : IDisposable
{
    string CurrentVersion { get; }
    bool CanUpdate { get; }
    string SourceDescription { get; }
    Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task DownloadUpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
    void ApplyAndRestart();
}
