using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Models;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using FocusPanel.Services;

namespace FocusPanel.ViewModels;

public partial class CustomFieldValueViewModel : ObservableObject
{
    public CustomFieldDefinition Definition { get; }
    
    private readonly Action<string, string> _onValueChanged;
    private readonly ShellPathOpenCoordinator _shellOpen;
    private bool _isActive = true;

    [ObservableProperty]
    private string value = string.Empty;

    partial void OnValueChanged(string value)
    {
        _onValueChanged(Definition.Id, value);
        if (IsLongText)
        {
            ExtractImagesFromMarkdown();
        }
    }

    // For Select types, provide Options
    public List<string> Options => Definition.Options ?? new List<string>();
    
    // Helper to check type for UI visibility
    public bool IsShortText => Definition.Type == CustomFieldType.ShortText;
    public bool IsLongText => Definition.Type == CustomFieldType.LongText;
    public bool IsSingleSelect => Definition.Type == CustomFieldType.SingleSelect;
    public bool IsMultiSelect => Definition.Type == CustomFieldType.MultiSelect;

    // For MultiSelect
    public ObservableCollection<SelectableOption> MultiSelectOptions { get; private set; } = new();

    // For Markdown Images
    public ObservableCollection<string> ExtractedImages { get; } = new();

    public CustomFieldValueViewModel(
        CustomFieldDefinition def,
        string initialValue,
        Action<string, string> onValueChanged)
        : this(
            def,
            initialValue,
            onValueChanged,
            new ShellPathOpenCoordinator())
    {
    }

    internal CustomFieldValueViewModel(
        CustomFieldDefinition def,
        string initialValue,
        Action<string, string> onValueChanged,
        ShellPathOpenCoordinator shellOpen)
    {
        Definition = def;
        value = initialValue;
        _onValueChanged = onValueChanged;
        _shellOpen =
            shellOpen
            ?? throw new ArgumentNullException(
                nameof(shellOpen));

        if (IsMultiSelect)
        {
            var selectedValues = new HashSet<string>(value?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>());
            foreach (var opt in Options)
            {
                MultiSelectOptions.Add(new SelectableOption(opt, selectedValues.Contains(opt), UpdateMultiSelectValue));
            }
        }

        if (IsLongText)
        {
            ExtractImagesFromMarkdown();
        }
    }

    private void ExtractImagesFromMarkdown()
    {
        ExtractedImages.Clear();
        if (string.IsNullOrWhiteSpace(Value)) return;

        // Regex to find ![alt](url)
        var regex = new Regex(@"!\[.*?\]\((.*?)\)");
        var matches = regex.Matches(Value);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                string path = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    ExtractedImages.Add(path);
                }
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ShellPathOpenCompletion completion =
            await _shellOpen.OpenAsync(path);
        if (!_isActive
            || !_shellOpen.IsCurrent(
                completion.Revision)
            || completion.Succeeded)
        {
            return;
        }

        FocusDialogService.Show(
            "无法打开图片。文件可能已被移动、删除，"
            + "或 Windows 暂时无法处理该图片类型。",
            "打开图片失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    internal void Deactivate() =>
        _isActive = false;

    private void UpdateMultiSelectValue()
    {
        var selected = MultiSelectOptions.Where(o => o.IsSelected).Select(o => o.Name);
        Value = string.Join(",", selected);
    }
}

public partial class SelectableOption : ObservableObject
{
    public string Name { get; }
    
    [ObservableProperty]
    private bool isSelected;
    
    private readonly Action _onSelectionChanged;

    public SelectableOption(string name, bool selected, Action onSelectionChanged)
    {
        Name = name;
        isSelected = selected;
        _onSelectionChanged = onSelectionChanged;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged();
    }
}
