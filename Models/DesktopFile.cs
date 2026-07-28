using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public partial class DesktopFile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string name = string.Empty;

    public string DisplayName => Extension?.ToLower() == ".lnk" 
        ? System.IO.Path.GetFileNameWithoutExtension(Name) 
        : Name;

    [ObservableProperty]
    private string fullPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string extension = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeDisplay))]
    private long size;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateGroup))]
    private DateTime createdAt;

    [ObservableProperty]
    private ImageSource? icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Category))]
    private string fileType = string.Empty; // e.g. "Image", "Document"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Category))]
    private string? customPartition; // User defined partition
    
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isHidden; // 是否已收纳（桌面隐藏）

    [ObservableProperty]
    private bool needsRecovery;

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
            if (diff.Days == 0) return "今天";
            if (diff.Days == 1) return "昨天";
            if (diff.Days < 7) return "本周";
            if (diff.Days < 30) return "本月";
            return "更早";
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
