using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Windows.Threading;

namespace FocusPanel.ViewModels;

public partial class FileOrganizerViewModel :
    ObservableObject,
    IDisposable
{
    private readonly FileOrganizerService _fileService;
    private readonly SettingsService _settingsService;
    private bool _isDisposed;

    // Split partitions for Masonry/Staggered Layout
    public ObservableCollection<PartitionViewModel> PartitionsCol1 { get; } = new();
    public ObservableCollection<PartitionViewModel> PartitionsCol2 { get; } = new();
    
    // Master list for reference/search
    public ObservableCollection<PartitionViewModel> AllPartitions { get; } = new();

    [ObservableProperty]
    private bool isPersonalizedView = true;

    [ObservableProperty]
    private string currentViewMode = "Personalized"; // "Personalized" or "Timeline"
    
    [ObservableProperty]
    private string newPartitionName = string.Empty;
    
    // Rename Support
    [ObservableProperty]
    private bool isRenameDialogOpen;
    
    [ObservableProperty]
    private string renamePartitionName = string.Empty;
    
    private PartitionViewModel? _partitionToRename;

    [ObservableProperty]
    private PartitionViewModel? selectedPartition;
    
    [ObservableProperty]
    private DesktopFile? selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrganizeButtonText))]
    [NotifyPropertyChangedFor(nameof(OrganizeButtonIcon))]
    private bool isDesktopHidden;
    
    [ObservableProperty]
    private bool isListView = false;

    [ObservableProperty]
    private bool isAutoOrganizeEnabled;

    [ObservableProperty]
    private string autoOrganizeStatus =
        "关闭时不会处理新增桌面项目";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardWidth))]
    [NotifyPropertyChangedFor(nameof(CardHeight))]
    [NotifyPropertyChangedFor(nameof(IconImageSize))]
    private double iconScale = 1.0;

    [RelayCommand]
    private void SetIconScale(string scaleStr)
    {
        if (double.TryParse(scaleStr, out double scale))
        {
            IconScale = scale;
            _settingsService.CurrentSettings.IconScale = scale;
            _settingsService.SaveSettings();
        }
    }

    partial void OnIconScaleChanged(double value)
    {
        _settingsService.CurrentSettings.IconScale = value;
        _settingsService.SaveSettings();
        SaveLayoutSettings();
    }

    partial void OnIsListViewChanged(bool value)
    {
        _settingsService.CurrentSettings.IsListView = value;
        _settingsService.SaveSettings();
        SaveLayoutSettings();
    }
    
    partial void OnIsPersonalizedViewChanged(bool value)
    {
        _settingsService.CurrentSettings.IsPersonalizedView = value;
        _settingsService.SaveSettings();
        SaveLayoutSettings();
    }

    partial void OnIsAutoOrganizeEnabledChanged(bool value)
    {
        AutoOrganizeStatus = value
            ? "正在监听新增桌面项目；现有项目不会被自动收纳"
            : "关闭时不会处理新增桌面项目";
        try
        {
            using var context = new AppDbContext();
            var config = context.AppConfigs.Find("FileOrganizer_AutoOrganize");
            if (config == null)
            {
                context.AppConfigs.Add(new AppConfig
                {
                    Key = "FileOrganizer_AutoOrganize",
                    Value = value.ToString()
                });
            }
            else
            {
                config.Value = value.ToString();
            }
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            AutoOrganizeStatus = value
                ? "本次会话已启用，但设置未能持久保存"
                : "本次会话已关闭，但设置未能持久保存";
            System.Diagnostics.Debug.WriteLine(
                $"Save auto organize setting failed: {ex.Message}");
        }
    }
    
    private void SaveLayoutSettings()
    {
        try
        {
            using (var context = new AppDbContext())
            {
                // Save IconScale
                var scaleConfig = context.AppConfigs.Find("FileOrganizer_IconScale");
                if (scaleConfig == null)
                {
                    context.AppConfigs.Add(new AppConfig { Key = "FileOrganizer_IconScale", Value = IconScale.ToString() });
                }
                else
                {
                    scaleConfig.Value = IconScale.ToString();
                }

                // Save IsListView
                var listConfig = context.AppConfigs.Find("FileOrganizer_IsListView");
                if (listConfig == null)
                {
                    context.AppConfigs.Add(new AppConfig { Key = "FileOrganizer_IsListView", Value = IsListView.ToString() });
                }
                else
                {
                    listConfig.Value = IsListView.ToString();
                }

                // Save IsPersonalizedView
                var viewConfig = context.AppConfigs.Find("FileOrganizer_IsPersonalizedView");
                if (viewConfig == null)
                {
                    context.AppConfigs.Add(new AppConfig { Key = "FileOrganizer_IsPersonalizedView", Value = IsPersonalizedView.ToString() });
                }
                else
                {
                    viewConfig.Value = IsPersonalizedView.ToString();
                }

                context.SaveChanges();
            }
        }
        catch { }
    }
    
    private void LoadLayoutSettings()
    {
        try
        {
            using (var context = new AppDbContext())
            {
                // Load IconScale
                var scaleConfig = context.AppConfigs.Find("FileOrganizer_IconScale");
                if (scaleConfig != null && double.TryParse(scaleConfig.Value, out double scale))
                {
                    IconScale = scale;
                }
                else
                {
                    // Fallback to legacy settings
                    IconScale = _settingsService.CurrentSettings.IconScale > 0 ? _settingsService.CurrentSettings.IconScale : 1.0;
                }

                // Load IsListView
                var listConfig = context.AppConfigs.Find("FileOrganizer_IsListView");
                if (listConfig != null && bool.TryParse(listConfig.Value, out bool isList))
                {
                    IsListView = isList;
                }
                else
                {
                    IsListView = _settingsService.CurrentSettings.IsListView;
                }

                // Load IsPersonalizedView
                var viewConfig = context.AppConfigs.Find("FileOrganizer_IsPersonalizedView");
                if (viewConfig != null && bool.TryParse(viewConfig.Value, out bool isPersonalized))
                {
                    IsPersonalizedView = isPersonalized;
                }
                else
                {
                    IsPersonalizedView = _settingsService.CurrentSettings.IsPersonalizedView;
                }

                var autoConfig = context.AppConfigs.Find("FileOrganizer_AutoOrganize");
                IsAutoOrganizeEnabled = autoConfig != null
                    && bool.TryParse(autoConfig.Value, out bool autoOrganize)
                    && autoOrganize;
            }
        }
        catch 
        {
             // Fallback
            IconScale = _settingsService.CurrentSettings.IconScale > 0 ? _settingsService.CurrentSettings.IconScale : 1.0;
            IsListView = _settingsService.CurrentSettings.IsListView;
            IsPersonalizedView = _settingsService.CurrentSettings.IsPersonalizedView;
        }
    }

    [ObservableProperty]
    private int partitionColumns = 1;

    public double CardWidth => 100 * IconScale;
    public double CardHeight => 120 * IconScale;
    public double IconImageSize => 48 * IconScale;

    public string OrganizeButtonText =>
        IsDesktopHidden ? "显示桌面图标" : "隐藏桌面图标";
    public string OrganizeButtonIcon => IsDesktopHidden ? "Eye" : "EyeOff";

    public FileOrganizerViewModel()
    {
        _settingsService = new SettingsService();
        _fileService = new FileOrganizerService();
        
        LoadLayoutSettings();
        
        // Listen for file updates
        _fileService.FilesChanged +=
            FileService_FilesChanged;
        _fileService.DesktopItemsCreated +=
            FileService_DesktopItemsCreated;

        // Check initial desktop state
        try
        {
            IsDesktopHidden = !FocusPanel.Helpers.DesktopHelper.IsDesktopIconsVisible();
        }
        catch
        {
            IsDesktopHidden = false; // Default safe value
        }

        // Initial Build
        BuildPartitions();
    }

    private void FileService_FilesChanged()
    {
        // FileOrganizerService publishes collection changes on the
        // UI thread; keep the guard for explicit service calls.
        System.Windows.Application.Current
            .Dispatcher.Invoke(BuildPartitions);
    }

    private async void FileService_DesktopItemsCreated(
        IReadOnlyList<string> paths)
    {
        if (!IsAutoOrganizeEnabled
            || _isDisposed
            || paths.Count == 0)
        {
            return;
        }

        try
        {
            DesktopOrganizeResult result =
                await _fileService.OrganizeFiles(paths);
            string status =
                DesktopAutoOrganizePolicy
                    .DescribeAutomaticResult(result);
            if (!_isDisposed
                && !string.IsNullOrWhiteSpace(status))
                AutoOrganizeStatus = status;
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
            {
                AutoOrganizeStatus =
                    "自动收纳暂时失败；项目仍保留在桌面";
            }
            System.Diagnostics.Debug.WriteLine(
                "Auto organize created desktop items failed: "
                + ex.Message);
        }
    }

    private void BuildPartitions()
    {
        var viewModels = new List<PartitionViewModel>();
        
        try
        {
            using (var context = new AppDbContext())
            {
                context.EnsureSchema();

                // 1. Migration (if DB empty but Settings exist)
                if (!context.DesktopPartitions.Any() && _settingsService.CurrentSettings.CustomPartitionNames.Any())
                {
                    // Migrate Partitions
                    int index = 0;
                    foreach (var name in _settingsService.CurrentSettings.CustomPartitionNames)
                    {
                        context.DesktopPartitions.Add(new DesktopPartition { Name = name, OrderIndex = index++ });
                    }
                    
                    // Migrate File Preferences
                    foreach (var kvp in _settingsService.CurrentSettings.FilePartitions)
                    {
                        context.DesktopFilePreferences.Add(new DesktopFilePreference { FilePath = kvp.Key, PartitionName = kvp.Value });
                    }
                    
                    context.SaveChanges();
                }

                // 2. Load Data
                var dbPartitions = context.DesktopPartitions.OrderBy(p => p.OrderIndex).ToList();
                var dbPrefs = context.DesktopFilePreferences.ToList();
                var preferencesByName = dbPrefs
                    .GroupBy(
                        item => item.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Last(),
                        StringComparer.OrdinalIgnoreCase);

                // 3. Create Partition ViewModels
                var partitionMap =
                    new Dictionary<string, PartitionViewModel>(
                        StringComparer.OrdinalIgnoreCase);
                
                if (IsPersonalizedView)
                {
                    foreach (var p in dbPartitions)
                    {
                        var vm = new PartitionViewModel(p.Name) { IsCustom = true, ColumnIndex = p.ColumnIndex };
                        partitionMap[p.Name] = vm;
                        viewModels.Add(vm);
                    }
                }

                // 4. Distribute Files - 使用 AllFiles（包含已收纳的文件）
                var allFiles = _fileService.AllFiles;
                var uncategorizedFiles = new List<DesktopFile>();

                foreach (var file in allFiles)
                {
                    preferencesByName.TryGetValue(
                        file.Name,
                        out DesktopFilePreference? pref);

                    // 已收纳的文件（IsHiddenFromDesktop=true）显示在对应分区
                    // 未收纳的文件如果没有分区则显示在 Unsorted
                    if (pref != null && partitionMap.ContainsKey(pref.PartitionName))
                    {
                        partitionMap[pref.PartitionName].Files.Add(file);
                        file.CustomPartition = pref.PartitionName;
                    }
                    else
                    {
                        uncategorizedFiles.Add(file);
                        file.CustomPartition = null;
                    }
                }

                // 5. Create Default Categories (if needed)
                // Removed per user request: No default partitions, only custom ones.
                
                if (!IsPersonalizedView) // Timeline View (Keep as is)
                {
                     var dateGroups = allFiles.GroupBy(f => f.DateGroup).OrderBy(g => GetDateGroupSortOrder(g.Key));
                     int i = 0;
                     foreach (var group in dateGroups)
                     {
                         int defaultCol = (i++ % 2);
                         var p = new PartitionViewModel(group.Key) { IsCustom = false, ColumnIndex = defaultCol };
                         foreach (var file in group.OrderByDescending(f => f.CreatedAt)) p.Files.Add(file);
                         viewModels.Add(p);
                     }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("BuildPartitions Error: " + ex.Message);
            // Keep the last valid visual tree. A transient database read
            // failure must not look like every collection disappeared.
            return;
        }

        // 6. Update ObservableCollections
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            PartitionCollectionSynchronizer.Synchronize(
                AllPartitions,
                PartitionsCol1,
                PartitionsCol2,
                viewModels);
        });
        
        // Auto-select
        if (AllPartitions.Any() && (SelectedPartition == null || !AllPartitions.Contains(SelectedPartition)))
        {
            SelectedPartition = AllPartitions.First();
        }
    }

    private int GetDateGroupSortOrder(string groupName)
    {
        return groupName switch
        {
            "今天" => 0,
            "昨天" => 1,
            "本周" => 2,
            "本月" => 3,
            "更早" => 4,
            _ => 5
        };
    }

    [RelayCommand]
    private void ToggleView()
    {
        IsPersonalizedView = !IsPersonalizedView;
        CurrentViewMode = IsPersonalizedView ? "Personalized" : "Timeline";
        BuildPartitions();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await _fileService.RefreshFiles();
        BuildPartitions();
    }

    [RelayCommand]
    private void ToggleDesktop()
    {
        // Toggle Desktop Icons visibility
        IsDesktopHidden = !IsDesktopHidden;
        _fileService.ToggleDesktopIcons(!IsDesktopHidden);
    }
    
    [RelayCommand]
    private async Task Rescue()
    {
        var result = FocusDialogService.Show(
            "这会把桌面上未分类的普通文件移动到“FocusPanel_Recovered”文件夹。\n\n"
            + "不会应用分类，快捷方式和文件夹会被跳过。是否继续？",
            "救援桌面",
            System.Windows.MessageBoxButton.YesNo, 
            System.Windows.MessageBoxImage.Warning);
            
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await _fileService.RescueFiles();
        }
    }

    [RelayCommand]
    private async Task OrganizeAll()
    {
        if (_fileService.Files.Count == 0)
        {
            FocusDialogService.Show(
                "桌面上没有需要整理的可见项目。",
                "自动整理",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var result = FocusDialogService.Show(
            $"将 {_fileService.Files.Count} 个可见桌面项目按类型收纳到面板。\n\n"
            + "文件不会移动或改名，只会写入分类并从原生桌面隐藏。是否继续？",
            "自动整理桌面",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            DesktopOrganizeResult organizeResult =
                await _fileService.OrganizeAllFiles();
            int collected = organizeResult.Collected;

            if (organizeResult.AuthorizationRequired > 0)
            {
                var authorize = FocusDialogService.Show(
                    $"另有 {organizeResult.AuthorizationRequired} 个公共桌面项目需要管理员授权。"
                    + "\n\n是否继续收纳这些项目？",
                    "公共桌面授权",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (authorize == System.Windows.MessageBoxResult.Yes)
                {
                    DesktopOrganizeResult elevatedResult =
                        await _fileService.OrganizeAllFiles(true);
                    collected += elevatedResult.Collected;
                }
            }

            await _fileService.RefreshFiles();
            BuildPartitions();
            int remaining = _fileService.Files.Count;
            FocusDialogService.Show(
                remaining == 0
                    ? $"已收纳 {collected} 个桌面项目。"
                    : $"已收纳 {collected} 个桌面项目；仍有 {remaining} 个项目因权限或文件状态未能收纳。",
                "自动整理完成",
                System.Windows.MessageBoxButton.OK,
                remaining == 0
                    ? System.Windows.MessageBoxImage.Information
                    : System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void OpenPartitionFolder(PartitionViewModel partition)
    {
        if (partition == null) return;
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            System.Diagnostics.Process.Start("explorer.exe", desktopPath);
        }
        catch { }
    }

    [RelayCommand]
    private void CreatePartition(string? name = null)
    {
        string partitionName = name ?? NewPartitionName;
        
        if (string.IsNullOrWhiteSpace(partitionName)) return;
        
        using (var context = new AppDbContext())
        {
            if (!context.DesktopPartitions.Any(p => p.Name == partitionName))
            {
                int maxOrder = context.DesktopPartitions.Any() ? context.DesktopPartitions.Max(p => p.OrderIndex) : -1;
                context.DesktopPartitions.Add(new DesktopPartition { Name = partitionName, OrderIndex = maxOrder + 1 });
                context.SaveChanges();
                
                if (!IsPersonalizedView)
                {
                    IsPersonalizedView = true;
                    CurrentViewMode = "Personalized";
                }
                BuildPartitions();
            }
        }
        
        NewPartitionName = string.Empty;
    }
    
    [RelayCommand]
    private void OpenRenameDialog(PartitionViewModel? partition)
    {
        if (partition == null) return;
        _partitionToRename = partition;
        RenamePartitionName = partition.Name;
        IsRenameDialogOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmRename()
    {
        if (_partitionToRename == null || string.IsNullOrWhiteSpace(RenamePartitionName)) 
        {
            IsRenameDialogOpen = false;
            return;
        }

        string oldName = _partitionToRename.Name;
        string newName = RenamePartitionName;
        
        IsRenameDialogOpen = false; // Close immediately for responsiveness

        if (oldName == newName) return;

        await Task.Run(() => 
        {
            using (var context = new AppDbContext())
            {
                var p = context.DesktopPartitions.FirstOrDefault(dp => dp.Name == oldName);
                if (p != null)
                {
                    p.Name = newName;
                    
                    var prefs = context.DesktopFilePreferences.Where(fp => fp.PartitionName == oldName).ToList();
                    foreach (var pref in prefs)
                    {
                        pref.PartitionName = newName;
                    }
                    
                    context.SaveChanges();
                }
            }
        });
        
        BuildPartitions();
    }
    
    [RelayCommand]
    private void CancelRename()
    {
        IsRenameDialogOpen = false;
    }

    [RelayCommand]
    private void DeletePartition(PartitionViewModel? partition)
    {
        if (partition == null) return;

        var result = FocusDialogService.Show(
            $"确定删除分区“{partition.Name}”吗？\n分区内文件会变为未分类，但仍保留在桌面。",
            "删除分区",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        using (var context = new AppDbContext())
        {
            var p = context.DesktopPartitions.FirstOrDefault(dp => dp.Name == partition.Name);
            if (p != null)
            {
                context.DesktopPartitions.Remove(p);
                var prefs = context.DesktopFilePreferences
                    .Where(fp => fp.PartitionName == partition.Name)
                    .ToList();
                foreach (var pref in prefs)
                    pref.PartitionName = "";
                context.SaveChanges();
            }
        }
        BuildPartitions();
    }

    [RelayCommand]
    private void SelectFile(DesktopFile? file)
    {
        if (file == null) return;
        if (SelectedFile != null && SelectedFile != file) SelectedFile.IsSelected = false;
        SelectedFile = file;
        SelectedFile.IsSelected = true;
    }

    public void ToggleFileSelection(DesktopFile? file)
    {
        if (file == null) return;
        file.IsSelected = !file.IsSelected;
        if (file.IsSelected)
            SelectedFile = file;
        else if (SelectedFile == file)
            SelectedFile = AllPartitions.SelectMany(p => p.Files).FirstOrDefault(f => f.IsSelected);
    }

    public void DeselectAllFiles()
    {
        foreach (var file in AllPartitions.SelectMany(p => p.Files).Where(f => f.IsSelected))
        {
            file.IsSelected = false;
        }
        SelectedFile = null;
    }

    public IEnumerable<DesktopFile> SelectedFiles =>
        AllPartitions.SelectMany(p => p.Files).Where(f => f.IsSelected);

    [RelayCommand]
    private async Task BatchHideToPanel(string partitionName)
    {
        var selected = SelectedFiles.ToList();
        if (selected.Count == 0 || string.IsNullOrEmpty(partitionName)) return;

        foreach (var file in selected)
        {
            await _fileService.HideFileFromDesktop(file.Name, partitionName);
        }
        BuildPartitions();
    }

    [RelayCommand]
    private async Task BatchRestoreFromPanel()
    {
        var selected = SelectedFiles.ToList();
        if (selected.Count == 0) return;

        foreach (var file in selected)
        {
            await _fileService.RestoreFileToDesktop(file.Name);
        }
        BuildPartitions();
    }

    [RelayCommand]
    private void OpenFile(DesktopFile file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open file: {ex.Message}");
        }
    }

    public void ReorderPartition(PartitionViewModel source, PartitionViewModel target, bool insertAfter = false)
    {
        if (source == null || target == null || source == target) return;
        if (!IsPersonalizedView) return;

        using (var context = new AppDbContext())
        {
            var partitions = context.DesktopPartitions.OrderBy(p => p.OrderIndex).ToList();
            var srcP = partitions.FirstOrDefault(p => p.Name == source.Name);
            var tgtP = partitions.FirstOrDefault(p => p.Name == target.Name);
            
            if (srcP != null && tgtP != null)
            {
                // Determine Target Column and Order
                int targetColumn = tgtP.ColumnIndex; 
                
                // If we are dragging to a different column, update source column
                if (srcP.ColumnIndex != targetColumn)
                {
                    srcP.ColumnIndex = targetColumn;
                }
                
                // Get all partitions in the target column, ordered by index
                var colPartitions = partitions.Where(p => p.ColumnIndex == targetColumn).OrderBy(p => p.OrderIndex).ToList();
                
                // Remove source if it's already in this list (same column move)
                // Note: We need to remove it first to calculate correct insertion index
                if (colPartitions.Contains(srcP))
                {
                    colPartitions.Remove(srcP);
                }
                
                // Find index of target
                int targetIndex = colPartitions.IndexOf(tgtP);
                
                if (targetIndex != -1)
                {
                    if (insertAfter)
                    {
                        // Insert AFTER target
                        if (targetIndex + 1 < colPartitions.Count)
                            colPartitions.Insert(targetIndex + 1, srcP);
                        else
                            colPartitions.Add(srcP);
                    }
                    else
                    {
                        // Insert BEFORE target
                        colPartitions.Insert(targetIndex, srcP);
                    }
                }
                else
                {
                    // Fallback
                    colPartitions.Add(srcP);
                }
                
                // Re-index this column
                for (int i = 0; i < colPartitions.Count; i++)
                {
                    colPartitions[i].OrderIndex = i;
                }
                
                // Re-index old column if needed
                if (source.ColumnIndex != targetColumn)
                {
                    var oldColPartitions = partitions.Where(p => p.ColumnIndex == source.ColumnIndex && p != srcP).OrderBy(p => p.OrderIndex).ToList();
                    for (int i = 0; i < oldColPartitions.Count; i++)
                    {
                        oldColPartitions[i].OrderIndex = i;
                    }
                }
                
                context.SaveChanges();
            }
        }
        BuildPartitions();
    }

    public void MovePartitionToColumn(PartitionViewModel source, int targetColumn)
    {
        if (source == null || !IsPersonalizedView) return;
        if (source.ColumnIndex == targetColumn) return; // Already in column, no change if just dropped on empty space

        using (var context = new AppDbContext())
        {
            var p = context.DesktopPartitions.FirstOrDefault(dp => dp.Name == source.Name);
            if (p != null)
            {
                p.ColumnIndex = targetColumn;
                
                // Set order to max + 1
                var colPartitions = context.DesktopPartitions.Where(dp => dp.ColumnIndex == targetColumn).ToList();
                int maxOrder = colPartitions.Any() ? colPartitions.Max(dp => dp.OrderIndex) : -1;
                p.OrderIndex = maxOrder + 1;
                
                context.SaveChanges();
            }
        }
        BuildPartitions();
    }

    [RelayCommand]
    private void AssignToPartition(string partitionName)
    {
        if (SelectedFile == null) return;
        
        using (var context = new AppDbContext())
        {
            var pref = context.DesktopFilePreferences.FirstOrDefault(fp => fp.FilePath == SelectedFile.Name);
            
            if (string.IsNullOrEmpty(partitionName))
            {
                if (pref != null)
                {
                    if (pref.IsHiddenFromDesktop)
                        pref.PartitionName = "";
                    else
                        context.DesktopFilePreferences.Remove(pref);
                }
            }
            else
            {
                if (pref == null)
                {
                    pref = new DesktopFilePreference { FilePath = SelectedFile.Name, PartitionName = "" };
                    context.DesktopFilePreferences.Add(pref);
                }
                pref.PartitionName = partitionName;
                
                if (!context.DesktopPartitions.Any(dp => dp.Name == partitionName))
                {
                    int maxOrder = context.DesktopPartitions.Any() ? context.DesktopPartitions.Max(dp => dp.OrderIndex) : -1;
                    context.DesktopPartitions.Add(new DesktopPartition { Name = partitionName, OrderIndex = maxOrder + 1 });
                }
            }
            context.SaveChanges();
        }
        BuildPartitions();
    }

    [RelayCommand]
    private async Task HideFileToPanel(string partitionName)
    {
        if (SelectedFile == null) return;

        await HideDraggedFileToPanel(
            SelectedFile,
            partitionName);
    }

    public async Task HideDraggedFileToPanel(
        DesktopFile file,
        string partitionName)
    {
        if (file == null)
            return;

        string fileName = file.Name;
        string targetPartition = partitionName ?? "Unsorted";

        try
        {
            await _fileService.HideFileFromDesktop(fileName, targetPartition);
            BuildPartitions();
        }
        catch (Exception ex)
        {
            FocusDialogService.Show(
                $"收纳失败，文件未被移动：{ex.Message}",
                "FocusPanel",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RestoreFileFromPanel()
    {
        if (SelectedFile == null) return;

        await RestoreDraggedFileToDesktop(SelectedFile);
    }

    public async Task RestoreDraggedFileToDesktop(DesktopFile file)
    {
        if (file == null || !file.IsHidden)
            return;

        string fileName = file.Name;
        try
        {
            await _fileService.RestoreFileToDesktop(fileName);
            BuildPartitions();
        }
        catch (Exception ex)
        {
            FocusDialogService.Show(
                $"恢复失败，FocusPanel 已保留恢复记录：{ex.Message}",
                "FocusPanel",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public async Task<DesktopImportResult> ImportFiles(
        string[] filePaths,
        string targetPartitionName)
    {
        if (filePaths == null
            || filePaths.Length == 0
            || string.IsNullOrEmpty(targetPartitionName))
            return new DesktopImportResult();

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string commonDesktopPath = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonDesktopDirectory);
        int collected = 0;
        int outsideDesktop = 0;
        int failed = 0;
        int authorizationCanceled = 0;
        bool? commonDesktopApproved = null;

        foreach (string path in filePaths)
        {
            if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
            {
                failed++;
                continue;
            }

            string fullPath = System.IO.Path.GetFullPath(path);
            DesktopDropLocation location = DesktopDropPolicy.Classify(
                fullPath,
                desktopPath,
                commonDesktopPath);
            if (location == DesktopDropLocation.OutsideDesktop)
            {
                outsideDesktop++;
                continue;
            }

            try
            {
                bool allowElevation = false;
                if (location == DesktopDropLocation.CommonDesktop)
                {
                    if (!commonDesktopApproved.HasValue)
                    {
                        commonDesktopApproved = FocusDialogService.Show(
                            "该项目位于 Windows 公共桌面。收纳或恢复它会影响本机所有账户，并需要管理员授权。\n\n是否继续？",
                            "收纳公共桌面项目",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
                    }

                    if (commonDesktopApproved != true)
                    {
                        authorizationCanceled++;
                        continue;
                    }
                    allowElevation = true;
                }

                await _fileService.HideFileFromDesktopPath(
                    fullPath,
                    targetPartitionName,
                    allowElevation);
                collected++;
            }
            catch (OperationCanceledException)
            {
                authorizationCanceled++;
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine(
                    $"Collect dropped desktop item failed: {ex.Message}");
            }
        }

        await _fileService.RefreshFiles();
        BuildPartitions();
        return new DesktopImportResult(
            collected,
            outsideDesktop,
            failed,
            authorizationCanceled);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _fileService.FilesChanged -=
            FileService_FilesChanged;
        _fileService.DesktopItemsCreated -=
            FileService_DesktopItemsCreated;
        _fileService.Dispose();
    }
}

public sealed record DesktopImportResult(
    int Collected = 0,
    int OutsideDesktop = 0,
    int Failed = 0,
    int AuthorizationCanceled = 0)
{
    public bool HasIssues => OutsideDesktop + Failed + AuthorizationCanceled > 0;
}
