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

    private UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;
    private VelopackAsset? _downloadedAsset;

    public VelopackUpdateService()
    {
        SourceConfiguration = new AppUpdateSourceConfiguration(
            AppUpdateSourceKind.GitHub,
            RepositoryUrl);
        _ = TryConfigure(SourceConfiguration, out _);
    }

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public bool CanUpdate => _manager != null
        && (_manager.IsInstalled || _manager.IsPortable);

    public AppUpdateSourceConfiguration SourceConfiguration { get; private set; }

    public string SourceDescription => SourceConfiguration.Kind == AppUpdateSourceKind.GitHub
        ? "GitHub Releases"
        : $"局域网 · {SourceConfiguration.Location}";

    public bool TryConfigure(
        AppUpdateSourceConfiguration configuration,
        out string? error)
    {
        if (!AppUpdateSourcePolicy.TryNormalize(configuration, out var normalized, out error))
            return false;

        UpdateManager? manager;
        try
        {
            manager = normalized.Kind == AppUpdateSourceKind.GitHub
                ? new UpdateManager(new GithubSource(RepositoryUrl, null, false))
                : new UpdateManager(normalized.Location);
        }
        catch (InvalidOperationException)
        {
            // A normal dotnet run / test process has no Velopack installation locator.
            manager = null;
        }
        catch (Exception ex) when (ex is ArgumentException or UriFormatException)
        {
            error = $"无法使用此更新源：{ex.Message}";
            return false;
        }

        _manager = manager;
        _pendingUpdate = null;
        _downloadedAsset = null;
        SourceConfiguration = normalized;
        error = null;
        return true;
    }

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
