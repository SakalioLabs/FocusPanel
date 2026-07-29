using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Models;
using FocusPanel.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System;
using System.Threading;

namespace FocusPanel.ViewModels;

public class KanbanColumn : ObservableObject
{
    public string Header { get; set; } = string.Empty;
    public string DisplayHeader => Header switch
    {
        "To Do" => "待处理",
        "In Progress" => "进行中",
        "Done" => "已完成",
        _ => Header
    };
    public ObservableCollection<TodoItem> Tasks { get; set; } = new();
}

internal static class TaskBoardComposer
{
    internal static IReadOnlyList<string> GetColumnNames(
        string? columnsJson)
    {
        List<string>? parsed = null;
        if (!string.IsNullOrWhiteSpace(columnsJson))
        {
            try
            {
                parsed =
                    JsonSerializer.Deserialize<List<string>>(
                        columnsJson);
            }
            catch (JsonException)
            {
                parsed = null;
            }
        }

        List<string> names = (parsed
                ?? new List<string>
                {
                    "To Do",
                    "In Progress",
                    "Done"
                })
            .Where(name =>
                !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0
            ? new[]
            {
                "To Do",
                "In Progress",
                "Done"
            }
            : names;
    }

    internal static IReadOnlyList<KanbanColumn> Compose(
        IEnumerable<TodoItem> tasks,
        string? columnsJson)
    {
        List<KanbanColumn> columns =
            GetColumnNames(columnsJson)
                .Select(
                    name =>
                        new KanbanColumn
                        {
                            Header = name
                        })
                .ToList();
        foreach (TodoItem task in tasks)
        {
            KanbanColumn target =
                columns.FirstOrDefault(
                    column =>
                        string.Equals(
                            column.Header,
                            task.Status,
                            StringComparison.OrdinalIgnoreCase))
                ?? columns[0];
            target.Tasks.Add(task);
        }

        return columns;
    }

    internal static string? GetAdjacentStatus(
        string? currentStatus,
        string? columnsJson,
        int offset)
    {
        IReadOnlyList<string> columns =
            GetColumnNames(columnsJson);
        int currentIndex =
            columns
                .Select((name, index) =>
                    new { name, index })
                .Where(item =>
                    string.Equals(
                        item.name,
                        currentStatus,
                        StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(0)
                .First();
        int targetIndex = currentIndex + offset;
        return targetIndex >= 0
            && targetIndex < columns.Count
            ? columns[targetIndex]
            : null;
    }
}

public sealed record CustomFieldTypeOption(
    CustomFieldType Value,
    string DisplayName);

public partial class TasksViewModel
    : ObservableObject, IDisposable
{
    private readonly TaskService _taskService;
    private readonly SettingsService _settingsService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly TaskImageImportCoordinator
        _imageImporter;
    private readonly ShellPathOpenCoordinator
        _shellOpen;
    private readonly CoalescingAsyncSaveQueue<TodoItem>
        _taskSaveQueue;
    private Task? _disposeTask;
    private TodoItem? _lastSaveFailureItem;
    private bool _isDisposed;
    private int _loadGeneration;
    private string _globalCustomFieldsJson =
        string.Empty;

    // Unified Items List (Replaces RootItems and ChildItems)
    [ObservableProperty]
    private ObservableCollection<TodoItem> currentViewItems = new();

    [ObservableProperty]
    private ObservableCollection<KanbanColumn> boardColumns = new();
    
    // Current Context (Parent Item). Null means we are at the Root.
    [ObservableProperty]
    private TodoItem? currentParentItem;

    [ObservableProperty]
    private TodoItem? selectedTask; // Selected child item for detail view

    [ObservableProperty]
    private string newTaskTitle = string.Empty;

    // View Mode Support
    [ObservableProperty]
    private bool isListView = true;

    [ObservableProperty]
    private bool isBoardView = false;
    
    [ObservableProperty]
    private bool isSettingsView = false;
    
    [ObservableProperty]
    private bool isTaskDetailView = false;
    
    // Window Management Events
    public event Action<TodoItem>? OpenTaskDetailRequested;
    public event Action? CloseTaskDetailRequested;
    
    // Navigation Support
    public bool CanGoBack => CurrentParentItem != null;
    public bool IsProjectSelected => CurrentParentItem != null;

    // Custom Fields Support (Context Item)
    [ObservableProperty]
    private ObservableCollection<CustomFieldDefinition> customFieldDefinitions = new();
    
    [ObservableProperty]
    private string newFieldName = string.Empty;

    [ObservableProperty]
    private string newFieldOptions = string.Empty;
    
    [ObservableProperty]
    private CustomFieldType selectedFieldType = CustomFieldType.ShortText;

    partial void OnSelectedFieldTypeChanged(CustomFieldType value)
    {
        OnPropertyChanged(nameof(IsFieldTypeSelect));
    }

    public bool IsFieldTypeSelect => SelectedFieldType == CustomFieldType.SingleSelect || SelectedFieldType == CustomFieldType.MultiSelect;

    // Custom Fields Support (Task Detail)
    [ObservableProperty]
    private ObservableCollection<CustomFieldValueViewModel> currentTaskCustomFields = new();

    // Global Settings
    [ObservableProperty]
    private string imageSavePath = string.Empty;

    public IReadOnlyList<CustomFieldTypeOption> FieldTypes { get; } =
    new CustomFieldTypeOption[]
    {
        new(CustomFieldType.ShortText, "短文本"),
        new(CustomFieldType.LongText, "长文本 / Markdown"),
        new(CustomFieldType.SingleSelect, "单选"),
        new(CustomFieldType.MultiSelect, "多选")
    };

    [ObservableProperty]
    private string taskStatusMessage = string.Empty;

    public TasksViewModel()
        : this(
            new ShellFolderPickerService(),
            new WindowsFilePickerService())
    {
    }

    internal TasksViewModel(
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService,
        TaskImageImportCoordinator? imageImporter = null,
        ShellPathOpenCoordinator? shellOpen = null)
    {
        _folderPickerService =
            folderPickerService;
        _filePickerService =
            filePickerService;
        _imageImporter =
            imageImporter
            ?? new TaskImageImportCoordinator();
        _shellOpen =
            shellOpen
            ?? new ShellPathOpenCoordinator();
        _taskService = new TaskService();
        _taskSaveQueue =
            new CoalescingAsyncSaveQueue<TodoItem>(
                _taskService.UpdateItemAsync,
                TimeSpan.FromMilliseconds(180));
        _taskSaveQueue.ItemSaved +=
            OnQueuedItemSaved;
        _taskSaveQueue.ItemSaveFailed +=
            OnQueuedItemSaveFailed;
        _settingsService = new SettingsService();
        ImageSavePath = _settingsService.CurrentSettings.ImageSavePath;
        _globalCustomFieldsJson =
            _settingsService.CurrentSettings
                .GlobalCustomFieldsJson;
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoadCurrentViewItems();
            _globalCustomFieldsJson =
                await _taskService
                    .LoadGlobalCustomFieldsAsync(
                        _globalCustomFieldsJson);
            if (_isDisposed)
                return;
            LoadCustomFieldDefinitions();
        }
        catch (Exception ex)
        {
            TaskStatusMessage =
                $"无法加载任务：{ex.Message}";
        }
    }

    private void OnTodoItemPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isDisposed
            || sender is not TodoItem item)
        {
            return;
        }

        if (e.PropertyName == nameof(TodoItem.Status))
            RefreshBoardColumns();

        _taskSaveQueue.Enqueue(item);
    }

