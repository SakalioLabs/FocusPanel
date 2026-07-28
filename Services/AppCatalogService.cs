using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using FocusPanel.Data;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public sealed class AppCatalogService : IAppCatalogService
{
    private readonly List<AppLaunchItem> _catalog = new();
    private readonly object _catalogLock = new();
    private readonly object _indexLock = new();
    private readonly object _iconLock = new();
    private readonly IAppIdentityResolver _identityResolver;
    private readonly IAppCatalogSource _catalogSource;
    private readonly IAppIconSource _iconSource;
    private readonly Func<IReadOnlyList<PinnedApp>>
        _pinnedLoader;
    private readonly Queue<string> _iconQueue = new();
    private readonly Dictionary<string, List<AppLaunchItem>>
        _iconWaiters =
            new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource>
        _loadedIcons =
            new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedIconKeys =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _indexCancellation;
    private volatile bool _isIndexing;
    private volatile bool _disposed;
    private bool _iconWorkerRunning;

    public AppCatalogService() : this(
        new AppIdentityResolver(),
        new WindowsAppCatalogSource(),
        new WindowsAppIconSource(),
        LoadPinnedEntities)
    {
    }

    internal AppCatalogService(
        IAppIdentityResolver identityResolver) : this(
        identityResolver,
        new WindowsAppCatalogSource(),
        new WindowsAppIconSource(),
        LoadPinnedEntities)
    {
    }

    internal AppCatalogService(
        IAppIdentityResolver identityResolver,
        IAppCatalogSource catalogSource,
        IAppIconSource iconSource,
        Func<IReadOnlyList<PinnedApp>> pinnedLoader)
    {
        _identityResolver = identityResolver;
        _catalogSource = catalogSource;
        _iconSource = iconSource;
        _pinnedLoader = pinnedLoader;
        Refresh();
    }

    public event EventHandler? CatalogChanged;
    public bool IsIndexing => _isIndexing;

    public void Refresh()
    {
        CancellationTokenSource cancellation;
        lock (_indexLock)
        {
            if (_disposed)
                return;
            _indexCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            _indexCancellation = cancellation;
            _isIndexing = true;
        }

        RaiseCatalogChanged();
        var thread = new Thread(
            () => BuildCatalog(cancellation))
        {
            IsBackground = true,
            Name = "FocusPanel.AppCatalog"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public IReadOnlyList<AppLaunchItem> Search(string query, int limit = 24)
    {
        string normalized = query?.Trim() ?? string.Empty;
        List<AppLaunchItem> snapshot;
        lock (_catalogLock)
            snapshot = _catalog.ToList();

        IEnumerable<AppLaunchItem> matches = snapshot;
        if (normalized.Length > 0)
        {
            matches = matches.Where(app =>
                app.DisplayName.Contains(normalized, StringComparison.CurrentCultureIgnoreCase)
                || Path.GetFileNameWithoutExtension(app.LaunchTarget)
                    .Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        List<AppLaunchItem> results = matches
            .OrderByDescending(app => app.IsPinned)
            .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToList();
        QueueIconLoads(results);
        return results;
    }

    public IReadOnlyList<AppLaunchItem> GetPinned()
    {
        IReadOnlyList<PinnedApp> entities =
            _pinnedLoader();
        List<AppLaunchItem> results = entities
            .OrderBy(item => item.OrderIndex)
            .Select(entity =>
            {
                AppLaunchItem? catalogItem;
                lock (_catalogLock)
                {
                    catalogItem = _catalog.FirstOrDefault(item =>
                        string.Equals(BuildKey(item), BuildKey(entity), StringComparison.OrdinalIgnoreCase));
                }
                AppLaunchItem result = catalogItem ?? ToLaunchItem(entity);
                return result;
            })
            .ToList();
        QueueIconLoads(results);
        return results;
    }

    public bool Launch(AppLaunchItem app)
    {
        if (!AppLaunchRequestBuilder.TryBuild(
                app,
                out ProcessStartInfo? startInfo)
            || startInfo == null)
        {
            return false;
        }

        return AppLaunchExecution.TryStart(startInfo);
    }

    public void SetPinned(AppLaunchItem app, bool pinned)
    {
        EnsureIdentity(app);
        using var context = new AppDbContext();
        string key = BuildKey(app);
        var existing = context.PinnedApps.AsEnumerable()
            .FirstOrDefault(item => string.Equals(BuildKey(item), key, StringComparison.OrdinalIgnoreCase));

        if (pinned && existing == null)
        {
            int nextOrder = context.PinnedApps.Any()
                ? context.PinnedApps.Max(item => item.OrderIndex) + 1
                : 0;
            context.PinnedApps.Add(new PinnedApp
            {
                DisplayName = app.DisplayName,
                LaunchKind = app.LaunchKind,
                LaunchTarget = app.LaunchTarget,
                Arguments = app.Arguments,
                IconKey = app.IconKey,
                OrderIndex = nextOrder,
                CreatedAt = DateTime.Now
            });
        }
        else if (!pinned && existing != null)
        {
            context.PinnedApps.Remove(existing);
        }

        context.SaveChanges();
        app.IsPinned = pinned;
    }

    public void MovePinned(AppLaunchItem app, int newIndex)
    {
        using var context = new AppDbContext();
        var ordered = context.PinnedApps.OrderBy(item => item.OrderIndex).ToList();
        var target = ordered.FirstOrDefault(item =>
            string.Equals(BuildKey(item), BuildKey(app), StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return;

        PinnedAppOrdering.Move(ordered, target, newIndex);
        for (int index = 0; index < ordered.Count; index++)
            ordered[index].OrderIndex = index;
        context.SaveChanges();
    }

    internal static IEnumerable<string> SafeEnumerateShortcuts(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (string file in files)
                yield return file;

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                subdirectories = Array.Empty<string>();
            }

            foreach (string subdirectory in subdirectories)
                pending.Push(subdirectory);
        }
    }

    private static List<PinnedApp> LoadPinnedEntities()
    {
        using var context = new AppDbContext();
        return context.PinnedApps.OrderBy(item => item.OrderIndex).AsNoTracking().ToList();
    }

    private static AppLaunchItem ToLaunchItem(PinnedApp entity) => new()
    {
        DisplayName = entity.DisplayName,
        LaunchKind = entity.LaunchKind,
        LaunchTarget = entity.LaunchTarget,
        Arguments = entity.Arguments,
        IconKey = entity.IconKey,
        IdentityKey = BuildDeferredIdentity(entity),
        IsPinned = true
    };

    private void EnsureIdentity(AppLaunchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.IdentityKey))
            item.IdentityKey = _identityResolver.ResolveLaunch(item).Key;
    }

    private void BuildCatalog(
        CancellationTokenSource cancellation)
    {
        try
        {
            var candidates =
                new Dictionary<string, AppLaunchItem>(
                    StringComparer.OrdinalIgnoreCase);
            AddCandidates(
                candidates,
                _catalogSource.EnumerateStartMenuApps(),
                cancellation.Token);
            AddCandidates(
                candidates,
                _catalogSource.EnumerateShellApps(),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            IReadOnlyList<PinnedApp> pinnedApps;
            try
            {
                pinnedApps = _pinnedLoader();
            }
            catch
            {
                pinnedApps = Array.Empty<PinnedApp>();
            }

            foreach (PinnedApp pinned in pinnedApps)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                string key = BuildKey(pinned);
                if (candidates.ContainsKey(key))
                    continue;
                try
                {
                    AppLaunchItem app = ToLaunchItem(pinned);
                    EnsureIdentity(app);
                    candidates.TryAdd(key, app);
                }
                catch
                {
                    // A stale pin remains launchable through GetPinned,
                    // even if its identity can no longer be resolved.
                }
            }
            HashSet<string> pinnedKeys = pinnedApps
                .Select(BuildKey)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            List<AppLaunchItem> ordered = candidates.Values
                .OrderBy(
                    item => item.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            foreach (AppLaunchItem app in ordered)
                app.IsPinned = pinnedKeys.Contains(BuildKey(app));

            lock (_indexLock)
            {
                if (_disposed
                    || !ReferenceEquals(
                        _indexCancellation,
                        cancellation)
                    || cancellation.IsCancellationRequested)
                {
                    return;
                }
                lock (_catalogLock)
                {
                    _catalog.Clear();
                    _catalog.AddRange(ordered);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Keep the last valid catalog. The search surface will leave
            // its loading state instead of blocking the WPF dispatcher.
        }
        finally
        {
            bool notify = false;
            lock (_indexLock)
            {
                if (!_disposed
                    && ReferenceEquals(
                        _indexCancellation,
                        cancellation))
                {
                    _indexCancellation = null;
                    _isIndexing = false;
                    notify = true;
                }
            }
            cancellation.Dispose();
            if (notify)
                RaiseCatalogChanged();
        }
    }

    private void AddCandidates(
        IDictionary<string, AppLaunchItem> destination,
        IEnumerable<AppLaunchItem> candidates,
        CancellationToken cancellationToken)
    {
        foreach (AppLaunchItem candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureIdentity(candidate);
                destination.TryAdd(
                    BuildKey(candidate),
                    candidate);
            }
            catch
            {
                // A broken shortcut must not abort the remaining catalog.
            }
        }
    }

    private void QueueIconLoads(
        IEnumerable<AppLaunchItem> items)
    {
        bool startWorker = false;
        foreach (AppLaunchItem item in items)
        {
            if (item.Icon != null)
                continue;
            string key =
                item.IconKey ?? item.LaunchTarget;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            ImageSource? cached = null;
            lock (_iconLock)
            {
                if (_disposed)
                    return;
                if (_loadedIcons.TryGetValue(key, out cached))
                {
                    // Applied below on the caller's thread.
                }
                else if (!_failedIconKeys.Contains(key))
                {
                    if (!_iconWaiters.TryGetValue(
                            key,
                            out List<AppLaunchItem>? waiters))
                    {
                        waiters = new List<AppLaunchItem>();
                        _iconWaiters.Add(key, waiters);
                        _iconQueue.Enqueue(key);
                    }
                    if (!waiters.Contains(item))
                        waiters.Add(item);
                    if (!_iconWorkerRunning)
                    {
                        _iconWorkerRunning = true;
                        startWorker = true;
                    }
                }
            }

            if (cached != null)
                item.Icon = cached;
        }

        if (startWorker)
            StartIconWorker();
    }

    private void StartIconWorker()
    {
        var thread = new Thread(LoadQueuedIcons)
        {
            IsBackground = true,
            Name = "FocusPanel.AppIcons"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void LoadQueuedIcons()
    {
        while (true)
        {
            var batch = new List<string>(12);
            lock (_iconLock)
            {
                if (_disposed)
                {
                    _iconWorkerRunning = false;
                    return;
                }
                while (batch.Count < 12
                       && _iconQueue.Count > 0)
                {
                    batch.Add(_iconQueue.Dequeue());
                }
                if (batch.Count == 0)
                {
                    _iconWorkerRunning = false;
                    return;
                }
            }

            var loaded =
                new List<(
                    ImageSource Icon,
                    List<AppLaunchItem> Waiters)>();
            foreach (string key in batch)
            {
                ImageSource? icon = null;
                try
                {
                    icon = _iconSource.Load(key);
                    icon?.Freeze();
                }
                catch
                {
                    icon = null;
                }

                List<AppLaunchItem> waiters;
                lock (_iconLock)
                {
                    if (!_iconWaiters.Remove(
                            key,
                            out waiters!))
                    {
                        continue;
                    }
                    if (icon == null)
                        _failedIconKeys.Add(key);
                    else
                        _loadedIcons[key] = icon;
                }
                if (icon != null)
                    loaded.Add((icon, waiters));
            }

            if (loaded.Count == 0)
                continue;
            Dispatch(() =>
            {
                if (_disposed)
                    return;
                foreach (var result in loaded)
                {
                    foreach (AppLaunchItem item
                             in result.Waiters)
                    {
                        item.Icon = result.Icon;
                    }
                }
                CatalogChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            });
        }
    }

    private void RaiseCatalogChanged() =>
        Dispatch(() =>
        {
            if (!_disposed)
            {
                CatalogChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
        });

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null
            && !dispatcher.CheckAccess())
        {
            if (dispatcher.HasShutdownStarted
                || dispatcher.HasShutdownFinished)
            {
                return;
            }
            dispatcher.BeginInvoke(action);
            return;
        }
        action();
    }

    private static string BuildKey(AppLaunchItem item) =>
        $"{(int)item.LaunchKind}|{item.LaunchTarget}|{item.Arguments}";

    private static string BuildKey(PinnedApp item) =>
        $"{(int)item.LaunchKind}|{item.LaunchTarget}|{item.Arguments}";

    private static string BuildDeferredIdentity(
        PinnedApp item)
    {
        string? resolved = item.LaunchKind switch
        {
            AppLaunchKind.ShellApp =>
                AppIdentityResolver.BuildKey(
                    item.LaunchTarget,
                    null),
            AppLaunchKind.Executable =>
                AppIdentityResolver.BuildKey(
                    null,
                    item.LaunchTarget),
            _ => null
        };
        return resolved
            ?? $"launch:{(int)item.LaunchKind}:"
            + $"{item.LaunchTarget.Trim().ToLowerInvariant()}:"
            + $"{item.Arguments?.Trim().ToLowerInvariant() ?? string.Empty}";
    }

    public void Dispose()
    {
        lock (_indexLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _isIndexing = false;
            _indexCancellation?.Cancel();
            _indexCancellation = null;
        }
        lock (_iconLock)
        {
            _iconQueue.Clear();
            _iconWaiters.Clear();
        }
    }
}
