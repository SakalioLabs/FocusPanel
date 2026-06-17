using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public partial class DesktopFile : ObservableObject
{
    [ObservableProperty]
    private string name;

    public string DisplayName => Extension?.ToLower() == ".lnk" 
        ? System.IO.Path.GetFileNameWithoutExtension(Name) 
        : Name;

    [ObservableProperty]
    private string fullPath;

    [ObservableProperty]
    private string extension;

    [ObservableProperty]
    private long size;

    [ObservableProperty]
    private DateTime createdAt;

    [ObservableProperty]
    private ImageSource icon;

    [ObservableProperty]
    private string fileType; // e.g. "Image", "Document"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Category))]
    private string customPartition; // User defined partition
    
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isHidden; // 是否已收纳（桌面隐藏）

    [ObservableProperty]
    private double desktopX;

    [ObservableProperty]
    private double desktopY;

    public string Category => !string.IsNullOrEmpty(CustomPartition) ? CustomPartition : FileType;

    public string DateGroup
    {
        get
        {
            var now = DateTime.Now;
            var diff = now.Date - CreatedAt.Date;
            if (diff.Days == 0) return "Today";
            if (diff.Days == 1) return "Yesterday";
            if (diff.Days < 7) return "This Week";
            if (diff.Days < 30) return "This Month";
            return "Older";
        }
    }

    public string SizeDisplay
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
            if (Size < 1024 * 1024 * 1024) return $"{Size / 1024.0 / 1024.0:F1} MB";
            return $"{Size / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }
    }
}
