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

public class FileOrganizerService
{
    private readonly string _desktopPath;
    private readonly string _commonDesktopPath;
    /// <summary>旧版本仓库，仅用于兼容已经被移动的项目。</summary>
    private readonly string _storageRoot;
    private readonly IDesktopItemVisibilityService _visibility;
    private FileSystemWatcher _desktopWatcher = null!;
    private FileSystemWatcher? _commonDesktopWatcher;
    private FileSystemWatcher? _storageWatcher;

    /// <summary>All files: desktop visible + stored in .FocusPanel</summary>
    public ObservableCollection<DesktopFile> AllFiles { get; private set; } = new();

    /// <summary>Only desktop-root visible files (not stored)</summary>
    public ObservableCollection<DesktopFile> Files { get; private set; } = new();

    public event Action? FilesChanged;

    private System.Threading.Timer? _debounceTimer;
    private readonly SemaphoreSlim _organizeGate = new(1, 1);
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
        IDesktopItemVisibilityService visibility)
    {
        _desktopPath = desktopPath;
        _commonDesktopPath = commonDesktopPath;
        _storageRoot = Path.Combine(_desktopPath, ".FocusPanel");
        _visibility = visibility;

        InitializeWatchers();
        RefreshFilesDebounced();
    }

    public bool ShowsProtectedSystemFiles => _visibility.ShowsProtectedSystemFiles;

    private void InitializeWatchers()
    {
        // Watch desktop root for visible file changes
        _desktopWatcher = new FileSystemWatcher(_desktopPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        _desktopWatcher.Created += OnChanged;
        _desktopWatcher.Deleted += OnChanged;
        _desktopWatcher.Renamed += OnRenamed;
        _desktopWatcher.EnableRaisingEvents = true;

        if (!string.IsNullOrWhiteSpace(_commonDesktopPath)
            && Directory.Exists(_commonDesktopPath)
            && !string.Equals(_desktopPath, _commonDesktopPath, StringComparison.OrdinalIgnoreCase))
        {
            _commonDesktopWatcher = new FileSystemWatcher(_commonDesktopPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            _commonDesktopWatcher.Created += OnChanged;
            _commonDesktopWatcher.Deleted += OnChanged;
            _commonDesktopWatcher.Renamed += OnRenamed;
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
            _storageWatcher.Created += (s, e) => RefreshFilesDebounced();
            _storageWatcher.Deleted += (s, e) => RefreshFilesDebounced();
            _storageWatcher.Renamed += (s, e) => RefreshFilesDebounced();
            _storageWatcher.EnableRaisingEvents = true;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore changes inside the storage folder from the desktop watcher
        if (e.FullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase)) return;
        RefreshFilesDebounced();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (e.FullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase)) return;
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
        RefreshFilesDebounced();
    }

    private void RefreshFilesDebounced()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await RefreshFiles();
                await AutoOrganizeIfEnabled();
            });
        }, null, DebounceInterval, System.Threading.Timeout.Infinite);
    }

    private async Task AutoOrganizeIfEnabled()
    {
        try
        {
            using var context = new AppDbContext();
            var config = context.AppConfigs.Find("FileOrganizer_AutoOrganize");
            if (config != null && bool.TryParse(config.Value, out var enable) && enable && Files.Count > 0)
            {
                DesktopOrganizeResult result = await OrganizeAllFiles();
                if (result.Failed > 0 || result.AuthorizationRequired > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Auto organize incomplete: {result.Failed} failed, "
                        + $"{result.AuthorizationRequired} require authorization.");
                }
            }
        }
        catch { }
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

                        var df = BuildDesktopFile(file.FullName, file.Name, file.Extension,
                            file.Length, file.CreationTime, ClassifyFile(file), isCollected, pref?.X, pref?.Y, fileList.Count);
                        try { df.Icon = IconHelper.GetIcon(file.FullName, true); } catch { }
                        fileList.Add(df);
                    }

                    foreach (var dir in new DirectoryInfo(desktopRoot).GetDirectories())
                    {
                        // Skip our storage folder
                        if (dir.Name.Equals(".FocusPanel", StringComparison.OrdinalIgnoreCase)) continue;
                        var pref = ResolvePreference(dir.Name, dir.FullName, dir.Attributes, preferences);
                        bool isCollected = pref?.IsHidden ?? false;
                        if (!isCollected && IsSystemHidden(dir.Attributes)) continue;
                        desktopRootFiles.Add(dir.Name);

                        var df = BuildDesktopFile(dir.FullName, dir.Name, "", 0,
                            dir.CreationTime, "Folder", isCollected, pref?.X, pref?.Y, fileList.Count);
                        try { df.Icon = IconHelper.GetIcon(dir.FullName, true); } catch { }
                        fileList.Add(df);
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
                            var df = BuildDesktopFile(fi.FullName, fi.Name, fi.Extension,
                                fi.Length, fi.CreationTime, ClassifyFile(fi), true, pref?.X, pref?.Y, fileList.Count);
                            try { df.Icon = IconHelper.GetIcon(fi.FullName, true); } catch { }
                            fileList.Add(df);
                        }

                        foreach (var dirPath in Directory.GetDirectories(partitionDir))
                        {
                            var di = new DirectoryInfo(dirPath);
                            if (desktopRootFiles.Contains(di.Name)) continue;

                            preferences.TryGetValue(di.Name, out var pref);
                            var df = BuildDesktopFile(di.FullName, di.Name, "", 0,
                                di.CreationTime, "Folder", true, pref?.X, pref?.Y, fileList.Count);
                            try { df.Icon = IconHelper.GetIcon(di.FullName, true); } catch { }
                            fileList.Add(df);
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
                    fileList.Add(new DesktopFile
                    {
                        Name = pref.FileName,
                        FullPath = pref.ManagedPath ?? Path.Combine(_desktopPath, pref.FileName),
                        Extension = Path.GetExtension(pref.FileName),
                        FileType = "Recovery",
                        CreatedAt = DateTime.Now,
                        IsHidden = true,
                        NeedsRecovery = true,
                        DesktopX = pref.X ?? 16,
                        DesktopY = pref.Y ?? 16
                    });
                }

                return fileList.OrderByDescending(f => f.FileType == "Folder").ThenBy(f => f.Name).ToList();
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateFilesIncremental(files);
                FilesChanged?.Invoke();
            });
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
                existing.Icon = newFile.Icon;
                existing.Size = newFile.Size;
                existing.FullPath = newFile.FullPath;
                existing.IsHidden = newFile.IsHidden;
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
            if (!visibleFiles.Any(v => v.Name == Files[i].Name))
                Files.RemoveAt(i);
        }
        foreach (var vf in visibleFiles)
        {
            if (!Files.Any(f => f.Name == vf.Name))
                Files.Add(vf);
        }
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

        if (!_visibility.Exists(fullPath))
            throw new FileNotFoundException("找不到要收纳的桌面项目。", fullPath);

        FileAttributes originalAttributes = _visibility.GetAttributes(fullPath);
        int preferenceId = 0;

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
            pref.PartitionName = partitionName;
            pref.IsHiddenFromDesktop = true;
            pref.ManagedPath = fullPath;
            pref.OriginalAttributes ??= (long)originalAttributes;
            pref.FileIdentity = _visibility.TryGetIdentity(fullPath);
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
            if (location == DesktopDropLocation.CommonDesktop)
                DesktopVisibilityElevatedHelper.SetAttributes(fullPath, collectedAttributes);
            else
            {
                _visibility.SetAttributes(fullPath, collectedAttributes);
                _visibility.NotifyAttributesChanged(fullPath);
            }

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
                _visibility.SetAttributes(fullPath, originalAttributes);
                _visibility.NotifyAttributesChanged(fullPath);
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

        // Update memory
        if (AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile file)
        {
            file.IsHidden = true;
            file.FullPath = fullPath;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var vf = Files.FirstOrDefault(f => f.Name == fileName);
            if (vf != null) Files.Remove(vf);
            FilesChanged?.Invoke();
        });

        IconHelper.ClearCache(fileName);
    }

    // ============================================================
    // 取消收纳：属性模式恢复原属性；旧仓库模式恢复到桌面
    // ============================================================
    public async Task RestoreFileToDesktop(string fileName, double? desktopX = null, double? desktopY = null)
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
                    File.Move(srcPath, destPath);
                else if (Directory.Exists(srcPath))
                    Directory.Move(srcPath, destPath);
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

        // Update memory
        if (AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile file)
        {
            file.IsHidden = false;
            file.Name = Path.GetFileName(restoredPath);
            file.FullPath = restoredPath;
            if (desktopX.HasValue) file.DesktopX = desktopX.Value;
            if (desktopY.HasValue) file.DesktopY = desktopY.Value;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (!Files.Any(f => f.Name == fileName) && AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile df)
                Files.Add(df);
            FilesChanged?.Invoke();
        });
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

        FilesChanged?.Invoke();
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

    public void ToggleDesktopIcons(bool show)
    {
        try { DesktopHelper.ToggleDesktopIcons(show); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ToggleDesktopIcons error: {ex.Message}"); }
    }

    // ============================================================
    // Rescue (puts loose desktop files into FocusPanel_Recovered)
    // ============================================================
    public async Task RescueFiles()
    {
        await Task.Run(() =>
        {
            var rescueRoot = Path.Combine(_desktopPath, "FocusPanel_Recovered");
            if (!Directory.Exists(rescueRoot)) Directory.CreateDirectory(rescueRoot);

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

        RefreshFilesDebounced();
    }

    // ============================================================
    // One-click organize
    // ============================================================
    public async Task<DesktopOrganizeResult> OrganizeAllFiles(
        bool allowCommonDesktopElevation = false)
    {
        await _organizeGate.WaitAsync();
        try
        {
            var visibleFiles = Files
                .Where(file => !file.IsHidden && !file.NeedsRecovery)
                .ToList();
            var items = visibleFiles
                .Select(file => new DesktopAutoOrganizeItem(
                    file.Name,
                    file.FullPath,
                    file.FileType))
                .ToList();

            DesktopOrganizeResult result =
                await DesktopAutoOrganizePolicy.ExecuteAsync(
                    items,
                    allowCommonDesktopElevation,
                    async (item, partition, allowElevation) =>
                    {
                        try
                        {
                            await HideFileFromDesktopPath(
                                item.FullPath,
                                partition,
                                allowElevation);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Auto organize {item.Name} failed: {ex.Message}");
                            throw;
                        }
                    });

            await RefreshFiles();
            return result;
        }
        finally
        {
            _organizeGate.Release();
        }
    }
}
