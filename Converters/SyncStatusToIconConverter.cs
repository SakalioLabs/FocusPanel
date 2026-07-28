using System;
using System.Globalization;
using System.Windows.Data;
using FocusPanel.Models;

namespace FocusPanel.Converters;

public class SyncStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OkrSyncStatus status)
            return "\uE946";

        return status switch
        {
            OkrSyncStatus.Synced => "\uE73E",
            OkrSyncStatus.LocalCreated => "\uE898",
            OkrSyncStatus.LocalModified => "\uE70F",
            OkrSyncStatus.LocalDeleted => "\uE74D",
            _ => "\uE946"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class SyncStatusToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OkrSyncStatus status)
            return "未知同步状态";

        return status switch
        {
            OkrSyncStatus.Synced => "已与飞书同步",
            OkrSyncStatus.LocalCreated => "本地新建，等待提交",
            OkrSyncStatus.LocalModified => "本地已修改，等待提交",
            OkrSyncStatus.LocalDeleted => "本地已删除，等待提交",
            _ => "未知同步状态"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
