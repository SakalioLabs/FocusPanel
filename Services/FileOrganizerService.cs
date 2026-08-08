using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.Data;

namespace FocusPanel.Services;

public sealed class CommonDesktopElevationRequiredException : InvalidOperationException
{
    public CommonDesktopElevationRequiredException(string path)
        : base("公共桌面项目需要管理员授权。")
    {
        Path = path;
    }

    public string Path { get; }
}

public class FileOrganizerService : IDisposable
{
    private readonly string _desktopPath;
    private readonly string _commonDesktopPath;
    /// <summary>旧版本仓库，仅用于兼容已经被移动的项目。</summary>
    private readonly string _storageRoot;
    private readonly IDesktopItemVisibilityService _visibility;
    private readonly IDesktopVisibilityIo
        _visibilityIo;
    private readonly IAiDesktopPartitionService
        _aiPartitionService;
    private readonly IPanelIconStore _iconStore;
    private FileSystemWatcher _desktopWatcher = null!;
    private FileSystemWatcher? _commonDesktopWatcher;
    private FileSystemWatcher? _storageWatcher;
    private readonly DesktopChangeAccumulator _pendingChanges = new();
    private readonly DesktopCreatedPathSuppression
        _createdPathSuppression = new();
    private readonly System.Threading.Timer _debounceTimer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _disposed;

    /// <summary>All files: desktop visible + stored in .FocusPanel</summary>
    public ObservableCollection<DesktopFile> AllFiles { get; private set; } = new();

    /// <summary>Only desktop-root visible files (not stored)</summary>
    public ObservableCollection<DesktopFile> Files { get; private set; } = new();

    public event Action? FilesChanged;
    public event Func<IReadOnlyList<string>, Task>?
        DesktopItemsCreated;

    private readonly SemaphoreSlim _organizeGate = new(1, 1);
    private readonly SemaphoreSlim _visibilityGate = new(1, 1);
    private const int DebounceInterval = 500;

