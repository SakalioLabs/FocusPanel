using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FocusPanel.Helpers;
using FocusPanel.Models;
using FocusPanel.Data;

namespace FocusPanel.Services;

public class FileOrganizerService
{
    public static event Action? OverlayRefreshRequested;

    private readonly string _desktopPath;
    /// <summary>Storage folder ON desktop: Desktop\.FocusPanel\ — same disk = instant move</summary>
    private readonly string _storageRoot;
    private FileSystemWatcher _desktopWatcher;
    private FileSystemWatcher _storageWatcher;

    /// <summary>All files: desktop visible + stored in .FocusPanel</summary>
    public ObservableCollection<DesktopFile> AllFiles { get; private set; } = new();

    /// <summary>Only desktop-root visible files (not stored)</summary>
    public ObservableCollection<DesktopFile> Files { get; private set; } = new();

    public event Action FilesChanged;

    private System.Threading.Timer _debounceTimer;
    private const int DebounceInterval = 500;

    public FileOrganizerService()
    {
        _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        _storageRoot = Path.Combine(_desktopPath, ".FocusPanel");

        InitializeWatchers();
        RefreshFilesDebounced();
    }

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

        // Watch storage folder for internal changes (moves between partitions, etc.)
        if (Directory.Exists(_storageRoot))
        {
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
                await OrganizeAllFiles();
                FilesChanged?.Invoke();
            }
        }
        catch { }
    }

    private sealed record DesktopPreferenceSnapshot(string PartitionName, bool IsHidden, double? X, double? Y);

    private Dictionary<string, DesktopPreferenceSnapshot> LoadDesktopPreferences()
    {
        var map = new Dictionary<string, DesktopPreferenceSnapshot>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            foreach (var pref in context.DesktopFilePreferences)
                map[pref.FilePath] = new DesktopPreferenceSnapshot(
                    pref.PartitionName ?? "",
                    pref.IsHiddenFromDesktop,
                    pref.DesktopX,
                    pref.DesktopY);
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
            var preferences = await Task.Run(() => LoadDesktopPreferences());

            var files = await Task.Run(() =>
            {
                var fileList = new List<DesktopFile>();
                var desktopRootFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Scan desktop root — all files here are visible
                if (Directory.Exists(_desktopPath))
                {
                    foreach (var file in new DirectoryInfo(_desktopPath).GetFiles())
                    {
                        if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        preferences.TryGetValue(file.Name, out var pref);
                        bool isCollected = pref?.IsHidden ?? false;
                        if (!isCollected && IsSystemHidden(file.Attributes)) continue;
                        desktopRootFiles.Add(file.Name);

                        var df = BuildDesktopFile(file.FullName, file.Name, file.Extension,
                            file.Length, file.CreationTime, ClassifyFile(file), isCollected, pref?.X, pref?.Y, fileList.Count);
                        try { df.Icon = IconHelper.GetIcon(file.FullName, true); } catch { }
                        fileList.Add(df);
                    }

                    foreach (var dir in new DirectoryInfo(_desktopPath).GetDirectories())
                    {
                        // Skip our storage folder
                        if (dir.Name.Equals(".FocusPanel", StringComparison.OrdinalIgnoreCase)) continue;
                        preferences.TryGetValue(dir.Name, out var pref);
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

                // 3. Clean up DB: remove records for files no longer in storage
                CleanStalePreferences(preferences, fileList);

                return fileList.OrderByDescending(f => f.FileType == "Folder").ThenBy(f => f.Name).ToList();
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateFilesIncremental(files);
                ApplyCollectedIconState();
                FilesChanged?.Invoke();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshFiles error: {ex.Message}");
        }
    }

    private void CleanStalePreferences(Dictionary<string, DesktopPreferenceSnapshot> preferences, List<DesktopFile> currentFiles)
    {
        var currentNames = new HashSet<string>(
            currentFiles.Where(f => f.IsHidden).Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var stale = preferences.Where(p => p.Value.IsHidden && !currentNames.Contains(p.Key)).Select(p => p.Key).ToList();
        if (stale.Count == 0) return;

        try
        {
            using var context = new AppDbContext();
            foreach (var name in stale)
            {
                var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == name);
                if (pref != null)
                {
                    pref.IsHiddenFromDesktop = false;
                }
            }
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CleanStalePreferences error: {ex.Message}");
        }
    }

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
    // 收纳：桌面根目录 → .FocusPanel/{partition}/  (同磁盘瞬移)
    // ============================================================
    public async Task HideFileFromDesktop(string fileName, string partitionName)
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
            pref.PartitionName = partitionName;
            pref.IsHiddenFromDesktop = true;

            if (!string.IsNullOrEmpty(partitionName) && !context.DesktopPartitions.Any(dp => dp.Name == partitionName))
            {
                int maxOrder = context.DesktopPartitions.Any() ? context.DesktopPartitions.Max(dp => dp.OrderIndex) : -1;
                context.DesktopPartitions.Add(new DesktopPartition { Name = partitionName, OrderIndex = maxOrder + 1 });
            }
            context.SaveChanges();
        });

        // Update memory
        if (AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile file)
        {
            file.IsHidden = true;
            file.FullPath = Path.Combine(_desktopPath, fileName);
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var vf = Files.FirstOrDefault(f => f.Name == fileName);
            if (vf != null) Files.Remove(vf);
            FilesChanged?.Invoke();
        });

        IconHelper.ClearCache(fileName);
        DesktopHelper.HideDesktopItem(fileName);
        OverlayRefreshRequested?.Invoke();
    }

    // ============================================================
    // 取消收纳：.FocusPanel/{partition}/ → 桌面根目录
    // ============================================================
    public async Task RestoreFileToDesktop(string fileName, double? desktopX = null, double? desktopY = null)
    {
        string partitionName = "";

        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == fileName);
            if (pref != null)
            {
                partitionName = pref.PartitionName ?? "";
            }

            string srcDir = Path.Combine(_storageRoot, string.IsNullOrEmpty(partitionName) ? "Unsorted" : partitionName);
            string srcPath = Path.Combine(srcDir, fileName);
            string destPath = Path.Combine(_desktopPath, fileName);

            if ((File.Exists(srcPath) || Directory.Exists(srcPath)) && (File.Exists(destPath) || Directory.Exists(destPath)))
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                int count = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                    destPath = Path.Combine(_desktopPath, $"{name} ({count++}){ext}");
            }

            if (File.Exists(srcPath))
                File.Move(srcPath, destPath);
            else if (Directory.Exists(srcPath))
                Directory.Move(srcPath, destPath);

            if (pref != null)
            {
                pref.IsHiddenFromDesktop = false;
                if (desktopX.HasValue) pref.DesktopX = desktopX.Value;
                if (desktopY.HasValue) pref.DesktopY = desktopY.Value;
                context.SaveChanges();
            }
        });

        // Update memory
        if (AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile file)
        {
            file.IsHidden = false;
            file.FullPath = Path.Combine(_desktopPath, fileName);
            if (desktopX.HasValue) file.DesktopX = desktopX.Value;
            if (desktopY.HasValue) file.DesktopY = desktopY.Value;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (!Files.Any(f => f.Name == fileName) && AllFiles.FirstOrDefault(f => f.Name == fileName) is DesktopFile df)
                Files.Add(df);
            FilesChanged?.Invoke();
        });

        DesktopHelper.ShowDesktopItem(fileName);
        await Task.Delay(200);
        ApplyCollectedIconState();
        OverlayRefreshRequested?.Invoke();
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

    // 跨分区移动（存储目录内）
    public async Task MoveToPartition(string fileName, string newPartitionName)
    {
        string oldPartition = "";

        await Task.Run(() =>
        {
            using var context = new AppDbContext();
            context.EnsureSchema();

            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == fileName);
            bool isStored = pref?.IsHiddenFromDesktop ?? false;
            oldPartition = pref?.PartitionName ?? "";

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

            // Legacy stored files may still live under .FocusPanel. New collected files
            // stay in Desktop root and only update their partition metadata.
            if (isStored && !string.IsNullOrEmpty(oldPartition))
            {
                string srcDir = Path.Combine(_storageRoot, oldPartition);
                string srcPath = Path.Combine(srcDir, fileName);
                if (File.Exists(srcPath) || Directory.Exists(srcPath))
                {
                    string destDir = Path.Combine(_storageRoot, string.IsNullOrEmpty(newPartitionName) ? "Unsorted" : newPartitionName);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, fileName);

                    if (File.Exists(srcPath))
                        File.Move(srcPath, destPath);
                    else if (Directory.Exists(srcPath))
                        Directory.Move(srcPath, destPath);
                }
            }
        });

        DesktopHelper.HideDesktopItem(fileName);
        FilesChanged?.Invoke();
        OverlayRefreshRequested?.Invoke();
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

    private void ApplyCollectedIconState()
    {
        foreach (var file in AllFiles.Where(f => f.IsHidden).ToList())
        {
            try { DesktopHelper.HideDesktopItem(file.Name); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"HideDesktopItem error: {ex.Message}"); }
        }
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
    private static readonly Dictionary<string, string> TypeToPartitionMap = new()
    {
        { "Image", "图片" },
        { "Document", "文档" },
        { "Video", "视频" },
        { "Audio", "音频" },
        { "Archive", "压缩包" },
        { "Application", "应用程序" },
        { "Folder", "文件夹" },
        { "File", "其他" },
    };

    public async Task OrganizeAllFiles()
    {
        var visibleFiles = Files.ToList();
        if (visibleFiles.Count == 0) return;

        foreach (var group in visibleFiles.GroupBy(f => f.FileType))
        {
            string partitionName = TypeToPartitionMap.TryGetValue(group.Key, out var name) ? name : "其他";
            foreach (var file in group)
                await HideFileFromDesktop(file.Name, partitionName);
        }

        RefreshFilesDebounced();
    }
}