    private void OnQueuedItemSaved(TodoItem item)
    {
        PostToUi(
            () =>
            {
                if (!ReferenceEquals(
                        _lastSaveFailureItem,
                        item))
                {
                    return;
                }

                _lastSaveFailureItem = null;
                if (TaskStatusMessage.StartsWith(
                        "任务保存失败：",
                        StringComparison.Ordinal))
                {
                    TaskStatusMessage = string.Empty;
                }
            });
    }

    private void OnQueuedItemSaveFailed(
        TodoItem item,
        Exception error)
    {
        PostToUi(
            () =>
            {
                _lastSaveFailureItem = item;
                TaskStatusMessage =
                    $"任务保存失败：{error.Message}";
            });
    }

    private void PostToUi(Action action)
    {
        if (_isDisposed)
            return;

        System.Windows.Threading.Dispatcher? dispatcher =
            Application.Current?.Dispatcher;
        if (dispatcher == null
            || dispatcher.CheckAccess())
        {
            if (!_isDisposed)
                action();
            return;
        }

        if (dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    if (!_isDisposed)
                        action();
                }));
    }

    async partial void OnCurrentParentItemChanged(
        TodoItem? oldValue,
        TodoItem? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnTodoItemPropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnTodoItemPropertyChanged;

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsProjectSelected));
        UpdateViewMode();
        try
        {
            await LoadCurrentViewItems();
            LoadCustomFieldDefinitions();
        }
        catch (Exception ex)
        {
            TaskStatusMessage =
                $"无法切换任务范围：{ex.Message}";
        }
        CloseTaskDetail(); 
    }
    
    partial void OnSelectedTaskChanged(
        TodoItem? oldValue,
        TodoItem? newValue)
    {
        if (newValue != null)
        {
            IsTaskDetailView = true;
            LoadCurrentTaskCustomFields(newValue);
            OpenTaskDetailRequested?.Invoke(newValue);
        }
        else
        {
            IsTaskDetailView = false;
            CloseTaskDetailRequested?.Invoke();
        }
    }

    private void UpdateViewMode()
    {
        // Reset all
        IsListView = false;
        IsBoardView = false;
        IsSettingsView = false;
        IsTaskDetailView = false;

        // Default to List if at root or parent has no preference
        if (CurrentParentItem == null) 
        {
            IsListView = true;
            return;
        }
        
        // Map enum to boolean flags
        switch (CurrentParentItem.ViewMode)
        {
            case ProjectViewMode.List:
                IsListView = true;
                break;
            case ProjectViewMode.Board:
                IsBoardView = true;
                break;
        }
    }

    [RelayCommand]
    private async Task SwitchViewMode(string? mode)
    {
        IsListView = false;
        IsBoardView = false;
        IsSettingsView = false;
        IsTaskDetailView = false;
        SelectedTask = null;

        if (mode == "List")
        {
            if (CurrentParentItem != null) CurrentParentItem.ViewMode = ProjectViewMode.List;
            IsListView = true;
        }
        else if (mode == "Board")
        {
            if (CurrentParentItem != null) CurrentParentItem.ViewMode = ProjectViewMode.Board;
            IsBoardView = true;
        }
        else if (mode == "Settings")
        {
            IsSettingsView = true;
            return; 
        }

        // Save preference if we are in a context
        if (CurrentParentItem != null)
        {
            await SaveImmediatelyAsync(
                CurrentParentItem);
        }
        
        // Reload to refresh UI if needed
        if (!IsSettingsView)
        {
            await LoadCurrentViewItems();
        }
    }
    
    [RelayCommand]
    private void CloseTaskDetail()
    {
        SelectedTask = null;
        IsTaskDetailView = false;
    }

    [RelayCommand]
    private void OpenTaskDetail(TodoItem? item)
    {
        if (item != null)
            SelectedTask = item;
    }

    // --- Custom Fields Logic (Definition) ---

    private void LoadCustomFieldDefinitions()
    {
        CustomFieldDefinitions.Clear();
        string json =
            CurrentParentItem?
                .CustomFieldsJson
            ?? _globalCustomFieldsJson;

        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var fields = JsonSerializer.Deserialize<List<CustomFieldDefinition>>(json);
            if (fields != null)
            {
                foreach (var f in fields) CustomFieldDefinitions.Add(f);
            }
        }
        catch { }
    }
    
    [RelayCommand]
    private async Task AddCustomField()
    {
        if (string.IsNullOrWhiteSpace(NewFieldName)) return;

        var newField = new CustomFieldDefinition
        {
            Name = NewFieldName,
            Type = SelectedFieldType
        };

        if (IsFieldTypeSelect && !string.IsNullOrWhiteSpace(NewFieldOptions))
        {
            var options = NewFieldOptions.Split(new[] { ',', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
                                         .Select(o => o.Trim())
                                         .Where(o => !string.IsNullOrWhiteSpace(o))
                                         .ToList();
            newField.Options = options;
        }

        CustomFieldDefinitions.Add(newField);
        await SaveCustomFields();
        NewFieldName = string.Empty;
        NewFieldOptions = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteCustomField(CustomFieldDefinition field)
    {
        if (field == null) return;
        CustomFieldDefinitions.Remove(field);
        await SaveCustomFields();
    }

    private async Task SaveCustomFields()
    {
        var json = JsonSerializer.Serialize(CustomFieldDefinitions);

        if (CurrentParentItem != null)
        {
            CurrentParentItem.CustomFieldsJson = json;
            await SaveImmediatelyAsync(
                CurrentParentItem);
        }
        else
        {
            try
            {
                await _taskService
                    .SaveGlobalCustomFieldsAsync(
                        json);
                _globalCustomFieldsJson = json;
            }
            catch (Exception ex)
            {
                TaskStatusMessage =
                    $"全局字段保存失败：{ex.Message}";
                return;
            }
            
            _settingsService.CurrentSettings
                .GlobalCustomFieldsJson = json;
            bool backupSaved =
                await Task.Run(
                    _settingsService.SaveSettings);
            if (!backupSaved)
            {
                TaskStatusMessage =
                    _settingsService.LastError
                    ?? "全局字段已保存到数据库，但旧设置备份未更新。";
            }
        }
    }
    
    // --- Custom Fields Logic (Values) ---
    
    private void LoadCurrentTaskCustomFields(TodoItem task)
    {
        DeactivateCurrentTaskFields();
        CurrentTaskCustomFields.Clear();
        
        // Load definitions (Project or Global)
        LoadCustomFieldDefinitions(); 
        
        // Load values
        Dictionary<string, string> values = new();
        try
        {
            if (!string.IsNullOrEmpty(task.CustomValuesJson))
                values = JsonSerializer.Deserialize<Dictionary<string, string>>(task.CustomValuesJson) ?? new();
        }
        catch { }

        foreach (var def in CustomFieldDefinitions)
        {
            string val = values.ContainsKey(def.Id) ? values[def.Id] : string.Empty;
            CurrentTaskCustomFields.Add(
                new CustomFieldValueViewModel(
                    def,
                    val,
                    OnCustomFieldValueChanged,
                    _shellOpen));
        }
    }

    private void DeactivateCurrentTaskFields()
    {
        foreach (CustomFieldValueViewModel field
                 in CurrentTaskCustomFields)
        {
            field.Deactivate();
        }
    }

    private void OnCustomFieldValueChanged(string fieldId, string newValue)
    {
        if (SelectedTask == null) return;
        
        // Update JSON
        Dictionary<string, string> values = new();
        try
        {
            if (!string.IsNullOrEmpty(SelectedTask.CustomValuesJson))
                values = JsonSerializer.Deserialize<Dictionary<string, string>>(SelectedTask.CustomValuesJson) ?? new();
        }
        catch { }
        
        values[fieldId] = newValue;
        SelectedTask.CustomValuesJson = JsonSerializer.Serialize(values);
        
        // Save Task handled by PropertyChanged
        // await _taskService.UpdateItemAsync(SelectedTask);
    }

    // --- Settings Logic ---
    
    [RelayCommand]
    private async Task SelectImageSavePath()
    {
        FolderPickerResult result =
            _folderPickerService.PickFolder(
                new FolderPickerRequest(
                    "选择任务图片保存位置",
                    ImageSavePath,
                    "使用此文件夹"));
        FolderSelectionDecision decision =
            FolderSelectionPolicy.Resolve(result);
        if (!decision.ShouldApply
            && decision.Error == null)
        {
            return;
        }

        if (!decision.ShouldApply
            || string.IsNullOrWhiteSpace(decision.Path))
        {
            FocusDialogService.Show(
                decision.Error
                    ?? "Windows 没有返回有效的文件夹路径。",
                "无法选择文件夹",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        string previousPath = ImageSavePath;
        ImageSavePath = decision.Path;
        _settingsService.CurrentSettings.ImageSavePath =
            ImageSavePath;
        bool saved =
            await Task.Run(
                _settingsService.SaveSettings);
        if (saved)
            return;

        ImageSavePath = previousPath;
        _settingsService.CurrentSettings.ImageSavePath =
            previousPath;
        FocusDialogService.Show(
            _settingsService.LastError
                ?? "无法保存任务图片目录设置。",
            "设置未保存",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    // --- Image Handling for Markdown ---
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InsertImageToMarkdown(
        CustomFieldValueViewModel fieldViewModel)
    {
        if (fieldViewModel == null || !fieldViewModel.IsLongText) return;

        FilePickerResult result =
            _filePickerService.PickFile(
                new FilePickerRequest(
                    "选择要插入的图片",
                    "图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyPictures)));
        FileSelectionDecision decision =
            FileSelectionPolicy.Resolve(result);
        if (!decision.ShouldOpen
            && decision.Error == null)
        {
            return;
        }
        if (!decision.ShouldOpen
            || string.IsNullOrWhiteSpace(
                decision.Path))
        {
            FocusDialogService.Show(
                decision.Error
                    ?? "Windows 没有返回有效的图片路径。",
                "无法选择图片",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        TodoItem? targetTask =
            SelectedTask;
        TaskImageImportResult import =
            await _imageImporter.ImportAsync(
                decision.Path,
                ImageSavePath);
        if (_isDisposed
            || targetTask == null
            || !ReferenceEquals(
                targetTask,
                SelectedTask)
            || !CurrentTaskCustomFields.Contains(
                fieldViewModel))
        {
            return;
        }
        if (!import.Succeeded)
        {
            FocusDialogService.Show(
                $"插入图片失败：{import.Error}",
                "无法插入图片",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        string imageMarkdown =
            $"![图片]({import.SavedPath})";
        fieldViewModel.Value =
            string.IsNullOrEmpty(
                fieldViewModel.Value)
                ? imageMarkdown
                : fieldViewModel.Value
                    + $"\n{imageMarkdown}";
    }


    // --- Core Logic (Unified) ---

    [RelayCommand]
    private async Task LoadCurrentViewItems()
    {
        int loadGeneration =
            Interlocked.Increment(
                ref _loadGeneration);
        int? parentId =
            CurrentParentItem?.Id;
        List<TodoItem> previousItems =
            CurrentViewItems.ToList();
        foreach (TodoItem item in previousItems)
        {
            item.PropertyChanged -= OnTodoItemPropertyChanged;
        }

        await _taskSaveQueue.FlushAsync();

        List<TodoItem> items;
        try
        {
            if (!parentId.HasValue)
            {
                items =
                    await _taskService.GetRootItemsAsync();
            }
            else
            {
                items =
                    await _taskService.GetChildItemsAsync(
                        parentId.Value);
            }
        }
        catch
        {
            if (_isDisposed
                || loadGeneration
                != Volatile.Read(
                    ref _loadGeneration))
            {
                return;
            }

            foreach (TodoItem item in previousItems)
            {
                item.PropertyChanged +=
                    OnTodoItemPropertyChanged;
            }
            throw;
        }

        if (_isDisposed
            || loadGeneration
            != Volatile.Read(
                ref _loadGeneration))
        {
            return;
        }

        CurrentViewItems.Clear();
        foreach (TodoItem item in items)
        {
            item.PropertyChanged +=
                OnTodoItemPropertyChanged;
            CurrentViewItems.Add(item);
        }

        RefreshBoardColumns();
    }

    [RelayCommand]
    private async Task AddItem(string? status = null)
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        
        var targetStatus = status ?? "To Do"; 

        var task = new TodoItem
        {
            Title = NewTaskTitle,
            ParentId = CurrentParentItem?.Id, // Null if at root
            IsCompleted = false,
            Status = targetStatus,
            CreatedAt = System.DateTime.Now,
            ViewMode = ProjectViewMode.List // Default
        };

        await _taskService.AddItemAsync(task);
        task.PropertyChanged += OnTodoItemPropertyChanged;
        
        if (IsListView)
        {
            CurrentViewItems.Insert(0, task);
        }
        else if (IsBoardView)
        {
            CurrentViewItems.Insert(0, task);
        }

        RefreshBoardColumns();
        
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    private void ToggleTask(TodoItem? task)
    {
        // PropertyChanged event handles the save, but we keep this command for explicit UI actions if needed
        if (task == null) return;
        // await _taskService.UpdateItemAsync(task); // Redundant if PropertyChanged handles it
    }

    [RelayCommand]
    private async Task DeleteItem(TodoItem? item)
    {
        if (item == null) return;
        if (item.Title == "Inbox" && item.ParentId == null) 
        {
            FocusDialogService.Show(
                "收件箱是系统保留项目，不能删除。",
                "无法删除",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = FocusDialogService.Show(
            $"确定删除“{item.Title}”及其全部子任务吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        item.PropertyChanged -= OnTodoItemPropertyChanged;
        _taskSaveQueue.Discard(item);

        try
        {
            await _taskService.DeleteItemAsync(item);
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
            {
                item.PropertyChanged +=
                    OnTodoItemPropertyChanged;
                TaskStatusMessage =
                    $"任务删除失败：{ex.Message}";
            }
            return;
        }

        if (IsListView)
        {
            CurrentViewItems.Remove(item);
        }
        else if (IsBoardView)
        {
            CurrentViewItems.Remove(item);
        }
        
        if (SelectedTask == item) CloseTaskDetail();
        RefreshBoardColumns();
    }
    
    // --- Navigation Commands ---
    
    [RelayCommand]
    private void NavigateToItem(TodoItem? item)
    {
        if (item == null) return;
        CurrentParentItem = item;
    }

    [RelayCommand]
    private async Task NavigateUp()
    {
        if (CurrentParentItem == null) return;
        
        if (CurrentParentItem.ParentId.HasValue)
        {
            if (CurrentParentItem.Parent != null)
            {
                CurrentParentItem = CurrentParentItem.Parent;
            }
            else
            {
                // Fetch parent from service if not loaded in context
                var parent = await _taskService.GetItemByIdAsync(CurrentParentItem.ParentId.Value);
                CurrentParentItem = parent;
            }
        }
        else
        {
            CurrentParentItem = null;
        }
    }

    [RelayCommand]
    private async Task UpdateCurrentContext()
    {
        if (CurrentParentItem == null) return;
        await SaveImmediatelyAsync(
            CurrentParentItem);
    }

    private async Task SaveImmediatelyAsync(
        TodoItem item)
    {
        _taskSaveQueue.Discard(item);
        try
        {
            await _taskService.UpdateItemAsync(item);
            if (ReferenceEquals(
                    _lastSaveFailureItem,
                    item))
            {
                _lastSaveFailureItem = null;
            }
            if (TaskStatusMessage.StartsWith(
                    "任务保存失败：",
                    StringComparison.Ordinal))
            {
                TaskStatusMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _lastSaveFailureItem = item;
            TaskStatusMessage =
                $"任务保存失败：{ex.Message}";
            throw;
        }
    }

    [RelayCommand]
    private void MoveTaskNext(TodoItem? task)
    {
        if (task == null) return;

        string? newStatus =
            TaskBoardComposer.GetAdjacentStatus(
                task.Status,
                CurrentParentItem?.ColumnsJson,
                1);
        if (newStatus != null)
            MoveTaskStatusLogic(task, newStatus);
    }

    [RelayCommand]
    private void MoveTaskPrev(TodoItem? task)
    {
        if (task == null) return;

        string? newStatus =
            TaskBoardComposer.GetAdjacentStatus(
                task.Status,
                CurrentParentItem?.ColumnsJson,
                -1);
        if (newStatus != null)
            MoveTaskStatusLogic(task, newStatus);
    }

    private static void MoveTaskStatusLogic(
        TodoItem task,
        string newStatus)
    {
        if (task.Status == newStatus) return;

        task.Status = newStatus;
    }

    private void RefreshBoardColumns()
    {
        BoardColumns = new ObservableCollection<KanbanColumn>(
            TaskBoardComposer.Compose(
                CurrentViewItems,
                CurrentParentItem?.ColumnsJson));
    }

    internal Task DisposeAsync()
    {
        if (_disposeTask != null)
            return _disposeTask;

        _isDisposed = true;
        DeactivateCurrentTaskFields();
        Interlocked.Increment(
            ref _loadGeneration);
        if (CurrentParentItem != null)
        {
            CurrentParentItem.PropertyChanged -=
                OnTodoItemPropertyChanged;
        }
        foreach (TodoItem item in CurrentViewItems)
        {
            item.PropertyChanged -=
                OnTodoItemPropertyChanged;
        }

        _taskSaveQueue.ItemSaved -=
            OnQueuedItemSaved;
        _taskSaveQueue.ItemSaveFailed -=
            OnQueuedItemSaveFailed;
        CloseTaskDetailRequested?.Invoke();
        _disposeTask =
            CompleteDisposeAsync();
        return _disposeTask;
    }

    private async Task CompleteDisposeAsync()
    {
        await _taskSaveQueue.CompleteAsync()
            .ConfigureAwait(false);
        await _taskService
            .WaitForIdleAsync()
            .ConfigureAwait(false);
    }

    public void Dispose() =>
        DisposeAsync()
            .GetAwaiter()
            .GetResult();
}