    public FileOrganizerService()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            new WindowsDesktopItemVisibilityService())
    {
    }

    internal FileOrganizerService(
        string desktopPath,
        IDesktopItemVisibilityService visibility)
        : this(desktopPath, "", visibility)
    {
    }

    internal FileOrganizerService(
        string desktopPath,
        string commonDesktopPath,
        IDesktopItemVisibilityService visibility,
        IAiDesktopPartitionService? aiPartitionService = null,
        IPanelIconStore? iconStore = null)
    {
        _desktopPath = desktopPath;
        _commonDesktopPath = commonDesktopPath;
        _storageRoot = Path.Combine(_desktopPath, ".FocusPanel");
        _visibility = visibility;
        _visibilityIo =
            new DesktopVisibilityIo(
                visibility);
        _aiPartitionService = aiPartitionService
            ?? new AiDesktopPartitionService();
        _iconStore = iconStore
            ?? new PanelIconStore();
        _debounceTimer = new System.Threading.Timer(
            _ => _ = ProcessPendingChangesSafelyAsync(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        InitializeWatchers();
        ScheduleFullRefresh();
    }

    public bool ShowsProtectedSystemFiles => _visibility.ShowsProtectedSystemFiles;

    private void InitializeWatchers()
    {
        // Watch desktop root for visible file changes
        _desktopWatcher = new FileSystemWatcher(_desktopPath)
        {
            NotifyFilter =
                NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Attributes
        };
        _desktopWatcher.Created += OnChanged;
        _desktopWatcher.Changed += OnChanged;
        _desktopWatcher.Deleted += OnChanged;
        _desktopWatcher.Renamed += OnRenamed;
        _desktopWatcher.Error += OnWatcherError;
        _desktopWatcher.EnableRaisingEvents = true;

        if (!string.IsNullOrWhiteSpace(_commonDesktopPath)
            && Directory.Exists(_commonDesktopPath)
            && !string.Equals(_desktopPath, _commonDesktopPath, StringComparison.OrdinalIgnoreCase))
        {
            _commonDesktopWatcher = new FileSystemWatcher(_commonDesktopPath)
            {
                NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Attributes
            };
            _commonDesktopWatcher.Created += OnChanged;
            _commonDesktopWatcher.Changed += OnChanged;
            _commonDesktopWatcher.Deleted += OnChanged;
            _commonDesktopWatcher.Renamed += OnRenamed;
            _commonDesktopWatcher.Error += OnWatcherError;
            _commonDesktopWatcher.EnableRaisingEvents = true;
        }

        // Watch storage folder for internal changes (moves between partitions, etc.)
        if (Directory.Exists(_storageRoot))
        {
            try
            {
                FileAttributes attributes = _visibility.GetAttributes(_storageRoot);
                FileAttributes collected = DesktopItemAttributePolicy.Collect(attributes);
                if (attributes != collected)
                    _visibility.SetAttributes(_storageRoot, collected);
                _visibility.NotifyAttributesChanged(_storageRoot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hide legacy storage container error: {ex.Message}");
            }

            _storageWatcher = new FileSystemWatcher(_storageRoot)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };
            _storageWatcher.Created += OnStorageChanged;
            _storageWatcher.Changed += OnStorageChanged;
            _storageWatcher.Deleted += OnStorageChanged;
            _storageWatcher.Renamed += OnStorageRenamed;
            _storageWatcher.Error += OnWatcherError;
            _storageWatcher.EnableRaisingEvents = true;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore changes inside the storage folder from the desktop watcher
        if (e.FullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase)) return;
        IconHelper.ClearCache(e.FullPath);
        bool isCreated =
            e.ChangeType == WatcherChangeTypes.Created
            && !_createdPathSuppression.TryConsume(
                e.FullPath,
                DateTimeOffset.UtcNow);
        SchedulePathRefresh(
            e.FullPath,
            isCreated);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (e.FullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase)) return;
        IconHelper.ClearCache(e.OldFullPath);
        IconHelper.ClearCache(e.FullPath);
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            string oldName = Path.GetFileName(e.OldFullPath);
            var pref = context.DesktopFilePreferences.FirstOrDefault(
                p => p.FilePath == oldName && p.IsHiddenFromDesktop);
            if (pref != null)
            {
                pref.FilePath = Path.GetFileName(e.FullPath);
                pref.ManagedPath = e.FullPath;
                pref.FileIdentity = _visibility.TryGetIdentity(e.FullPath) ?? pref.FileIdentity;
                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Track collected rename error: {ex.Message}");
        }
        _ = InvokeOnUiAsync(() =>
        {
            if (_disposed)
                return;

            DesktopFile? renamed = AllFiles
                .FirstOrDefault(item =>
                    string.Equals(
                        item.FullPath,
                        e.OldFullPath,
                        StringComparison.OrdinalIgnoreCase));
            if (renamed == null)
                return;

            renamed.Name =
                Path.GetFileName(e.FullPath);
            renamed.FullPath = e.FullPath;
        });
        ScheduleRenamedPathRefresh(
            e.OldFullPath,
            e.FullPath);
    }

    private void OnStorageChanged(
        object sender,
        FileSystemEventArgs e)
        => ScheduleFullRefresh();

    private void OnStorageRenamed(
        object sender,
        RenamedEventArgs e)
        => ScheduleFullRefresh();

    private void OnWatcherError(
        object sender,
        ErrorEventArgs e)
        => ScheduleFullRefresh();

    private void SchedulePathRefresh(
        string path,
        bool isCreated = false)
    {
        if (_disposed)
            return;

        _pendingChanges.AddPath(path, isCreated);
        RestartDebounceTimer();
    }

    private void ScheduleRenamedPathRefresh(
        string oldPath,
        string newPath)
    {
        if (_disposed)
            return;

        _pendingChanges.RenamePath(
            oldPath,
            newPath);
        RestartDebounceTimer();
    }

    private void ScheduleFullRefresh()
    {
        if (_disposed)
            return;

        _pendingChanges.RequireFullRefresh();
        RestartDebounceTimer();
    }

    private void RestartDebounceTimer()
    {
        try
        {
            _debounceTimer.Change(
                DebounceInterval,
                Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown raced the last watcher callback.
        }
    }

    private async Task ProcessPendingChangesSafelyAsync()
    {
        try
        {
            await ProcessPendingChangesAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Timer callbacks are deliberately fire-and-forget. Keep
            // one final observation boundary around the entire pump so
            // a shutdown race or an unexpected observer failure can
            // never terminate FocusPanel after collecting an item.
            new CrashLogService().TryAppend(
                new InvalidOperationException(
                    "Desktop change processing failed.",
                    ex));
        }
    }

    private async Task ProcessPendingChangesAsync()
    {
        if (_disposed)
            return;

        IReadOnlyList<string> createdPaths =
            Array.Empty<string>();
        await _refreshGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            DesktopChangeBatch batch =
                _pendingChanges.Take();
            if (batch.IsEmpty || _disposed)
                return;

            if (batch.RequiresFullRefresh)
                await RefreshFilesCore()
                    .ConfigureAwait(false);
            else
                await RefreshChangedPaths(batch.Paths)
                    .ConfigureAwait(false);

            if (!_disposed
                && batch.CreatedPaths.Count > 0)
            {
                createdPaths =
                    batch.CreatedPaths;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Process desktop changes error: {ex.Message}");
        }
        finally
        {
            _refreshGate.Release();
        }

        if (!_disposed
            && createdPaths.Count > 0)
        {
            await NotifyDesktopItemsCreatedAsync(
                    createdPaths)
                .ConfigureAwait(false);
        }
    }

    private async Task NotifyDesktopItemsCreatedAsync(
        IReadOnlyList<string> createdPaths)
    {
        Func<IReadOnlyList<string>, Task>?
            handlers =
                DesktopItemsCreated;
        if (handlers == null)
            return;

        foreach (Delegate handler
                 in handlers.GetInvocationList())
        {
            try
            {
                var callback =
                    (Func<IReadOnlyList<string>, Task>)
                        handler;
                await InvokeOnUiAsync(
                        () => callback(createdPaths))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Desktop created-item handler failed: "
                    + ex.Message);
            }
        }
    }

    private async Task RefreshChangedPaths(
        IReadOnlyList<string> changedPaths)
    {
        Dictionary<string, DesktopPreferenceSnapshot> preferences =
            await Task.Run(LoadDesktopPreferences);
        IReadOnlyList<DesktopItemRefresh> changes =
            await Task.Run(() => changedPaths
                .Select(path =>
                    ReadChangedPath(
                        path,
                        preferences))
                .Where(change => change != null)
                .Cast<DesktopItemRefresh>()
                .ToArray());

        if (_disposed)
            return;

        await InvokeOnUiAsync(() =>
        {
            if (_disposed)
                return;

            DesktopFileCollectionSynchronizer.Apply(
                AllFiles,
                Files,
                changes);
            if (changes.Count > 0)
                NotifyFilesChanged();
        });
    }

    private DesktopItemRefresh? ReadChangedPath(
        string changedPath,
        Dictionary<string, DesktopPreferenceSnapshot>
            preferences)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(changedPath);
        }
        catch
        {
            return null;
        }

        string? root = GetDesktopRoots()
            .FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetDirectoryName(fullPath),
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
        if (root == null)
            return null;

        string fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Equals(
                "desktop.ini",
                StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(
                ".FocusPanel",
                StringComparison.OrdinalIgnoreCase))
        {
            return new DesktopItemRefresh(
                fullPath,
                null,
                true);
        }

        string? currentPath = GetDesktopRoots()
            .Select(candidate =>
                Path.Combine(candidate, fileName))
            .FirstOrDefault(_visibility.Exists);
        if (currentPath == null)
        {
            if (preferences.TryGetValue(
                    fileName,
                    out DesktopPreferenceSnapshot? missing)
                && missing.IsHidden)
            {
                MarkRecoveryRequired(missing.Id);
                return new DesktopItemRefresh(
                    fullPath,
                    BuildRecoveryItem(missing),
                    false);
            }

            return new DesktopItemRefresh(
                fullPath,
                null,
                true);
        }

        try
        {
            FileAttributes attributes =
                _visibility.GetAttributes(currentPath);
            DesktopPreferenceSnapshot? preference =
                ResolvePreference(
                    fileName,
                    currentPath,
                    attributes,
                    preferences);
            bool isCollected =
                preference?.IsHidden ?? false;
            if (isCollected)
            {
                FileAttributes collected =
                    DesktopItemAttributePolicy.Collect(
                        attributes);
                if (attributes != collected)
                {
                    _visibility.SetAttributes(
                        currentPath,
                        collected);
                    _visibility.NotifyAttributesChanged(
                        currentPath);
                    attributes = collected;
                }
            }
            else if (IsSystemHidden(attributes))
            {
                return new DesktopItemRefresh(
                    fullPath,
                    null,
                    true);
            }

            DesktopFile item = BuildDesktopFileFromPath(
                currentPath,
                isCollected,
                preference,
                0);
            return new DesktopItemRefresh(
                fullPath,
                item,
                false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Refresh desktop item {currentPath} error: "
                + ex.Message);
            return null;
        }
    }

    private DesktopFile BuildDesktopFileFromPath(
        string fullPath,
        bool isCollected,
        DesktopPreferenceSnapshot? preference,
        int index)
    {
        DesktopFile item;
        if (File.Exists(fullPath))
        {
            var file = new FileInfo(fullPath);
            item = BuildDesktopFile(
                file.FullName,
                file.Name,
                file.Extension,
                file.Length,
                file.CreationTime,
                ClassifyFile(file),
                isCollected,
                preference?.X,
                preference?.Y,
                index);
        }
        else
        {
            var directory = new DirectoryInfo(fullPath);
            item = BuildDesktopFile(
                directory.FullName,
                directory.Name,
                string.Empty,
                0,
                directory.CreationTime,
                "Folder",
                isCollected,
                preference?.X,
                preference?.Y,
                index);
        }

        try
        {
            item.CustomIconPath =
                preference?.CustomIconPath;
            item.CustomIconIndex =
                preference?.CustomIconIndex ?? 0;
            item.Icon = IconHelper.GetIcon(
                fullPath,
                item.CustomIconPath,
                item.CustomIconIndex,
                true);
        }
        catch
        {
            // The shell may still be committing a newly created item.
        }
        return item;
    }

    private DesktopFile BuildRecoveryItem(
        DesktopPreferenceSnapshot preference)
    {
        var item = new DesktopFile
        {
            Name = preference.FileName,
            FullPath = preference.ManagedPath
                ?? Path.Combine(
                    _desktopPath,
                    preference.FileName),
            Extension =
                Path.GetExtension(preference.FileName),
            FileType = "Recovery",
            CreatedAt = DateTime.Now,
            IsHidden = true,
            NeedsRecovery = true,
            DesktopX = preference.X ?? 16,
            DesktopY = preference.Y ?? 16,
            CustomIconPath = preference.CustomIconPath,
            CustomIconIndex = preference.CustomIconIndex ?? 0
        };
        try
        {
            item.Icon = IconHelper.GetIcon(
                item.FullPath,
                item.CustomIconPath,
                item.CustomIconIndex,
                true);
        }
        catch
        {
            // Keep the recovery action available even if its icon source is
            // temporarily unavailable.
        }
        return item;
    }

    private static void MarkRecoveryRequired(int preferenceId)
    {
        try
        {
            using var context = new AppDbContext();
            DesktopFilePreference? preference =
                context.DesktopFilePreferences.Find(
                    preferenceId);
            if (preference == null
                || preference.OperationState
                    == DesktopVisibilityOperation
                        .RecoveryRequired)
            {
                return;
            }

            preference.OperationState =
                DesktopVisibilityOperation.RecoveryRequired;
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Mark desktop recovery required error: "
                + ex.Message);
        }
    }

    private static Task InvokeOnUiAsync(
        Action action)
    {
        Application? application =
            Application.Current;
        if (application == null)
        {
            action();
            return Task.CompletedTask;
        }

        System.Windows.Threading.Dispatcher dispatcher =
            application.Dispatcher;
        if (dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return Task.CompletedTask;
        }
        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher
            .InvokeAsync(action)
            .Task;
    }

    private static Task InvokeOnUiAsync(
        Func<Task> action)
    {
        Application? application =
            Application.Current;
        if (application == null)
            return action();

        System.Windows.Threading.Dispatcher dispatcher =
            application.Dispatcher;
        if (dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return Task.CompletedTask;
        }
        if (dispatcher.CheckAccess())
            return action();

        return dispatcher
            .InvokeAsync(action)
            .Task
            .Unwrap();
    }

    private sealed record DesktopPreferenceSnapshot(
        int Id,
        string FileName,
        string PartitionName,
        bool IsHidden,
        double? X,
        double? Y,
        string? ManagedPath,
        long? OriginalAttributes,
        string? FileIdentity,
        string? CustomIconPath,
        int? CustomIconIndex,
        DesktopCollectionMode CollectionMode,
        DesktopVisibilityOperation OperationState);

    private Dictionary<string, DesktopPreferenceSnapshot> LoadDesktopPreferences()
    {
        var map = new Dictionary<string, DesktopPreferenceSnapshot>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            foreach (var pref in context.DesktopFilePreferences)
                map[pref.FilePath] = new DesktopPreferenceSnapshot(
                    pref.Id,
                    pref.FilePath,
                    pref.PartitionName ?? "",
                    pref.IsHiddenFromDesktop,
                    pref.DesktopX,
                    pref.DesktopY,
                    pref.ManagedPath,
                    pref.OriginalAttributes,
                    pref.FileIdentity,
                    pref.CustomIconPath,
                    pref.CustomIconIndex,
                    pref.CollectionMode,
                    pref.OperationState);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadDesktopPreferences error: {ex.Message}");
        }
        return map;
    }

    public async Task RefreshFiles()
    {
        if (_disposed)
            return;

        await _refreshGate.WaitAsync()
            .ConfigureAwait(false);
        try
        {
            if (!_disposed)
            {
                await RefreshFilesCore()
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshFilesCore()
    {
        try
        {
            await Task.Run(ReconcileVisibilityState);
            var preferences = await Task.Run(() => LoadDesktopPreferences());

            var files = await Task.Run(() =>
            {
                var fileList = new List<DesktopFile>();
                var desktopRootFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Windows merges the per-user and public desktop roots into one view.
                foreach (string desktopRoot in GetDesktopRoots())
                {
                    foreach (var file in new DirectoryInfo(desktopRoot).GetFiles())
                    {
                        if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        var pref = ResolvePreference(file.Name, file.FullName, file.Attributes, preferences);
                        bool isCollected = pref?.IsHidden ?? false;
                        if (!isCollected && IsSystemHidden(file.Attributes)) continue;
                        desktopRootFiles.Add(file.Name);

                        fileList.Add(BuildDesktopFileFromPath(
                            file.FullName,
                            isCollected,
                            pref,
                            fileList.Count));
                    }

                    foreach (var dir in new DirectoryInfo(desktopRoot).GetDirectories())
                    {
                        // Skip our storage folder
                        if (dir.Name.Equals(".FocusPanel", StringComparison.OrdinalIgnoreCase)) continue;
                        var pref = ResolvePreference(dir.Name, dir.FullName, dir.Attributes, preferences);
                        bool isCollected = pref?.IsHidden ?? false;
                        if (!isCollected && IsSystemHidden(dir.Attributes)) continue;
                        desktopRootFiles.Add(dir.Name);

                        fileList.Add(BuildDesktopFileFromPath(
                            dir.FullName,
                            isCollected,
                            pref,
                            fileList.Count));
                    }
                }

                // 2. Scan storage folder — files here are "收纳" (hidden)
                if (Directory.Exists(_storageRoot))
                {
                    foreach (var partitionDir in Directory.GetDirectories(_storageRoot))
                    {
                        string partitionName = Path.GetFileName(partitionDir);

                        foreach (var filePath in Directory.GetFiles(partitionDir))
                        {
                            var fi = new FileInfo(filePath);
                            // If a file with same name is on desktop root, skip (desktop version wins)
                            if (desktopRootFiles.Contains(fi.Name)) continue;

                            preferences.TryGetValue(fi.Name, out var pref);
                            fileList.Add(BuildDesktopFileFromPath(
                                fi.FullName,
                                true,
                                pref,
                                fileList.Count));
                        }

                        foreach (var dirPath in Directory.GetDirectories(partitionDir))
                        {
                            var di = new DirectoryInfo(dirPath);
                            if (desktopRootFiles.Contains(di.Name)) continue;

                            preferences.TryGetValue(di.Name, out var pref);
                            fileList.Add(BuildDesktopFileFromPath(
                                di.FullName,
                                true,
                                pref,
                                fileList.Count));
                        }
                    }
                }

                // 保留无法定位项目的恢复入口，绝不静默删除数据库记录。
                foreach (var pref in preferences.Values.Where(
                    p => p.IsHidden
                        && p.OperationState == DesktopVisibilityOperation.RecoveryRequired
                        && !fileList.Any(f => string.Equals(
                            f.Name,
                            p.FileName,
                            StringComparison.OrdinalIgnoreCase))))
                {
                    fileList.Add(BuildRecoveryItem(pref));
                }

                return fileList.OrderByDescending(f => f.FileType == "Folder").ThenBy(f => f.Name).ToList();
            });

            if (_disposed)
                return;

            await InvokeOnUiAsync(() =>
            {
                if (_disposed)
                    return;

                UpdateFilesIncremental(files);
                NotifyFilesChanged();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshFiles error: {ex.Message}");
        }
    }

    private IEnumerable<string> GetDesktopRoots()
    {
        yield return _desktopPath;
        if (!string.IsNullOrWhiteSpace(_commonDesktopPath)
            && Directory.Exists(_commonDesktopPath)
            && !string.Equals(_desktopPath, _commonDesktopPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return _commonDesktopPath;
        }
    }

    private DesktopPreferenceSnapshot? ResolvePreference(
        string fileName,
        string fullPath,
        FileAttributes attributes,
        Dictionary<string, DesktopPreferenceSnapshot> preferences)
    {
        if (preferences.TryGetValue(fileName, out var direct))
            return direct;
        if (!IsSystemHidden(attributes))
            return null;

        try
        {
            string? identity = _visibility.TryGetIdentity(fullPath);
            if (string.IsNullOrWhiteSpace(identity))
                return null;

            DesktopPreferenceSnapshot? matched = preferences.Values.FirstOrDefault(
                p => p.IsHidden
                    && p.CollectionMode == DesktopCollectionMode.Attribute
                    && string.Equals(p.FileIdentity, identity, StringComparison.OrdinalIgnoreCase));
            if (matched == null)
                return null;

            using var context = new AppDbContext();
            var pref = context.DesktopFilePreferences.Find(matched.Id);
            if (pref != null)
            {
                pref.FilePath = fileName;
                pref.ManagedPath = fullPath;
                context.SaveChanges();
            }
            return matched with { FileName = fileName, ManagedPath = fullPath };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resolve collected identity error: {ex.Message}");
            return null;
        }
    }

    private void ReconcileVisibilityState()
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        var collected = context.DesktopFilePreferences.Where(p => p.IsHiddenFromDesktop).ToList();

        List<DesktopFilePreference> protectedLaunchers =
            collected.Where(pref =>
                    DesktopAutoOrganizePolicy
                        .IsProtectedPanelLauncher(
                            pref.FilePath,
                            pref.ManagedPath,
                            Environment.ProcessPath))
                .ToList();
        if (protectedLaunchers.Count > 0)
        {
            // Repair only the launcher. Restoring the complete stable
            // collection makes unrelated icons reappear and loses the
            // user's layout when the watcher organizes them again.
            foreach (DesktopFilePreference pref
                     in protectedLaunchers.Where(pref =>
                         pref.CollectionMode
                         == DesktopCollectionMode.Attribute))
            {
                try
                {
                    string path = pref.ManagedPath
                        ?? Path.Combine(
                            _desktopPath,
                            pref.FilePath);
                    if (!_visibility.Exists(path))
                    {
                        pref.OperationState =
                            DesktopVisibilityOperation
                                .RecoveryRequired;
                        continue;
                    }

                    long original = pref.OriginalAttributes
                        ?? (long)(_visibility.GetAttributes(path)
                            & ~FileAttributes.Hidden
                            & ~FileAttributes.System);
                    _visibility.SetAttributes(
                        path,
                        DesktopItemAttributePolicy
                            .Restore(original));
                    _visibility.NotifyAttributesChanged(path);
                    pref.IsHiddenFromDesktop = false;
                    pref.PartitionName = string.Empty;
                    pref.CollectionMode =
                        DesktopCollectionMode.None;
                    pref.OperationState =
                        DesktopVisibilityOperation.Stable;
                    pref.OriginalAttributes = null;
                }
                catch (Exception ex)
                {
                    pref.OperationState =
                        DesktopVisibilityOperation
                            .RecoveryRequired;
                    System.Diagnostics.Debug.WriteLine(
                        "Restore poisoned desktop batch error: "
                        + ex.Message);
                }
            }

            context.SaveChanges();
            collected = context.DesktopFilePreferences
                .Where(pref => pref.IsHiddenFromDesktop)
                .ToList();
        }

        foreach (var pref in collected)
        {
            try
            {
                string desktopPath = Path.Combine(_desktopPath, pref.FilePath);
                string legacyPath = GetLegacyPath(pref);

                if (pref.CollectionMode == DesktopCollectionMode.None)
                {
                    if (_visibility.Exists(desktopPath))
                    {
                        FileAttributes original = _visibility.GetAttributes(desktopPath);
                        pref.ManagedPath = desktopPath;
                        pref.OriginalAttributes = (long)original;
                        pref.FileIdentity = _visibility.TryGetIdentity(desktopPath);
                        pref.CollectionMode = DesktopCollectionMode.Attribute;
                        pref.OperationState = DesktopVisibilityOperation.Collecting;
                    }
                    else if (_visibility.Exists(legacyPath))
                    {
                        pref.ManagedPath = legacyPath;
                        pref.CollectionMode = DesktopCollectionMode.LegacyStorage;
                        pref.OperationState = DesktopVisibilityOperation.Stable;
                        continue;
                    }
                }

                if (pref.CollectionMode == DesktopCollectionMode.LegacyStorage)
                {
                    if (string.IsNullOrWhiteSpace(pref.ManagedPath) && _visibility.Exists(legacyPath))
                        pref.ManagedPath = legacyPath;
                    if (!_visibility.Exists(pref.ManagedPath ?? legacyPath))
                        pref.OperationState = DesktopVisibilityOperation.RecoveryRequired;
                    continue;
                }

                string managedPath = pref.ManagedPath ?? desktopPath;
                if (!_visibility.Exists(managedPath))
                {
                    pref.OperationState = DesktopVisibilityOperation.RecoveryRequired;
                    continue;
                }

                if (pref.OperationState == DesktopVisibilityOperation.Restoring)
                {
                    long original = pref.OriginalAttributes
                        ?? (long)(_visibility.GetAttributes(managedPath)
                            & ~FileAttributes.Hidden
                            & ~FileAttributes.System);
                    _visibility.SetAttributes(managedPath, DesktopItemAttributePolicy.Restore(original));
                    _visibility.NotifyAttributesChanged(managedPath);
                    pref.IsHiddenFromDesktop = false;
                    pref.OperationState = DesktopVisibilityOperation.Stable;
                    pref.CollectionMode = DesktopCollectionMode.None;
                    pref.OriginalAttributes = null;
                    continue;
                }

                FileAttributes current = _visibility.GetAttributes(managedPath);
                FileAttributes collectedAttributes = DesktopItemAttributePolicy.Collect(current);
                if (current != collectedAttributes)
                    _visibility.SetAttributes(managedPath, collectedAttributes);
                _visibility.NotifyAttributesChanged(managedPath);
                pref.FileIdentity = _visibility.TryGetIdentity(managedPath) ?? pref.FileIdentity;
                pref.OperationState = DesktopVisibilityOperation.Stable;
            }
            catch (Exception ex)
            {
                pref.OperationState = DesktopVisibilityOperation.RecoveryRequired;
                System.Diagnostics.Debug.WriteLine($"Reconcile collected item error: {ex.Message}");
            }
        }

        context.SaveChanges();
    }

    private string GetLegacyPath(DesktopFilePreference pref)
        => Path.Combine(
            _storageRoot,
            string.IsNullOrEmpty(pref.PartitionName) ? "Unsorted" : pref.PartitionName,
            pref.FilePath);

    private static DesktopFile BuildDesktopFile(string fullPath, string name, string ext,
        long size, DateTime created, string fileType, bool isHidden, double? desktopX, double? desktopY, int index)
    {
        double fallbackX = 16 + (index / 7) * 104;
        double fallbackY = 16 + (index % 7) * 112;

        return new DesktopFile
        {
            Name = name,
            FullPath = fullPath,
            Extension = ext,
            Size = size,
            CreatedAt = created,
            FileType = fileType,
            IsHidden = isHidden,
            DesktopX = desktopX ?? fallbackX,
            DesktopY = desktopY ?? fallbackY
        };
    }

    private void UpdateFilesIncremental(List<DesktopFile> newFiles)
    {
        var newFileMap = new Dictionary<string, DesktopFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in newFiles) newFileMap[f.Name] = f;

        for (int i = AllFiles.Count - 1; i >= 0; i--)
        {
            var existing = AllFiles[i];
            if (newFileMap.TryGetValue(existing.Name, out var newFile))
            {
                DesktopFileCollectionSynchronizer
                    .CopyState(
                        existing,
                        newFile);
                newFileMap.Remove(existing.Name);
            }
            else
            {
                AllFiles.RemoveAt(i);
            }
        }

        foreach (var newFile in newFileMap.Values)
            AllFiles.Add(newFile);

        // Files = only visible (desktop-root, not stored)
        var visibleFiles = AllFiles.Where(f => !f.IsHidden).ToList();
        for (int i = Files.Count - 1; i >= 0; i--)
        {
            if (!visibleFiles.Any(v =>
                    string.Equals(
                        v.Name,
                        Files[i].Name,
                        StringComparison.OrdinalIgnoreCase)))
                Files.RemoveAt(i);
        }
        foreach (var vf in visibleFiles)
        {
            if (!Files.Any(f =>
                    string.Equals(
                        f.Name,
                        vf.Name,
                        StringComparison.OrdinalIgnoreCase)))
                Files.Add(vf);
        }
        DesktopFileCollectionSynchronizer.Sort(
            AllFiles);
        DesktopFileCollectionSynchronizer.Sort(
            Files);
    }

    // ============================================================
    // 收纳：实体文件留在桌面，仅追加 Hidden + System 属性
    // ============================================================
    public async Task HideFileFromDesktop(string fileName, string partitionName)
    {
        DesktopFile? existingFile = AllFiles.FirstOrDefault(
            f => string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase));
        if (existingFile?.IsHidden == true)
        {
            await MoveToPartition(fileName, partitionName);
            return;
        }

        string fullPath = existingFile?.FullPath
            ?? Path.Combine(_desktopPath, fileName);
        await HideFileFromDesktopPath(fullPath, partitionName);
    }

    public async Task HideFileFromDesktopPath(
        string fullPath,
        string partitionName,
        bool allowCommonDesktopElevation = false)
    {
        await RunVisibilityMutationAsync(
            () =>
                HideFileFromDesktopPathCore(
                    fullPath,
                    partitionName,
                    allowCommonDesktopElevation,
                    true));
    }

    private async Task HideFileFromDesktopPathCore(
        string fullPath,
        string partitionName,
        bool allowCommonDesktopElevation,
        bool updateUi)
    {
        fullPath = Path.GetFullPath(fullPath);
        DesktopDropLocation location = DesktopDropPolicy.Classify(
            fullPath,
            _desktopPath,
            _commonDesktopPath);
        if (location == DesktopDropLocation.OutsideDesktop)
            throw new InvalidOperationException("该项目不在用户桌面或公共桌面根目录。");
        if (location == DesktopDropLocation.CommonDesktop && !allowCommonDesktopElevation)
            throw new CommonDesktopElevationRequiredException(fullPath);

        string fileName = Path.GetFileName(fullPath);
        DesktopFile? existingFile = AllFiles.FirstOrDefault(
            f => string.Equals(f.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existingFile?.IsHidden == true)
        {
            await MoveToPartition(fileName, partitionName);
            return;
        }

        FileAttributes originalAttributes =
            await _visibilityIo
                .ReadAttributesAsync(fullPath)
                .ConfigureAwait(false);
        string? fileIdentity = null;
        try
        {
            fileIdentity = _visibility.TryGetIdentity(
                fullPath);
        }
        catch
        {
            // The managed path remains authoritative when the file system
            // cannot provide a stable identity.
        }
        string? detectedIconPath = null;
        int? detectedIconIndex = null;
        if (IconHelper.TryResolveCustomIconLocation(
                fullPath,
                out string resolvedIconPath,
                out int resolvedIconIndex))
        {
            detectedIconPath = resolvedIconPath;
            detectedIconIndex = resolvedIconIndex;
        }
        detectedIconPath = await PreserveStandaloneIconAsync(
                detectedIconPath)
            .ConfigureAwait(false);
        int preferenceId = 0;

        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            List<DesktopFilePreference> preferences =
                context.DesktopFilePreferences.ToList();
            var pref = DesktopIconPreferenceSelector.Select(
                preferences,
                fullPath,
                fileName,
                fileIdentity);
            if (pref == null)
            {
                pref = new DesktopFilePreference { FilePath = fileName, PartitionName = "" };
                context.DesktopFilePreferences.Add(pref);
            }
            pref.FilePath = fileName;
            pref.PartitionName = partitionName;
            pref.IsHiddenFromDesktop = true;
            pref.ManagedPath = fullPath;
            pref.OriginalAttributes ??= (long)originalAttributes;
            pref.FileIdentity = fileIdentity;
            if (!string.IsNullOrWhiteSpace(detectedIconPath)
                && (string.IsNullOrWhiteSpace(pref.CustomIconPath)
                    || !File.Exists(pref.CustomIconPath)))
            {
                pref.CustomIconPath = detectedIconPath;
                pref.CustomIconIndex = detectedIconIndex;
            }
            pref.CollectionMode = DesktopCollectionMode.Attribute;
            pref.OperationState = DesktopVisibilityOperation.Collecting;

            if (!string.IsNullOrEmpty(partitionName) && !context.DesktopPartitions.Any(dp => dp.Name == partitionName))
            {
                int maxOrder = context.DesktopPartitions.Any() ? context.DesktopPartitions.Max(dp => dp.OrderIndex) : -1;
                context.DesktopPartitions.Add(new DesktopPartition { Name = partitionName, OrderIndex = maxOrder + 1 });
            }
            context.SaveChanges();
            preferenceId = pref.Id;
        });

        try
        {
            FileAttributes collectedAttributes = DesktopItemAttributePolicy.Collect(originalAttributes);
            await _visibilityIo
                .ApplyAttributesAsync(
                    fullPath,
                    collectedAttributes,
                    location
                    == DesktopDropLocation
                        .CommonDesktop)
                .ConfigureAwait(false);

            await Task.Run(() =>
            {
                using var context = new AppDbContext();
                var pref = context.DesktopFilePreferences.Find(preferenceId);
                if (pref != null)
                {
                    pref.OperationState = DesktopVisibilityOperation.Stable;
                    context.SaveChanges();
                }
            });
        }
        catch
        {
            bool attributesRestored = false;
            try
            {
                await _visibilityIo
                    .ApplyAttributesAsync(
                        fullPath,
                        originalAttributes,
                        location
                        == DesktopDropLocation
                            .CommonDesktop)
                    .ConfigureAwait(false);
                attributesRestored = true;
            }
            catch { }
            await Task.Run(() =>
            {
                using var context = new AppDbContext();
                var pref = context.DesktopFilePreferences.Find(preferenceId);
                if (pref != null)
                {
                    pref.IsHiddenFromDesktop = !attributesRestored;
                    pref.CollectionMode = attributesRestored
                        ? DesktopCollectionMode.None
                        : DesktopCollectionMode.Attribute;
                    pref.OperationState = attributesRestored
                        ? DesktopVisibilityOperation.Stable
                        : DesktopVisibilityOperation.RecoveryRequired;
                    if (attributesRestored)
                        pref.OriginalAttributes = null;
                    context.SaveChanges();
                }
            });
            throw;
        }

        if (updateUi)
        {
            await InvokeOnUiAsync(() =>
            {
                if (_disposed)
                    return;

                if (AllFiles.FirstOrDefault(
                        f => f.Name == fileName)
                    is DesktopFile file)
                {
                    file.IsHidden = true;
                    file.FullPath = fullPath;
                }

                DesktopFile? visibleFile =
                    Files.FirstOrDefault(
                        f => f.Name == fileName);
                if (visibleFile != null)
                    Files.Remove(visibleFile);
                NotifyFilesChanged();
            }).ConfigureAwait(false);
        }

        IconHelper.ClearCache(fullPath);
    }

    private async Task<string?> PreserveStandaloneIconAsync(
        string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath)
            || !Path.GetExtension(iconPath).Equals(
                ".ico",
                StringComparison.OrdinalIgnoreCase)
            || !File.Exists(iconPath))
        {
            return iconPath;
        }

        try
        {
            return await _iconStore.ImportAsync(
                    Path.GetFullPath(iconPath))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Preserve desktop custom icon error: "
                + ex.Message);
            return iconPath;
        }
    }

    public async Task<string?> SetCustomIcon(
        DesktopFile file,
        string? iconPath)
    {
        ArgumentNullException.ThrowIfNull(file);
        string? normalized = string.IsNullOrWhiteSpace(iconPath)
            ? null
            : await _iconStore.ImportAsync(
                    Path.GetFullPath(iconPath))
                .ConfigureAwait(false);

        string? identity = null;
        try
        {
            identity = _visibility.TryGetIdentity(
                file.FullPath);
        }
        catch
        {
            // Managed path remains the primary key when identity is unavailable.
        }

        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            List<DesktopFilePreference> preferences =
                context.DesktopFilePreferences.ToList();
            DesktopFilePreference? preference =
                DesktopIconPreferenceSelector.Select(
                    preferences,
                    file.FullPath,
                    file.Name,
                    identity);
            if (preference == null)
            {
                preference = new DesktopFilePreference
                {
                    FilePath = file.Name,
                    PartitionName =
                        file.CustomPartition ?? string.Empty,
                    ManagedPath = file.FullPath,
                    FileIdentity = identity
                };
                context.DesktopFilePreferences.Add(
                    preference);
            }

            preference.CustomIconPath = normalized;
            preference.CustomIconIndex = normalized == null
                ? null
                : 0;
            context.SaveChanges();
        }).ConfigureAwait(false);

        IconHelper.ClearCache(file.FullPath);
        if (normalized != null)
            IconHelper.ClearCache(normalized);
        file.CustomIconPath = normalized;
        file.CustomIconIndex = 0;
        return normalized;
    }

    // ============================================================
    // 取消收纳：属性模式恢复原属性；旧仓库模式恢复到桌面
    // ============================================================
    public async Task RestoreFileToDesktop(string fileName, double? desktopX = null, double? desktopY = null)
    {
        await RunVisibilityMutationAsync(
            () =>
                RestoreFileToDesktopCore(
                    fileName,
                    desktopX,
                    desktopY));
    }

    private async Task RestoreFileToDesktopCore(
        string fileName,
        double? desktopX,
        double? desktopY)
    {
        string restoredPath = Path.Combine(_desktopPath, fileName);

        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == fileName);
            if (pref == null)
                return;

            if (pref.CollectionMode == DesktopCollectionMode.LegacyStorage)
            {
                string srcPath = pref.ManagedPath ?? GetLegacyPath(pref);
                string destPath = Path.Combine(_desktopPath, fileName);

                if (_visibility.Exists(srcPath) && _visibility.Exists(destPath))
                {
                    string name = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    int count = 1;
                    while (_visibility.Exists(destPath))
                        destPath = Path.Combine(_desktopPath, $"{name} ({count++}){ext}");
                }

                if (File.Exists(srcPath))
                {
                    _createdPathSuppression.Suppress(
                        destPath,
                        DateTimeOffset.UtcNow);
                    File.Move(srcPath, destPath);
                }
                else if (Directory.Exists(srcPath))
                {
                    _createdPathSuppression.Suppress(
                        destPath,
                        DateTimeOffset.UtcNow);
                    Directory.Move(srcPath, destPath);
                }
                else
                    throw new FileNotFoundException("找不到旧版收纳项目。", srcPath);

                restoredPath = destPath;
            }
            else
            {
                string managedPath = pref.ManagedPath ?? restoredPath;
                if (!_visibility.Exists(managedPath))
                {
                    pref.OperationState = DesktopVisibilityOperation.RecoveryRequired;
                    context.SaveChanges();
                    throw new FileNotFoundException("找不到要恢复的桌面项目。", managedPath);
                }

                pref.OperationState = DesktopVisibilityOperation.Restoring;
                context.SaveChanges();

                long original = pref.OriginalAttributes
                    ?? (long)(_visibility.GetAttributes(managedPath)
                        & ~FileAttributes.Hidden
                        & ~FileAttributes.System);
                FileAttributes restoredAttributes = DesktopItemAttributePolicy.Restore(original);
                if (DesktopDropPolicy.Classify(
                        managedPath,
                        _desktopPath,
                        _commonDesktopPath) == DesktopDropLocation.CommonDesktop)
                {
                    DesktopVisibilityElevatedHelper.SetAttributes(managedPath, restoredAttributes);
                }
                else
                {
                    _visibility.SetAttributes(managedPath, restoredAttributes);
                    _visibility.NotifyAttributesChanged(managedPath);
                }
                restoredPath = managedPath;
            }

            pref.FilePath = Path.GetFileName(restoredPath);
            pref.ManagedPath = restoredPath;
            pref.PartitionName = string.Empty;
            pref.IsHiddenFromDesktop = false;
            pref.CollectionMode = DesktopCollectionMode.None;
            pref.OperationState = DesktopVisibilityOperation.Stable;
            pref.OriginalAttributes = null;
            if (desktopX.HasValue) pref.DesktopX = desktopX.Value;
            if (desktopY.HasValue) pref.DesktopY = desktopY.Value;
            context.SaveChanges();
        });

        await InvokeOnUiAsync(() =>
        {
            if (_disposed)
                return;

            DesktopFile? restoredFile =
                AllFiles.FirstOrDefault(
                    f => f.Name == fileName);
            if (restoredFile != null)
            {
                restoredFile.IsHidden = false;
                restoredFile.Name =
                    Path.GetFileName(restoredPath);
                restoredFile.FullPath = restoredPath;
                if (desktopX.HasValue)
                    restoredFile.DesktopX =
                        desktopX.Value;
                if (desktopY.HasValue)
                    restoredFile.DesktopY =
                        desktopY.Value;
            }

            if (!Files.Any(
                    f => f.Name == fileName)
                && restoredFile != null)
            {
                Files.Add(restoredFile);
            }
            NotifyFilesChanged();
        }).ConfigureAwait(false);
    }

    private async Task RunVisibilityMutationAsync(
        Func<Task> operation)
    {
        await _visibilityGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(FileOrganizerService));
            }

            await operation();
        }
        finally
        {
            _visibilityGate.Release();
        }
    }

    public async Task SaveDesktopPosition(string fileName, double desktopX, double desktopY)
    {
        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == fileName);
            if (pref == null)
            {
                pref = new DesktopFilePreference { FilePath = fileName, PartitionName = "" };
                context.DesktopFilePreferences.Add(pref);
            }

            pref.DesktopX = desktopX;
            pref.DesktopY = desktopY;
            context.SaveChanges();
        });

        if (AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile allFile)
        {
            allFile.DesktopX = desktopX;
            allFile.DesktopY = desktopY;
        }

        if (Files.FirstOrDefault(f => f.Name == fileName) is DesktopFile visibleFile)
        {
            visibleFile.DesktopX = desktopX;
            visibleFile.DesktopY = desktopY;
        }
    }

    // 分区只属于 FocusPanel 元数据，不移动实体文件。
    public async Task MoveToPartition(string fileName, string newPartitionName)
    {
        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == fileName);

            if (pref == null)
            {
                pref = new DesktopFilePreference { FilePath = fileName, PartitionName = "" };
                context.DesktopFilePreferences.Add(pref);
            }
            pref.PartitionName = newPartitionName;

            if (!string.IsNullOrEmpty(newPartitionName) && !context.DesktopPartitions.Any(dp => dp.Name == newPartitionName))
            {
                int maxOrder = context.DesktopPartitions.Any() ? context.DesktopPartitions.Max(dp => dp.OrderIndex) : -1;
                context.DesktopPartitions.Add(new DesktopPartition { Name = newPartitionName, OrderIndex = maxOrder + 1 });
            }
            context.SaveChanges();
        });

        NotifyFilesChanged();
    }

    private void NotifyFilesChanged()
    {
        ObserverIsolation.Notify(
            FilesChanged,
            ex =>
                System.Diagnostics.Debug.WriteLine(
                    "Desktop file observer failed: "
                    + ex.Message));
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static string ClassifyFile(FileInfo file)
    {
        string ext = file.Extension.ToLower();
        if (new[] { ".jpg", ".png", ".gif", ".bmp", ".jpeg", ".svg", ".webp" }.Contains(ext)) return "Image";
        if (new[] { ".doc", ".docx", ".pdf", ".txt", ".md", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx" }.Contains(ext)) return "Document";
        if (new[] { ".exe", ".lnk", ".msi", ".bat", ".cmd" }.Contains(ext)) return "Application";
        if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv" }.Contains(ext)) return "Video";
        if (new[] { ".mp3", ".wav", ".flac", ".aac" }.Contains(ext)) return "Audio";
        if (new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }.Contains(ext)) return "Archive";
        return "File";
    }

    public static string ClassifyFileStatic(string extension)
    {
        string ext = extension.ToLower();
        if (new[] { ".jpg", ".png", ".gif", ".bmp", ".jpeg", ".svg", ".webp" }.Contains(ext)) return "Image";
        if (new[] { ".doc", ".docx", ".pdf", ".txt", ".md", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx" }.Contains(ext)) return "Document";
        if (new[] { ".exe", ".lnk", ".msi", ".bat", ".cmd" }.Contains(ext)) return "Application";
        if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv" }.Contains(ext)) return "Video";
        if (new[] { ".mp3", ".wav", ".flac", ".aac" }.Contains(ext)) return "Audio";
        if (new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }.Contains(ext)) return "Archive";
        return "File";
    }

    private static bool IsSystemHidden(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.System)
            || attributes.HasFlag(FileAttributes.Hidden);
    }

    // ============================================================
    // Rescue (puts loose desktop files into FocusPanel_Recovered)
    // ============================================================
    public async Task RescueFiles()
    {
        await Task.Run(() =>
        {
            var rescueRoot = Path.Combine(_desktopPath, "FocusPanel_Recovered");
            if (!Directory.Exists(rescueRoot))
            {
                _createdPathSuppression.Suppress(
                    rescueRoot,
                    DateTimeOffset.UtcNow);
                Directory.CreateDirectory(rescueRoot);
            }

            foreach (var file in new DirectoryInfo(_desktopPath).GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) continue;

                string target = Path.Combine(rescueRoot, file.Name);
                try
                {
                    if (File.Exists(target))
                    {
                        string name = Path.GetFileNameWithoutExtension(file.Name);
                        string ext = file.Extension;
                        int count = 1;
                        while (File.Exists(target))
                            target = Path.Combine(rescueRoot, $"{name} ({count++}){ext}");
                    }
                    file.MoveTo(target);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Rescue error: {ex.Message}");
                }
            }
        });

        ScheduleFullRefresh();
    }

    // ============================================================
    // One-click organize
    // ============================================================
    public async Task<DesktopOrganizeResult> OrganizeAllFiles(
        bool allowCommonDesktopElevation = false,
        IProgress<DesktopOrganizeProgress>? progress = null)
        => await OrganizeFiles(
            Files
                .Where(file =>
                    !file.IsHidden
                    && !file.NeedsRecovery)
                .Select(file => file.FullPath)
                .ToArray(),
            allowCommonDesktopElevation,
            progress);

    public IReadOnlyList<string>
        GetCommonDesktopCandidatePaths() =>
        Files.Where(file =>
            !file.IsHidden
            && !file.NeedsRecovery
            && !DesktopAutoOrganizePolicy
                .IsProtectedPanelLauncher(
                    file.Name,
                    file.FullPath,
                    Environment.ProcessPath)
            && DesktopDropPolicy.Classify(
                file.FullPath,
                _desktopPath,
                _commonDesktopPath)
            == DesktopDropLocation.CommonDesktop)
            .Select(file => file.FullPath)
            .ToArray();

    public int CountCommonDesktopCandidates() =>
        GetCommonDesktopCandidatePaths().Count;

    public async Task<DesktopOrganizeResult> OrganizeFiles(
        IReadOnlyList<string> paths,
        bool allowCommonDesktopElevation = false,
        IProgress<DesktopOrganizeProgress>? progress = null)
    {
        await _organizeGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return new DesktopOrganizeResult(
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            var candidates = Files
                .Select(file => new DesktopAutoOrganizeItem(
                    file.Name,
                    file.FullPath,
                    file.FileType,
                    file.IsHidden,
                    file.NeedsRecovery,
                    DesktopAutoOrganizePolicy
                        .IsProtectedPanelLauncher(
                            file.Name,
                            file.FullPath,
                            Environment.ProcessPath),
                    file.CustomPartition))
                .ToArray();
            IReadOnlyList<DesktopAutoOrganizeItem> items =
                DesktopAutoOrganizePolicy
                    .SelectCreatedItems(
                        candidates,
                        paths);
            IReadOnlyDictionary<string, string> aiPartitions =
                await _aiPartitionService.ResolveAsync(items)
                    .ConfigureAwait(false);
            if (aiPartitions.Count > 0)
            {
                items = items.Select(item =>
                    aiPartitions.TryGetValue(
                        item.FullPath,
                        out string? partition)
                        ? item with { AiPartition = partition }
                        : item)
                    .ToArray();
            }

            IDisposable? elevatedBatch = null;
            bool elevationReady =
                allowCommonDesktopElevation;
            int commonDesktopCount = items.Count(item =>
                DesktopDropPolicy.Classify(
                    item.FullPath,
                    _desktopPath,
                    _commonDesktopPath)
                == DesktopDropLocation.CommonDesktop);
            if (allowCommonDesktopElevation
                && commonDesktopCount > 0)
            {
                try
                {
                    // Start one short-lived elevated helper for the
                    // complete batch. Attribute writes and rollbacks
                    // reuse this session, so a desktop containing many
                    // public shortcuts produces only one UAC prompt.
                    elevatedBatch =
                        await _visibilityIo
                            .BeginElevatedBatchAsync()
                            .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return new DesktopOrganizeResult(
                        items.Count,
                        0,
                        commonDesktopCount,
                        0,
                        Array.Empty<string>(),
                        items.Where(item =>
                                DesktopDropPolicy.Classify(
                                    item.FullPath,
                                    _desktopPath,
                                    _commonDesktopPath)
                                == DesktopDropLocation
                                    .CommonDesktop)
                            .Select(item => item.FullPath)
                            .ToArray());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "Start desktop elevation batch failed: "
                        + ex.Message);
                    return new DesktopOrganizeResult(
                        items.Count,
                        0,
                        commonDesktopCount,
                        items.Count
                            - commonDesktopCount,
                        items.Where(item =>
                                DesktopDropPolicy.Classify(
                                    item.FullPath,
                                    _desktopPath,
                                    _commonDesktopPath)
                                != DesktopDropLocation
                                    .CommonDesktop)
                            .Select(item => item.Name)
                            .ToArray(),
                        items.Where(item =>
                                DesktopDropPolicy.Classify(
                                    item.FullPath,
                                    _desktopPath,
                                    _commonDesktopPath)
                                == DesktopDropLocation
                                    .CommonDesktop)
                            .Select(item => item.FullPath)
                            .ToArray());
                }
            }

            await _refreshGate.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                DesktopOrganizeResult? result = null;
                await RunVisibilityMutationAsync(
                    async () =>
                    {
                        result =
                            await DesktopAutoOrganizePolicy
                                .ExecuteAsync(
                                    items,
                                    elevationReady,
                                    async (
                                        item,
                                        partition,
                                        allowElevation) =>
                                    {
                                        try
                                        {
                                            // A bulk organize operation
                                            // commits file visibility and
                                            // database state item by item,
                                            // but publishes the observable
                                            // desktop collections only once
                                            // after the batch. This prevents
                                            // layout re-entry while Explorer
                                            // is raising attribute changes.
                                            await HideFileFromDesktopPathCore(
                                                item.FullPath,
                                                partition,
                                                allowElevation,
                                                false);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(
                                                $"Auto organize {item.Name} failed: {ex.Message}");
                                            throw;
                                        }
                                    },
                                    progress);
                    });

                // Keep the refresh gate for the complete transaction so
                // watcher-driven refreshes cannot publish a half-organized
                // collection. One final snapshot becomes the only UI commit.
                await RefreshFilesCore()
                    .ConfigureAwait(false);
                return result
                    ?? new DesktopOrganizeResult(
                        0,
                        0,
                        0,
                        0,
                        Array.Empty<string>(),
                        Array.Empty<string>());
            }
            finally
            {
                _refreshGate.Release();
                elevatedBatch?.Dispose();
            }
        }
        finally
        {
            _organizeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _debounceTimer.Change(
            Timeout.Infinite,
            Timeout.Infinite);
        _desktopWatcher.Dispose();
        _commonDesktopWatcher?.Dispose();
        _storageWatcher?.Dispose();
        _debounceTimer.Dispose();
        if (_aiPartitionService is IDisposable disposable)
            disposable.Dispose();
    }
}
