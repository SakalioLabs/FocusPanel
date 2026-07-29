using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;
using Velopack;
using Velopack.Sources;

namespace FocusPanel.Services;

internal interface IVelopackUpdateBoundary
{
    string? CurrentVersion { get; }
    bool CanUpdate { get; }
    Task<AppUpdateInfo?> CheckForUpdateAsync();
    Task DownloadUpdateAsync(
        IProgress<int>? progress,
        CancellationToken cancellationToken);
    void ApplyAndRestart();
}

internal sealed class VelopackUpdateBoundary :
    IVelopackUpdateBoundary
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _pendingUpdate;
    private VelopackAsset? _downloadedAsset;

    public VelopackUpdateBoundary(
        IUpdateSource source)
    {
        _manager = new UpdateManager(source);
    }

    public string? CurrentVersion =>
        _manager.CurrentVersion?.ToString();

    public bool CanUpdate =>
        _manager.IsInstalled
        || _manager.IsPortable;

    public async Task<AppUpdateInfo?>
        CheckForUpdateAsync()
    {
        if (!CanUpdate)
            return null;

        _pendingUpdate =
            await _manager.CheckForUpdatesAsync();
        _downloadedAsset = null;
        if (_pendingUpdate == null)
            return null;

        VelopackAsset asset =
            _pendingUpdate.TargetFullRelease;
        return new AppUpdateInfo(
            asset.Version.ToString(),
            asset.NotesMarkdown,
            asset.Size);
    }

    public async Task DownloadUpdateAsync(
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (_pendingUpdate == null)
            throw new InvalidOperationException(
                "请先检查更新。");

        await _manager.DownloadUpdatesAsync(
            _pendingUpdate,
            value => progress?.Report(value),
            cancellationToken);
        _downloadedAsset =
            _pendingUpdate.TargetFullRelease;
    }

    public void ApplyAndRestart()
    {
        if (_downloadedAsset == null)
            throw new InvalidOperationException(
                "更新包尚未下载完成。");

        _manager.ApplyUpdatesAndRestart(
            _downloadedAsset);
    }
}

public sealed class VelopackUpdateService : IAppUpdateService
{
    public const string RepositoryUrl = "https://github.com/SakalioLabs/FocusPanel";
    public const string StaticFeedUrl =
        "https://github.com/SakalioLabs/FocusPanel/releases/latest/download";
    public const string DownloadPageUrl =
        "https://github.com/SakalioLabs/FocusPanel/releases/latest";

    private readonly Task<IVelopackUpdateBoundary?>
        _managerInitialization;
    private readonly string _assemblyVersion;
    private IVelopackUpdateBoundary? _manager;

    public VelopackUpdateService() : this(
        CreateUpdateBoundary)
    {
    }

    internal VelopackUpdateService(
        Func<IVelopackUpdateBoundary?>
            managerFactory)
    {
        ArgumentNullException.ThrowIfNull(
            managerFactory);
        _assemblyVersion =
            Assembly.GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString(3)
            ?? "0.0.0";
        _managerInitialization =
            Task.Run(managerFactory);
    }

    public string CurrentVersion =>
        Volatile.Read(ref _manager)?
            .CurrentVersion
        ?? _assemblyVersion;

    public bool CanUpdate =>
        Volatile.Read(ref _manager)?
            .CanUpdate
        ?? false;

    public string SourceDescription => "GitHub Releases · 静态清单";

    internal static IUpdateSource CreateUpdateSource()
        => new SimpleWebSource(StaticFeedUrl);

    private static IVelopackUpdateBoundary?
        CreateUpdateBoundary()
    {
        try
        {
            return new VelopackUpdateBoundary(
                CreateUpdateSource());
        }
        catch (InvalidOperationException)
        {
            // A normal dotnet run / test process has no
            // Velopack installation locator.
            return null;
        }
    }

    private async Task<IVelopackUpdateBoundary?>
        GetManagerAsync(
            CancellationToken cancellationToken)
    {
        IVelopackUpdateBoundary? manager =
            await _managerInitialization
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        Volatile.Write(ref _manager, manager);
        return manager;
    }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IVelopackUpdateBoundary? manager =
            await GetManagerAsync(
                cancellationToken);
        if (manager == null
            || !manager.CanUpdate)
        {
            return null;
        }

        return await manager
            .CheckForUpdateAsync();
    }

    public async Task DownloadUpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IVelopackUpdateBoundary? manager =
            await GetManagerAsync(
                cancellationToken);
        if (manager == null
            || !manager.CanUpdate)
        {
            throw new InvalidOperationException("请先检查更新。");
        }

        await manager.DownloadUpdateAsync(
            progress,
            cancellationToken);
    }

    public void ApplyAndRestart()
    {
        IVelopackUpdateBoundary? manager =
            Volatile.Read(ref _manager);
        if (manager == null
            || !manager.CanUpdate)
        {
            throw new InvalidOperationException("更新包尚未下载完成。");
        }

        manager.ApplyAndRestart();
    }

    public bool OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DownloadPageUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
