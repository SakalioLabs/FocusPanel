using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using FocusPanel.Data;
using FocusPanel.Helpers;
using FocusPanel.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusPanel.Services;

public sealed class AppCatalogService : IAppCatalogService
{
    private readonly List<AppLaunchItem> _catalog = new();
    private readonly object _catalogLock = new();
    private Thread? _shellIndexThread;

    public AppCatalogService()
    {
        Refresh();
        StartShellAppsIndex();
    }

    public event EventHandler? CatalogChanged;

    public void Refresh()
    {
        var candidates = new Dictionary<string, AppLaunchItem>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in GetStartMenuRoots())
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string shortcut in SafeEnumerateShortcuts(root))
            {
                string displayName = Path.GetFileNameWithoutExtension(shortcut);
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                candidates.TryAdd(
                    shortcut,
                    new AppLaunchItem
                    {
                        DisplayName = displayName,
                        LaunchKind = AppLaunchKind.Shortcut,
                        LaunchTarget = shortcut,
                        IconKey = shortcut
                    });
            }
        }

        var pinnedKeys = LoadPinnedEntities()
            .Select(BuildKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_catalogLock)
        {
            _catalog.Clear();
            foreach (var app in candidates.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                app.IsPinned = pinnedKeys.Contains(BuildKey(app));
                _catalog.Add(app);
            }
        }
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
        foreach (AppLaunchItem item in results)
            EnsureIcon(item);
        return results;
    }

    public IReadOnlyList<AppLaunchItem> GetPinned()
    {
        var entities = LoadPinnedEntities();
        return entities
            .OrderBy(item => item.OrderIndex)
            .Select(entity =>
            {
                AppLaunchItem? catalogItem;
                lock (_catalogLock)
                {
                    catalogItem = _catalog.FirstOrDefault(item =>
                        string.Equals(BuildKey(item), BuildKey(entity), StringComparison.OrdinalIgnoreCase));
                }
                if (catalogItem != null)
                    EnsureIcon(catalogItem);
                return catalogItem ?? ToLaunchItem(entity);
            })
            .ToList();
    }

    public void Launch(AppLaunchItem app)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = app.LaunchTarget,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(app.Arguments))
            startInfo.Arguments = app.Arguments;
        Process.Start(startInfo);
    }

    public void SetPinned(AppLaunchItem app, bool pinned)
    {
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

    private static IEnumerable<string> GetStartMenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
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

    private static IEnumerable<AppLaunchItem> EnumerateShellApps()
    {
        object? shellObject = null;
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                yield break;

            shellObject = Activator.CreateInstance(shellType);
            if (shellObject == null)
                yield break;

            dynamic shell = shellObject;
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            if (folder == null)
                yield break;

            foreach (dynamic item in folder.Items())
            {
                string name = item.Name as string ?? string.Empty;
                string path = item.Path as string ?? string.Empty;
                if (name.Length == 0 || path.Length == 0)
                    continue;

                yield return new AppLaunchItem
                {
                    DisplayName = name,
                    LaunchKind = AppLaunchKind.ShellApp,
                    LaunchTarget = path,
                    IconKey = path
                };
            }
        }
        finally
        {
            if (shellObject != null && System.Runtime.InteropServices.Marshal.IsComObject(shellObject))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shellObject);
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
        Icon = IconHelper.GetIcon(entity.IconKey ?? entity.LaunchTarget),
        IsPinned = true
    };

    private static void EnsureIcon(AppLaunchItem item)
    {
        if (item.Icon == null)
            item.Icon = IconHelper.GetIcon(item.IconKey ?? item.LaunchTarget);
    }

    private void StartShellAppsIndex()
    {
        _shellIndexThread = new Thread(() =>
        {
            List<AppLaunchItem> shellApps;
            try
            {
                shellApps = EnumerateShellApps().ToList();
            }
            catch
            {
                shellApps = new List<AppLaunchItem>();
            }

            if (shellApps.Count == 0)
                return;

            lock (_catalogLock)
            {
                var existing = _catalog
                    .Select(item => item.LaunchTarget)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (AppLaunchItem app in shellApps)
                {
                    if (existing.Add(app.LaunchTarget))
                        _catalog.Add(app);
                }
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
                CatalogChanged?.Invoke(this, EventArgs.Empty));
        })
        {
            IsBackground = true,
            Name = "FocusPanel.AppCatalog"
        };
        _shellIndexThread.SetApartmentState(ApartmentState.STA);
        _shellIndexThread.Start();
    }

    private static string BuildKey(AppLaunchItem item) =>
        $"{(int)item.LaunchKind}|{item.LaunchTarget}|{item.Arguments}";

    private static string BuildKey(PinnedApp item) =>
        $"{(int)item.LaunchKind}|{item.LaunchTarget}|{item.Arguments}";

    public void Dispose()
    {
        _shellIndexThread = null;
    }
}
