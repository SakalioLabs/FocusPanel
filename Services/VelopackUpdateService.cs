using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using Velopack;
using Velopack.Sources;

namespace FocusPanel.Services;

public sealed class VelopackUpdateService : IAppUpdateService
{
    public const string RepositoryUrl = "https://github.com/SakalioLabs/FocusPanel";

    private readonly UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;
    private VelopackAsset? _downloadedAsset;

    public VelopackUpdateService()
    {
        try
        {
            var source = new GithubSource(RepositoryUrl, null, false);
            _manager = new UpdateManager(source);
        }
        catch (InvalidOperationException)
        {
            // A normal dotnet run / test process has no Velopack installation locator.
            _manager = null;
        }
    }

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public bool CanUpdate => _manager != null
        && (_manager.IsInstalled || _manager.IsPortable);

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanUpdate)
            return null;

        if (_manager == null)
            return null;

        _pendingUpdate = await _manager.CheckForUpdatesAsync();
        _downloadedAsset = null;
        if (_pendingUpdate == null)
            return null;

        VelopackAsset asset = _pendingUpdate.TargetFullRelease;
        return new AppUpdateInfo(
            asset.Version.ToString(),
            asset.NotesMarkdown,
            asset.Size);
    }

    public async Task DownloadUpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_manager == null || _pendingUpdate == null)
            throw new InvalidOperationException("请先检查更新。");

        await _manager.DownloadUpdatesAsync(
            _pendingUpdate,
            value => progress?.Report(value),
            cancellationToken);
        _downloadedAsset = _pendingUpdate.TargetFullRelease;
    }

    public void ApplyAndRestart()
    {
        if (_manager == null || _downloadedAsset == null)
            throw new InvalidOperationException("更新包尚未下载完成。");

        _manager.ApplyUpdatesAndRestart(_downloadedAsset);
    }

    public void Dispose()
    {
    }
}
