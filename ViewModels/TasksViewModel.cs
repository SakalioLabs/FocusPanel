using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System;

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
    private readonly AppDbContext _context; // Keep context alive
    private readonly SettingsService _settingsService;

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
    {
        _context = new AppDbContext();
        _taskService = new TaskService(_context);
        _settingsService = new SettingsService();
        ImageSavePath = _settingsService.CurrentSettings.ImageSavePath;
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoadCurrentViewItems();
            LoadCustomFieldDefinitions();
        }
        catch (Exception ex)
        {
            TaskStatusMessage =
                $"无法加载任务：{ex.Message}";
        }
    }

    private async void OnTodoItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TodoItem item)
        {
            try
            {
                if (e.PropertyName == nameof(TodoItem.Status))
                    RefreshBoardColumns();
                await _taskService.UpdateItemAsync(item);
                TaskStatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                TaskStatusMessage =
                    $"任务保存失败：{ex.Message}";
            }
        }
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
            await _taskService.UpdateItemAsync(CurrentParentItem);
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
        string json = string.Empty;

        if (CurrentParentItem != null)
        {
            json = CurrentParentItem.CustomFieldsJson;
        }
        else
        {
            // Global fields (from AppConfig)
            try
            {
                using (var context = new AppDbContext())
                {
                    var config = context.AppConfigs.Find("GlobalCustomFieldsJson");
                    if (config != null)
                    {
                        json = config.Value;
                    }
                    else
                    {
                        // Migration: Load from Settings, and save to DB for next time
                        json = _settingsService.CurrentSettings.GlobalCustomFieldsJson;
                        if (!string.IsNullOrEmpty(json))
                        {
                            context.AppConfigs.Add(new AppConfig { Key = "GlobalCustomFieldsJson", Value = json });
                            context.SaveChanges();
                        }
                    }
                }
            }
            catch 
            {
                // Fallback to settings.json temporarily if DB fails
                json = _settingsService.CurrentSettings.GlobalCustomFieldsJson;
            }
        }

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
            await _taskService.UpdateItemAsync(CurrentParentItem);
        }
        else
        {
            // Save Global (to AppConfig)
            try
            {
                using (var context = new AppDbContext())
                {
                    var config = await context.AppConfigs.FindAsync("GlobalCustomFieldsJson");
                    if (config == null)
                    {
                        config = new AppConfig { Key = "GlobalCustomFieldsJson", Value = json };
                        context.AppConfigs.Add(config);
                    }
                    else
                    {
                        config.Value = json;
                    }
                    await context.SaveChangesAsync();
                }
            }
            catch { }
            
            // Still save to settings.json as backup/legacy
            _settingsService.CurrentSettings.GlobalCustomFieldsJson = json;
            _settingsService.SaveSettings();
        }
    }
    
    // --- Custom Fields Logic (Values) ---
    
    private void LoadCurrentTaskCustomFields(TodoItem task)
    {
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
            CurrentTaskCustomFields.Add(new CustomFieldValueViewModel(def, val, OnCustomFieldValueChanged));
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
    private void SelectImageSavePath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ImageSavePath = dialog.SelectedPath;
            _settingsService.CurrentSettings.ImageSavePath = ImageSavePath;
            _settingsService.SaveSettings();
        }
    }

    // --- Image Handling for Markdown ---
    [RelayCommand]
    private void InsertImageToMarkdown(CustomFieldValueViewModel fieldViewModel)
    {
        if (fieldViewModel == null || !fieldViewModel.IsLongText) return;
        
        // Open File Dialog
        var openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
        if (openFileDialog.ShowDialog() == true)
        {
            try 
            {
                string savedPath = SaveImageForMarkdown(openFileDialog.FileName);
                // Insert markdown syntax
                string imageMarkdown = $"![Image]({savedPath})";
                
                // Append or Insert at cursor (cursor position tricky in MVVM, just append for now)
                if (string.IsNullOrEmpty(fieldViewModel.Value))
                    fieldViewModel.Value = imageMarkdown;
                else
                    fieldViewModel.Value += $"\n{imageMarkdown}";
            }
            catch(System.Exception ex)
            {
                FocusDialogService.Show(
                    $"插入图片失败：{ex.Message}",
                    "无法插入图片",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    public string SaveImageForMarkdown(string sourceFilePath)
    {
        if (!Directory.Exists(ImageSavePath))
        {
            Directory.CreateDirectory(ImageSavePath);
        }

        string fileName = Path.GetFileName(sourceFilePath);
        string destPath = Path.Combine(ImageSavePath, $"{System.Guid.NewGuid()}_{fileName}");
        
        File.Copy(sourceFilePath, destPath);
        return destPath;
    }


    // --- Core Logic (Unified) ---

    [RelayCommand]
    private async Task LoadCurrentViewItems()
    {
        foreach (var item in CurrentViewItems)
        {
            item.PropertyChanged -= OnTodoItemPropertyChanged;
        }
        CurrentViewItems.Clear();
        
        List<TodoItem> items;
        if (CurrentParentItem == null)
        {
            items = await _taskService.GetRootItemsAsync();
        }
        else
        {
            items = await _taskService.GetChildItemsAsync(CurrentParentItem.Id);
        }

        foreach (var t in items)
        {
            t.PropertyChanged += OnTodoItemPropertyChanged;
            CurrentViewItems.Add(t);
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

        await _taskService.DeleteItemAsync(item);
        item.PropertyChanged -= OnTodoItemPropertyChanged;
        
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
        await _taskService.UpdateItemAsync(CurrentParentItem);
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

    public void Dispose()
    {
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

        CloseTaskDetailRequested?.Invoke();
        _context.Dispose();
    }
}
