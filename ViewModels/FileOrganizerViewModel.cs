using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Models;
using FocusPanel.Services;
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
    private readonly InFlightTaskTracker
        _layoutMutationTracker = new();
    private readonly InFlightTaskTracker
        _organizeOperationTracker = new();
    private readonly DesktopDropPreflight
        _desktopDropPreflight = new();
    private readonly ShellPathOpenCoordinator
        _shellOpen;
    private readonly SemaphoreSlim
        _organizePresentationGate =
            new(1, 1);
    private bool _applyingLayoutOptions;
    private long _layoutSettingsRevision;
    private long _organizePresentationRevision;
    private bool _isDisposed;
    private Task? _disposeTask;

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
    private bool isListView = false;

    [ObservableProperty]
    private bool isAutoOrganizeEnabled;

    [ObservableProperty]
    private string autoOrganizeStatus =
        "关闭时不会处理新增桌面项目";

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasPendingCommonDesktopItems))]
    [NotifyCanExecuteChangedFor(
        nameof(CollectPendingCommonDesktopCommand))]
    private int pendingCommonDesktopCount;

    private IReadOnlyList<string>
        _pendingCommonDesktopPaths =
            Array.Empty<string>();

    public bool HasPendingCommonDesktopItems =>
        PendingCommonDesktopCount > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(OrganizeAllCommand))]
    [NotifyCanExecuteChangedFor(
        nameof(CollectPendingCommonDesktopCommand))]
    private bool isOrganizing;

    [ObservableProperty]
    private double organizeProgressValue;

    [ObservableProperty]
    private double organizeProgressMaximum = 1;

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
        Dispatcher uiDispatcher,
        ShellPathOpenCoordinator? shellOpen = null)
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
        _shellOpen =
            shellOpen
            ?? new ShellPathOpenCoordinator();

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

        RequestLayoutRefresh();
    }

    private void FileService_FilesChanged()
    {
        if (!IsOrganizing)
        {
            RefreshPendingCommonDesktopItems();
        }
        RequestLayoutRefresh();
    }

    private Task FileService_DesktopItemsCreated(
        IReadOnlyList<string> paths)
    {
        if (!IsAutoOrganizeEnabled
            || _isDisposed
            || paths.Count == 0)
        {
            return Task.CompletedTask;
        }

        Task<bool>? operation =
            _organizeOperationTracker.TryStart(
                async () =>
                {
                    await AutoOrganizeCreatedItemsAsync(
                        paths);
                    return true;
                });
        return operation
            ?? Task.CompletedTask;
    }

    private async Task AutoOrganizeCreatedItemsAsync(
        IReadOnlyList<string> paths)
    {
        await _organizePresentationGate
            .WaitAsync();
        try
        {
            long progressRevision =
                BeginOrganizePresentation(
                paths.Count,
                "正在自动收纳新增项目");
            DesktopOrganizeResult result =
                await _fileService.OrganizeFiles(
                    paths,
                    false,
                    CreateOrganizeProgress(
                        "自动收纳",
                        progressRevision));
            string status =
                DesktopAutoOrganizePolicy
                    .DescribeAutomaticResult(result);
            RefreshPendingCommonDesktopItems();
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
        finally
        {
            EndOrganizePresentation();
            _organizePresentationGate.Release();
        }
    }

    private void UpdatePendingCommonDesktopItems(
        IReadOnlyList<string>? paths)
    {
        _pendingCommonDesktopPaths =
            paths?
                .Where(path =>
                    !string.IsNullOrWhiteSpace(path))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray()
            ?? Array.Empty<string>();
        PendingCommonDesktopCount =
            _pendingCommonDesktopPaths.Count;
    }

    private void RefreshPendingCommonDesktopItems() =>
        UpdatePendingCommonDesktopItems(
            _fileService
                .GetCommonDesktopCandidatePaths());

    private bool CanCollectPendingCommonDesktop() =>
        !IsOrganizing
        && PendingCommonDesktopCount > 0;

    [RelayCommand(
        CanExecute = nameof(
            CanCollectPendingCommonDesktop))]
    private async Task CollectPendingCommonDesktop()
    {
        string[] paths =
            _pendingCommonDesktopPaths.ToArray();
        if (paths.Length == 0)
            return;

        var confirmation = FocusDialogService.Show(
            $"这 {paths.Length} 个快捷方式位于公共桌面，"
            + "需要一次管理员授权才能隐藏。\n\n"
            + "授权完成前不会修改任何图标。是否继续？",
            "授权收纳公共桌面项目",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmation
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        Task<bool>? operation =
            _organizeOperationTracker.TryStart(
                async () =>
                {
                    await CollectPendingCommonDesktopCore(
                        paths);
                    return true;
                });
        if (operation != null)
            await operation;
    }

    private async Task
        CollectPendingCommonDesktopCore(
            IReadOnlyList<string> paths)
    {
        await _organizePresentationGate
            .WaitAsync();
        try
        {
            long revision =
                BeginOrganizePresentation(
                    paths.Count,
                    "正在等待公共桌面授权");
            DesktopOrganizeResult result =
                await _fileService.OrganizeFiles(
                    paths,
                    true,
                    CreateOrganizeProgress(
                        "授权收纳",
                        revision));
            RefreshPendingCommonDesktopItems();
            RequestLayoutRefresh();
            AutoOrganizeStatus =
                result.Collected > 0
                    ? $"已授权收纳 {result.Collected} 个公共桌面项目"
                    : result.AuthorizationRequired > 0
                        ? "授权已取消；公共桌面图标保持原状"
                        : "公共桌面项目暂时无法收纳；图标保持原状";
        }
        catch (Exception ex)
        {
            AutoOrganizeStatus =
                "公共桌面授权收纳失败；图标保持原状";
            System.Diagnostics.Debug.WriteLine(
                "Collect pending common desktop items failed: "
                + ex);
        }
        finally
        {
            EndOrganizePresentation();
            _organizePresentationGate.Release();
        }
    }

    private IProgress<DesktopOrganizeProgress>
        CreateOrganizeProgress(
            string operationName,
            long revision) =>
        new SafeDispatcherProgress<
            DesktopOrganizeProgress>(
            _uiDispatcher,
            progress =>
            {
                if (_isDisposed
                    || !IsOrganizing
                    || revision != Volatile.Read(
                        ref _organizePresentationRevision))
                    return;

                OrganizeProgressValue =
                    progress.Processed;
                OrganizeProgressMaximum =
                    Math.Max(1, progress.Total);
                AutoOrganizeStatus =
                    $"{operationName} "
                    + $"{progress.Processed}/"
                    + $"{progress.Total} · "
                    + progress.CurrentItemName;
            },
            error =>
                System.Diagnostics.Debug.WriteLine(
                    "Desktop organize progress failed: "
                    + error.Message));

    private long BeginOrganizePresentation(
        int total,
        string status)
    {
        long revision =
            Interlocked.Increment(
                ref _organizePresentationRevision);
        IsOrganizing = true;
        OrganizeProgressValue = 0;
        OrganizeProgressMaximum =
            Math.Max(1, total);
        AutoOrganizeStatus = status;
        return revision;
    }

    private void EndOrganizePresentation()
    {
        Interlocked.Increment(
            ref _organizePresentationRevision);
        IsOrganizing = false;
        if (OrganizeProgressValue
            < OrganizeProgressMaximum)
        {
            OrganizeProgressValue =
                OrganizeProgressMaximum;
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

    private async Task<bool> RunLayoutMutationAsync(
        Func<bool> mutation,
        string operationName)
    {
        if (_isDisposed)
            return false;

        try
        {
            Task<bool>? work =
                _layoutMutationTracker.TryStart(
                    () => Task.Run(mutation));
            if (work == null)
                return false;

            bool changed = await work;
            if (!_isDisposed
                && changed)
            {
                RequestLayoutRefresh();
            }
            return changed;
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
            {
                FocusDialogService.Show(
                    $"{operationName}失败，原有布局未被修改：{ex.Message}",
                    "桌面收纳",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            return false;
        }
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

    private bool CanOrganizeAll() =>
        !IsOrganizing;

    [RelayCommand(
        CanExecute = nameof(CanOrganizeAll))]
    private async Task OrganizeAll()
    {
        Task<bool>? operation =
            _organizeOperationTracker.TryStart(
                async () =>
                {
                    await OrganizeAllCore();
                    return true;
                });
        if (operation != null)
            await operation;
    }

    private async Task OrganizeAllCore()
    {
        try
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

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            int commonDesktopCount =
                _fileService
                    .CountCommonDesktopCandidates();
            bool allowCommonDesktopElevation = false;
            if (commonDesktopCount > 0)
            {
                var authorize = FocusDialogService.Show(
                    $"其中 {commonDesktopCount} 个项目位于公共桌面，需要一次管理员授权。"
                    + "\n\n授权完成前不会隐藏任何图标；如果取消，本批保持原状。是否继续？",
                    "公共桌面授权",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (authorize
                    != System.Windows.MessageBoxResult.Yes)
                {
                    AutoOrganizeStatus =
                        "已取消整理；桌面图标保持原状";
                    return;
                }

                allowCommonDesktopElevation = true;
            }

            await _organizePresentationGate
                .WaitAsync();
            try
            {
                int initialTotal =
                    _fileService.Files.Count;
                long progressRevision =
                    BeginOrganizePresentation(
                    initialTotal,
                    $"正在整理 0/{initialTotal}");
                DesktopOrganizeResult organizeResult =
                    await _fileService.OrganizeAllFiles(
                        allowCommonDesktopElevation,
                        CreateOrganizeProgress(
                            "正在整理",
                            progressRevision));
                RefreshPendingCommonDesktopItems();
                int collected =
                    organizeResult.Collected;

                RequestLayoutRefresh();
                int remaining =
                    _fileService.Files.Count;
                AutoOrganizeStatus =
                    remaining == 0
                        ? $"已收纳 {collected} 个桌面项目"
                        : $"已收纳 {collected} 个；"
                        + $"{remaining} 个仍保留在桌面";
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
            finally
            {
                EndOrganizePresentation();
                _organizePresentationGate
                    .Release();
            }
        }
        catch (Exception ex)
        {
            AutoOrganizeStatus =
                "自动整理失败；未完成项目仍保留在桌面";
            System.Diagnostics.Debug.WriteLine(
                "Manual desktop organize failed: "
                + ex);
            FocusDialogService.Show(
                "自动整理没有完成。已成功收纳的项目仍可在面板中恢复，"
                + "其余项目保持在桌面。\n\n"
                + ex.Message,
                "自动整理失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenPartitionFolder(
        PartitionViewModel partition)
    {
        if (partition == null)
            return;
        string desktopPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop);
        await OpenShellPathAsync(
            desktopPath,
            "桌面文件夹");
    }

    [RelayCommand]
    private async Task CreatePartition(
        string? name = null)
    {
        string partitionName =
            (name ?? NewPartitionName).Trim();
        
        if (partitionName.Length == 0)
            return;

        bool changed =
            await RunLayoutMutationAsync(
                () =>
                    _layoutRepository
                        .CreatePartition(
                            partitionName),
                "创建收纳盒");
        if (changed
            && !IsPersonalizedView)
        {
            IsPersonalizedView = true;
            CurrentViewMode = "Personalized";
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

        await RunLayoutMutationAsync(
            () =>
                _layoutRepository.RenamePartition(
                    oldName,
                    newName),
            "重命名收纳盒");
    }
    
    [RelayCommand]
    private void CancelRename()
    {
        IsRenameDialogOpen = false;
    }

    [RelayCommand]
    private async Task DeletePartition(
        PartitionViewModel? partition)
    {
        if (partition == null) return;

        var result = FocusDialogService.Show(
            $"确定删除分区“{partition.Name}”吗？\n分区内文件会变为未分类，但仍保留在桌面。",
            "删除分区",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        await RunLayoutMutationAsync(
            () =>
                _layoutRepository.DeletePartition(
                    partition.Name),
            "删除收纳盒");
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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenFile(DesktopFile file)
    {
        if (file == null
            || string.IsNullOrWhiteSpace(
                file.FullPath))
            return;
        await OpenShellPathAsync(
            file.FullPath,
            file.Name);
    }

    private async Task OpenShellPathAsync(
        string path,
        string displayName)
    {
        ShellPathOpenCompletion completion =
            await _shellOpen.OpenAsync(path);
        if (_isDisposed
            || !_shellOpen.IsCurrent(
                completion.Revision)
            || completion.Succeeded)
        {
            return;
        }

        FocusDialogService.Show(
            $"无法打开“{displayName}”。项目可能已被移动、删除，"
            + "或 Windows 暂时无法处理该文件类型。",
            "打开失败",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    public async Task ReorderPartition(
        PartitionViewModel source,
        PartitionViewModel target,
        bool insertAfter = false)
    {
        if (source == null || target == null || source == target) return;
        if (!IsPersonalizedView) return;

        await RunLayoutMutationAsync(
            () =>
                _layoutRepository.ReorderPartition(
                    source.Name,
                    target.Name,
                    insertAfter),
            "调整收纳盒顺序");
    }

    public async Task MovePartitionToColumn(
        PartitionViewModel source,
        int targetColumn)
    {
        if (source == null || !IsPersonalizedView) return;
        if (source.ColumnIndex == targetColumn) return; // Already in column, no change if just dropped on empty space

        await RunLayoutMutationAsync(
            () =>
                _layoutRepository
                    .MovePartitionToColumn(
                        source.Name,
                        targetColumn),
            "移动收纳盒");
    }

    public async Task AssignFileToPartition(
        DesktopFile? file,
        string? partitionName)
    {
        if (file == null) return;

        string fileName = file.Name;
        await RunLayoutMutationAsync(
            () =>
                _layoutRepository
                    .AssignFileToPartition(
                        fileName,
                        partitionName ?? string.Empty),
            "更新文件分类");
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
    private async Task RestoreFileFromPanel(
        DesktopFile? file)
    {
        if (file == null) return;

        await RestoreDraggedFileToDesktop(file);
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
        int failed = 0;
        int authorizationCanceled = 0;
        bool? commonDesktopApproved = null;
        DesktopDropPreflightResult preflight =
            await _desktopDropPreflight
                .ResolveAsync(
                    filePaths,
                    desktopPath,
                    commonDesktopPath);

        foreach (DesktopDropCandidate candidate
                 in preflight.Candidates)
        {
            try
            {
                bool allowElevation = false;
                if (candidate.Location
                    == DesktopDropLocation
                        .CommonDesktop)
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
                    candidate.FullPath,
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
            preflight.OutsideDesktop,
            failed,
            authorizationCanceled,
            preflight.MissingOrInvalid,
            preflight.SkippedDuplicates);
    }

    internal Task DisposeAsync()
    {
        if (_disposeTask != null)
            return _disposeTask;

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
        _disposeTask =
            CompleteDisposeAsync();
        return _disposeTask;
    }

    private async Task CompleteDisposeAsync()
    {
        await _layoutSaveQueue.CompleteAsync()
            .ConfigureAwait(false);
        try
        {
            await _layoutMutationTracker.CompleteAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Complete organizer layout mutation failed: "
                + ex.Message);
        }
        try
        {
            await _organizeOperationTracker
                .CompleteAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "Complete organizer operation failed: "
                + ex.Message);
        }
        _fileService.Dispose();
    }

    public void Dispose() =>
        DisposeAsync()
            .GetAwaiter()
            .GetResult();
}

public sealed record DesktopImportResult(
    int Collected = 0,
    int OutsideDesktop = 0,
    int Failed = 0,
    int AuthorizationCanceled = 0,
    int MissingOrInvalid = 0,
    int SkippedDuplicates = 0)
{
    public bool HasIssues =>
        OutsideDesktop
        + Failed
        + AuthorizationCanceled
        + MissingOrInvalid
        + SkippedDuplicates
        > 0;
}
