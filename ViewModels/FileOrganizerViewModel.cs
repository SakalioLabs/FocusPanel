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
using System.Threading;
using System.Windows.Threading;

namespace FocusPanel.ViewModels;

internal sealed record PendingOrganizerLayoutSnapshot(
    long SettingsRevision,
    OrganizerLayoutSnapshot Snapshot);

public partial class FileOrganizerViewModel :
    ObservableObject,
    IDisposable
{
    private readonly FileOrganizerService _fileService;
    private readonly SettingsService _settingsService;
    private readonly IOrganizerLayoutRepository
        _layoutRepository;
    private readonly Dispatcher _uiDispatcher;
    private readonly OrganizerLegacyLayout _legacyLayout;
    private readonly CoalescingBackgroundRefresh<
        PendingOrganizerLayoutSnapshot> _layoutRefresh;
    private readonly CoalescingAsyncSaveQueue<
        OrganizerLayoutSaveState> _layoutSaveQueue;
    private readonly OrganizerLayoutSaveState
        _layoutSaveState;
    private bool _applyingLayoutOptions;
    private long _layoutSettingsRevision;
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
            IconScale = scale;
    }

    partial void OnIconScaleChanged(double value)
    {
        if (!_applyingLayoutOptions)
            QueueLayoutOptionsSave();
    }

    partial void OnIsListViewChanged(bool value)
    {
        if (!_applyingLayoutOptions)
            QueueLayoutOptionsSave();
    }
    
    partial void OnIsPersonalizedViewChanged(bool value)
    {
        if (_applyingLayoutOptions)
            return;

        QueueLayoutOptionsSave();
        RequestLayoutRefresh();
    }

    partial void OnIsAutoOrganizeEnabledChanged(bool value)
    {
        AutoOrganizeStatus = value
            ? "正在监听新增桌面项目；现有项目不会被自动收纳"
            : "关闭时不会处理新增桌面项目";
        if (!_applyingLayoutOptions)
            QueueLayoutOptionsSave();
    }
    
    private void QueueLayoutOptionsSave()
    {
        if (_isDisposed)
            return;

        Interlocked.Increment(
            ref _layoutSettingsRevision);
        _layoutSaveState.Update(
            CaptureLayoutOptions());
        _layoutSaveQueue.Enqueue(
            _layoutSaveState);
    }
    
    private OrganizerLayoutOptions CaptureLayoutOptions() =>
        new(
            IconScale,
            IsListView,
            IsPersonalizedView,
            IsAutoOrganizeEnabled);

    private Task SaveLayoutOptionsAsync(
        OrganizerLayoutSaveState state)
    {
        OrganizerLayoutOptions options =
            state.Read();
        return Task.Run(
            () =>
            {
                _layoutRepository.SaveOptions(options);
                _settingsService.CurrentSettings.IconScale =
                    options.IconScale;
                _settingsService.CurrentSettings.IsListView =
                    options.IsListView;
                _settingsService.CurrentSettings
                    .IsPersonalizedView =
                    options.IsPersonalizedView;
                if (!_settingsService.SaveSettings())
                {
                    throw new InvalidOperationException(
                        _settingsService.LastError
                        ?? "无法保存桌面收纳设置。");
                }
            });
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
        : this(
            new SettingsService(),
            new FileOrganizerService(),
            new OrganizerLayoutRepository(),
            Dispatcher.CurrentDispatcher)
    {
    }

    internal FileOrganizerViewModel(
        SettingsService settingsService,
        FileOrganizerService fileService,
        IOrganizerLayoutRepository layoutRepository,
        Dispatcher uiDispatcher)
    {
        _settingsService =
            settingsService
            ?? throw new ArgumentNullException(
                nameof(settingsService));
        _fileService =
            fileService
            ?? throw new ArgumentNullException(
                nameof(fileService));
        _layoutRepository =
            layoutRepository
            ?? throw new ArgumentNullException(
                nameof(layoutRepository));
        _uiDispatcher =
            uiDispatcher
            ?? throw new ArgumentNullException(
                nameof(uiDispatcher));

        AppSettings legacySettings =
            _settingsService.CurrentSettings;
        var fallbackOptions =
            new OrganizerLayoutOptions(
                legacySettings.IconScale,
                legacySettings.IsListView,
                legacySettings.IsPersonalizedView,
                false);
        _legacyLayout =
            new OrganizerLegacyLayout(
                fallbackOptions,
                legacySettings.CustomPartitionNames
                    .ToArray(),
                new Dictionary<string, string>(
                    legacySettings.FilePartitions,
                    StringComparer.OrdinalIgnoreCase));
        _layoutSaveState =
            new OrganizerLayoutSaveState(
                fallbackOptions);
        _layoutSaveQueue =
            new CoalescingAsyncSaveQueue<
                OrganizerLayoutSaveState>(
                SaveLayoutOptionsAsync,
                TimeSpan.FromMilliseconds(180));
        _layoutSaveQueue.ItemSaved +=
            OnLayoutOptionsSaved;
        _layoutSaveQueue.ItemSaveFailed +=
            OnLayoutOptionsSaveFailed;
        _layoutRefresh =
            new CoalescingBackgroundRefresh<
                PendingOrganizerLayoutSnapshot>(
                CaptureLayoutSnapshot,
                ApplyLayoutSnapshotAsync);

        _applyingLayoutOptions = true;
        try
        {
            IconScale = fallbackOptions.IconScale;
            IsListView = fallbackOptions.IsListView;
            IsPersonalizedView =
                fallbackOptions.IsPersonalizedView;
            CurrentViewMode = IsPersonalizedView
                ? "Personalized"
                : "Timeline";
        }
        finally
        {
            _applyingLayoutOptions = false;
        }
        
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

        RequestLayoutRefresh();
    }

    private void FileService_FilesChanged()
    {
        RequestLayoutRefresh();
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

    private void RequestLayoutRefresh()
    {
        if (_isDisposed)
            return;

        if (!_uiDispatcher.CheckAccess())
        {
            if (_uiDispatcher.HasShutdownStarted
                || _uiDispatcher.HasShutdownFinished)
            {
                return;
            }

            _uiDispatcher.BeginInvoke(
                new Action(RequestLayoutRefresh),
                DispatcherPriority.Background);
            return;
        }

        _layoutRefresh.Request();
    }

    private PendingOrganizerLayoutSnapshot
        CaptureLayoutSnapshot() =>
        new(
            Volatile.Read(
                ref _layoutSettingsRevision),
            _layoutRepository.Load(
                _legacyLayout));

    private async Task ApplyLayoutSnapshotAsync(
        PendingOrganizerLayoutSnapshot pending,
        CancellationToken cancellationToken)
    {
        await _uiDispatcher.InvokeAsync(
            () =>
                ApplyLayoutSnapshot(
                    pending,
                    cancellationToken),
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ApplyLayoutSnapshot(
        PendingOrganizerLayoutSnapshot pending,
        CancellationToken cancellationToken)
    {
        if (_isDisposed
            || cancellationToken.IsCancellationRequested
            || !pending.Snapshot.IsValid)
        {
            return;
        }

        long currentSettingsRevision =
            Volatile.Read(
                ref _layoutSettingsRevision);
        if (OrganizerLayoutApplyPolicy.CanApplyOptions(
                pending.Snapshot,
                pending.SettingsRevision,
                currentSettingsRevision))
        {
            ApplyLayoutOptions(
                pending.Snapshot.Options);
        }

        IReadOnlyList<PartitionViewModel>
            viewModels = OrganizerLayoutComposer.Compose(
                pending.Snapshot,
                IsPersonalizedView,
                _fileService.AllFiles.ToArray());
        PartitionCollectionSynchronizer.Synchronize(
            AllPartitions,
            PartitionsCol1,
            PartitionsCol2,
            viewModels);
        
        if (AllPartitions.Any()
            && (SelectedPartition == null
                || !AllPartitions.Contains(
                    SelectedPartition)))
        {
            SelectedPartition = AllPartitions.First();
        }
    }

    private void ApplyLayoutOptions(
        OrganizerLayoutOptions options)
    {
        _applyingLayoutOptions = true;
        try
        {
            IconScale = options.IconScale;
            IsListView = options.IsListView;
            IsPersonalizedView =
                options.IsPersonalizedView;
            IsAutoOrganizeEnabled =
                options.IsAutoOrganizeEnabled;
            CurrentViewMode = IsPersonalizedView
                ? "Personalized"
                : "Timeline";
        }
        finally
        {
            _applyingLayoutOptions = false;
        }
    }

    private void OnLayoutOptionsSaveFailed(
        OrganizerLayoutSaveState state,
        Exception error)
    {
        _ = state;
        _ = error;
        if (_isDisposed
            || _uiDispatcher.HasShutdownStarted
            || _uiDispatcher.HasShutdownFinished)
        {
            return;
        }

        _uiDispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!_isDisposed)
                {
                    AutoOrganizeStatus =
                        "布局仍在本次会话生效，但设置未能持久保存";
                }
            }),
            DispatcherPriority.Background);
    }

    private void OnLayoutOptionsSaved(
        OrganizerLayoutSaveState state)
    {
        _ = state;
        RequestLayoutRefresh();
    }

    [RelayCommand]
    private void ToggleView()
    {
        IsPersonalizedView = !IsPersonalizedView;
        CurrentViewMode = IsPersonalizedView ? "Personalized" : "Timeline";
        RequestLayoutRefresh();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await _fileService.RefreshFiles();
        RequestLayoutRefresh();
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
            RequestLayoutRefresh();
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
                RequestLayoutRefresh();
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
        
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
            RequestLayoutRefresh();
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
            RequestLayoutRefresh();
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
        RequestLayoutRefresh();
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
        _layoutRefresh.Dispose();
        _fileService.FilesChanged -=
            FileService_FilesChanged;
        _fileService.DesktopItemsCreated -=
            FileService_DesktopItemsCreated;
        _layoutSaveQueue.ItemSaved -=
            OnLayoutOptionsSaved;
        _layoutSaveQueue.ItemSaveFailed -=
            OnLayoutOptionsSaveFailed;
        _layoutSaveQueue.CompleteAsync()
            .GetAwaiter()
            .GetResult();
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
