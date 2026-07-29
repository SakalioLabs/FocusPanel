using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Models;

public sealed class TaskbarAppItem : ObservableObject
{
    private string _displayName = string.Empty;
    private ImageSource? _icon;
    private AppLaunchItem? _launchItem;
    private IReadOnlyList<AppLaunchItem> _pinnedLaunches =
        Array.Empty<AppLaunchItem>();
    private WindowTaskItem? _runningTask;

    public string IdentityKey { get; init; } = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        init => _displayName = value;
    }
    public ImageSource? Icon
    {
        get => _icon;
        init => _icon = value;
    }
    public AppLaunchItem? LaunchItem
    {
        get => _launchItem;
        init => _launchItem = value;
    }
    public IReadOnlyList<AppLaunchItem> PinnedLaunches
    {
        get => _pinnedLaunches;
        init => _pinnedLaunches = value;
    }
    public WindowTaskItem? RunningTask
    {
        get => _runningTask;
        init => _runningTask = value;
    }
    public bool IsPinned => PinnedLaunches.Count > 0;
    public bool IsRunning => RunningTask != null;
    public bool IsActive => RunningTask?.IsActive == true;
    public bool CanLaunchNewInstance =>
        !string.IsNullOrWhiteSpace(LaunchItem?.LaunchTarget)
        || !string.IsNullOrWhiteSpace(ApplicationUserModelId)
        || !string.IsNullOrWhiteSpace(ExecutablePath);
    public bool CanPin => IsPinned
        || !string.IsNullOrWhiteSpace(RunningTask?.ApplicationUserModelId)
        || !string.IsNullOrWhiteSpace(RunningTask?.ExecutablePath);
    public IReadOnlyList<WindowReference> Windows =>
        RunningTask?.Windows ?? Array.Empty<WindowReference>();
    public int WindowCount => Windows.Count;
    public string? ApplicationUserModelId => RunningTask?.ApplicationUserModelId;
    public string? ExecutablePath => RunningTask?.ExecutablePath;
    public string WindowSummary => WindowCount == 0 ? "未运行" : $"{WindowCount} 个窗口";
    public string StatusSummary =>
        IsActive
            ? $"正在使用 · {WindowCount} 个窗口"
            : IsRunning
                ? $"正在运行 · {WindowCount} 个窗口"
                : IsPinned
                    ? "已固定 · 未运行"
                    : "未运行";
    public string AccessibleName =>
        $"{DisplayName}，{StatusSummary}";
    public string InteractionHint
    {
        get
        {
            string primaryAction = WindowCount > 1
                ? "左键打开窗口列表，右键管理应用"
                : IsRunning
                    ? "左键切换或最小化，右键管理应用"
                    : "左键启动，右键管理应用";
            return CanLaunchNewInstance
                ? $"{primaryAction}；Shift+左键或中键启动新实例"
                : primaryAction;
        }
    }

    internal void ApplySnapshot(TaskbarAppItem snapshot)
    {
        if (!string.Equals(
                IdentityKey,
                snapshot.IdentityKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "不能把不同应用身份的快照合并到同一个任务栏项目。");
        }

        bool displayNameChanged = SetProperty(
            ref _displayName,
            snapshot.DisplayName,
            nameof(DisplayName));
        bool iconChanged = SetProperty(
            ref _icon,
            snapshot.Icon,
            nameof(Icon));
        bool launchChanged = !ReferenceEquals(
            _launchItem,
            snapshot.LaunchItem);
        bool pinnedChanged = !ReferenceEquals(
            _pinnedLaunches,
            snapshot.PinnedLaunches);
        bool runningChanged = !ReferenceEquals(
            _runningTask,
            snapshot.RunningTask);

        _launchItem = snapshot.LaunchItem;
        _pinnedLaunches = snapshot.PinnedLaunches;
        _runningTask = snapshot.RunningTask;

        if (launchChanged)
            OnPropertyChanged(nameof(LaunchItem));
        if (pinnedChanged)
            OnPropertyChanged(nameof(PinnedLaunches));
        if (runningChanged)
            OnPropertyChanged(nameof(RunningTask));

        if (displayNameChanged
            || iconChanged
            || launchChanged
            || pinnedChanged
            || runningChanged)
        {
            RaisePresentationChanged();
        }
    }

    private void RaisePresentationChanged()
    {
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanLaunchNewInstance));
        OnPropertyChanged(nameof(CanPin));
        OnPropertyChanged(nameof(Windows));
        OnPropertyChanged(nameof(WindowCount));
        OnPropertyChanged(nameof(ApplicationUserModelId));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(WindowSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(InteractionHint));
    }

    public AppLaunchItem? CreateLaunchItem()
    {
        if (LaunchItem != null)
            return LaunchItem;
        if (!string.IsNullOrWhiteSpace(ApplicationUserModelId))
        {
            return new AppLaunchItem
            {
                DisplayName = DisplayName,
                LaunchKind = AppLaunchKind.ShellApp,
                LaunchTarget = ApplicationUserModelId,
                IconKey = ApplicationUserModelId,
                Icon = Icon,
                IdentityKey = IdentityKey
            };
        }
        if (!string.IsNullOrWhiteSpace(ExecutablePath))
        {
            return new AppLaunchItem
            {
                DisplayName = DisplayName,
                LaunchKind = AppLaunchKind.Executable,
                LaunchTarget = ExecutablePath,
                IconKey = ExecutablePath,
                Icon = Icon,
                IdentityKey = IdentityKey
            };
        }
        return null;
    }
}
