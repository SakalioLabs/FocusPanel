using System;
using System.Globalization;
using System.Windows.Data;
using FocusPanel.Models;

namespace FocusPanel.Converters;

public class SyncStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OkrSyncStatus status) return "HelpCircle";
        return status switch
        {
            OkrSyncStatus.Synced => "CheckCircle",
            OkrSyncStatus.LocalCreated => "CloudUpload",
            OkrSyncStatus.LocalModified => "Pencil",
            OkrSyncStatus.LocalDeleted => "Delete",
            _ => "HelpCircle"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SyncStatusToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OkrSyncStatus status) return "Unknown";
        return status switch
        {
            OkrSyncStatus.Synced => "Synced with Feishu",
            OkrSyncStatus.LocalCreated => "New locally (pending push)",
            OkrSyncStatus.LocalModified => "Modified locally (pending push)",
            OkrSyncStatus.LocalDeleted => "Deleted locally (pending push)",
            _ => "Unknown"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
