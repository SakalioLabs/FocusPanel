using CommunityToolkit.Mvvm.ComponentModel;
using FocusPanel.Models;
using System.Collections.ObjectModel;

namespace FocusPanel.ViewModels;

public partial class PartitionViewModel : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool isCustom;

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private bool isLocked;

    public ObservableCollection<DesktopFile> Files { get; } = new();

    // Command to handle file dropping or adding (future)
    // For now, just a container.
    
    [ObservableProperty]
    private int columnIndex; // 0 or 1

    public PartitionViewModel(string name)
    {
        Name = name;
    }
}
