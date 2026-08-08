using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusPanel.Services;

namespace FocusPanel.Models;

public sealed class TaskbarAppItem : ObservableObject
{
    private const int WindowPreviewLimit = 3;
    private const int WindowTitleLimit = 64;

    private string _displayName = string.Empty;
    private ImageSource? _icon;
    private AppLaunchItem? _launchItem;
    private IReadOnlyList<AppLaunchItem> _pinnedLaunches =
        Array.Empty<AppLaunchItem>();
    private WindowTaskItem? _runningTask;
    private TaskbarDropPlacement? _dropPlacement;
    private bool _isFileDropTarget;
    private bool _isStatusCenterWindowListExpanded;
    private TaskbarSlotShortcutState
        _shortcutState =
            TaskbarSlotShortcutState.None;

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
    public bool IsBackgroundOnly =>
        IsRunning && WindowCount == 0;
    public bool IsActive => RunningTask?.IsActive == true;
    public bool IsAttentionRequested =>
        RunningTask?.IsAttentionRequested == true;
    public bool CanLaunchNewInstance =>
        !string.IsNullOrWhiteSpace(LaunchItem?.LaunchTarget)
        || !string.IsNullOrWhiteSpace(ApplicationUserModelId)
        || !string.IsNullOrWhiteSpace(ExecutablePath);
    public bool CanLaunchElevated =>
        CreateElevatedLaunchItem() != null;
    public bool CanPin => IsPinned
        || !string.IsNullOrWhiteSpace(RunningTask?.ApplicationUserModelId)
        || !string.IsNullOrWhiteSpace(RunningTask?.ExecutablePath);
    public IReadOnlyList<WindowReference> Windows =>
        RunningTask?.Windows ?? Array.Empty<WindowReference>();
    public int WindowCount => Windows.Count;
    public bool HasMultipleWindows =>
        WindowCount > 1;
    public bool IsFullyMinimized =>
        WindowCount > 0
        && Windows.All(
            window =>
                window.State
                == TrackedWindowState.Minimized);
    public bool HasWindowPreview =>
        WindowCount > 0;
    public string WindowCountBadgeText =>
        WindowCount > 99
            ? "99+"
            : WindowCount.ToString();
    public string WindowPreviewText =>
        ComposeWindowPreview();
    public string? ApplicationUserModelId => RunningTask?.ApplicationUserModelId;
    public string?
        JumpListApplicationUserModelId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    ApplicationUserModelId))
            {
                return ApplicationUserModelId;
            }

            string? catalogApplicationUserModelId =
                LaunchItem?.ApplicationUserModelId
                ?? PinnedLaunches
                    .Select(
                        item =>
                            item.ApplicationUserModelId)
                    .FirstOrDefault(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value));
            if (!string.IsNullOrWhiteSpace(
                    catalogApplicationUserModelId))
            {
                return catalogApplicationUserModelId;
            }

            const string prefix =
                "aumid:";
            return IdentityKey.StartsWith(
                    prefix,
                    StringComparison
                        .OrdinalIgnoreCase)
                && IdentityKey.Length
                    > prefix.Length
                ? IdentityKey[
                    prefix.Length..]
                : null;
        }
    }
    public string? ExecutablePath => RunningTask?.ExecutablePath;
    public string WindowSummary =>
        IsAttentionRequested
            ? "需要注意"
            : IsBackgroundOnly
            ? "后台运行 · 无可见窗口"
            : WindowCount == 0
                ? "未运行"
                : $"{WindowCount} 个窗口";
    public string StatusSummary =>
        IsActive
            ? $"正在使用 · {WindowCount} 个窗口"
            : IsAttentionRequested
                ? $"需要注意 · {WindowCount} 个窗口"
            : IsFullyMinimized
                ? $"已最小化 · {WindowCount} 个窗口"
                : IsBackgroundOnly
                    ? "后台运行 · 无可见窗口"
                : IsRunning
                    ? $"后台运行 · {WindowCount} 个窗口"
                    : IsPinned
                        ? "已固定 · 未运行"
                        : "未运行";
    public string ShortcutGestureText =>
        !_shortcutState.IsAvailable
            ? string.Empty
            : _shortcutState.CanActivateOrLaunch
                ? $"Ctrl+Alt+{_shortcutState.SlotNumber}"
                : $"Ctrl+Alt+Shift+{_shortcutState.SlotNumber}";
    public string ShortcutSlotText =>
        _shortcutState.IsAvailable
            ? _shortcutState.SlotNumber
                ?.ToString()
              ?? string.Empty
            : string.Empty;
    public string ShortcutDescription =>
        !_shortcutState.IsAvailable
            ? string.Empty
            : _shortcutState.CanActivateOrLaunch
                && _shortcutState.CanLaunchNewInstance
                ? $"Ctrl+Alt+{_shortcutState.SlotNumber} 启动或切换，"
                  + "加 Shift 启动新实例"
                : _shortcutState.CanActivateOrLaunch
                    ? $"Ctrl+Alt+{_shortcutState.SlotNumber} 启动或切换"
                    : $"Ctrl+Alt+Shift+{_shortcutState.SlotNumber} 启动新实例";
    public bool HasShortcutGesture =>
        _shortcutState.IsAvailable;
    public string AccessibleName =>
        HasShortcutGesture
            ? $"{DisplayName}，{StatusSummary}，"
              + $"快速键 {ShortcutDescription}"
            : $"{DisplayName}，{StatusSummary}";
    public string InteractionHint
    {
        get
        {
            string primaryAction = WindowCount > 1
                ? "左键固定实时窗口缩略卡，再次左键收起；Ctrl+左键或 Ctrl+滚轮循环窗口，右键管理应用"
                : IsBackgroundOnly
                    ? "左键请求应用打开界面，右键管理应用"
                : IsFullyMinimized
                    ? "左键还原并切换，右键管理应用"
                    : IsActive
                        ? "左键最小化，右键管理应用"
                        : IsRunning
                            ? "左键切换，右键管理应用"
                            : "左键启动，右键管理应用";
            string interaction =
                CanLaunchNewInstance
                ? $"{primaryAction}；Shift+左键或中键启动新实例；"
                  + (CanLaunchElevated
                      ? "Ctrl+Shift+左键以管理员身份启动；"
                      : string.Empty)
                  + "可拖入文件用此应用打开"
                : primaryAction;
            string hint = IsPinned
                ? $"{interaction}；Alt+↑/↓调整固定顺序"
                : interaction;
            return HasShortcutGesture
                ? $"{hint}；{ShortcutDescription}"
                : hint;
        }
    }
    public string AccessibleInteractionHint =>
        $"{InteractionHint}；↑/↓浏览应用，Home/End 到首尾，PageUp/PageDown 翻页";
    public bool ShowsDropBefore =>
        _dropPlacement
        == TaskbarDropPlacement.Before;
    public bool ShowsDropAfter =>
        _dropPlacement
        == TaskbarDropPlacement.After;
    public bool IsFileDropTarget =>
        _isFileDropTarget;
    public bool IsStatusCenterWindowListExpanded
    {
        get => _isStatusCenterWindowListExpanded;
        internal set => SetProperty(
            ref _isStatusCenterWindowListExpanded,
            value);
    }

    internal void SetShortcutState(
        TaskbarSlotShortcutState state)
    {
        if (_shortcutState == state)
        {
            return;
        }

        _shortcutState = state;
        OnPropertyChanged(
            nameof(ShortcutGestureText));
        OnPropertyChanged(
            nameof(ShortcutSlotText));
        OnPropertyChanged(
            nameof(ShortcutDescription));
        OnPropertyChanged(
            nameof(HasShortcutGesture));
        OnPropertyChanged(
            nameof(AccessibleName));
        OnPropertyChanged(
            nameof(InteractionHint));
        OnPropertyChanged(
            nameof(AccessibleInteractionHint));
    }

    private string ComposeWindowPreview()
    {
        if (WindowCount == 0)
            return string.Empty;

        var lines = Windows
            .Take(WindowPreviewLimit)
            .Select(window =>
                "• "
                + NormalizeWindowTitle(
                    window.Title)
                + GetWindowStateSuffix(
                    window))
            .ToList();
        if (WindowCount > WindowPreviewLimit)
        {
            lines.Add(
                $"• 另有 {WindowCount - WindowPreviewLimit} 个窗口");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string GetWindowStateSuffix(
        WindowReference window)
    {
        var states = new List<string>(2);
        if (window.IsActive)
        {
            states.Add("当前窗口");
        }
        else if (window.State
                 == TrackedWindowState.Minimized)
        {
            states.Add("已最小化");
        }
        else if (window.State
                 == TrackedWindowState.Maximized)
        {
            states.Add("已最大化");
        }

        if (window.IsTopmost)
            states.Add("已置顶");
        if (window.HasDisplayLabel)
            states.Add(window.DisplayLabel);
        return states.Count == 0
            ? string.Empty
            : " · " + string.Join(" · ", states);
    }

    private string NormalizeWindowTitle(
        string? title)
    {
        string normalized =
            string.IsNullOrWhiteSpace(title)
                ? DisplayName
                : title
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();
        int[] textElements =
            StringInfo.ParseCombiningCharacters(
                normalized);
        return textElements.Length
                <= WindowTitleLimit
            ? normalized
            : normalized[
                ..textElements[
                    WindowTitleLimit - 1]]
                + "…";
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
        if (snapshot.WindowCount <= 1)
        {
            IsStatusCenterWindowListExpanded =
                false;
        }

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

    internal void SetDropPlacement(
        TaskbarDropPlacement? placement)
    {
        if (_dropPlacement == placement)
            return;

        _dropPlacement = placement;
        OnPropertyChanged(
            nameof(ShowsDropBefore));
        OnPropertyChanged(
            nameof(ShowsDropAfter));
    }

    internal void SetFileDropTarget(
        bool isTarget)
    {
        if (_isFileDropTarget
            == isTarget)
        {
            return;
        }

        _isFileDropTarget =
            isTarget;
        OnPropertyChanged(
            nameof(IsFileDropTarget));
    }

    private void RaisePresentationChanged()
    {
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsBackgroundOnly));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(
            nameof(IsAttentionRequested));
        OnPropertyChanged(nameof(CanLaunchNewInstance));
        OnPropertyChanged(nameof(CanLaunchElevated));
        OnPropertyChanged(nameof(CanPin));
        OnPropertyChanged(nameof(Windows));
        OnPropertyChanged(nameof(WindowCount));
        OnPropertyChanged(nameof(HasMultipleWindows));
        OnPropertyChanged(nameof(IsFullyMinimized));
        OnPropertyChanged(nameof(HasWindowPreview));
        OnPropertyChanged(nameof(WindowCountBadgeText));
        OnPropertyChanged(nameof(WindowPreviewText));
        OnPropertyChanged(nameof(ApplicationUserModelId));
        OnPropertyChanged(
            nameof(
                JumpListApplicationUserModelId));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(WindowSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(InteractionHint));
        OnPropertyChanged(
            nameof(AccessibleInteractionHint));
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

    public AppLaunchItem?
        CreateElevatedLaunchItem()
    {
        if (LaunchItem != null
            && LaunchItem.LaunchKind
                is AppLaunchKind.Executable
                    or AppLaunchKind.Shortcut
            && !string.IsNullOrWhiteSpace(
                LaunchItem.LaunchTarget))
        {
            return LaunchItem;
        }

        if (string.IsNullOrWhiteSpace(
                ApplicationUserModelId)
            && !string.IsNullOrWhiteSpace(
                ExecutablePath))
        {
            return new AppLaunchItem
            {
                DisplayName = DisplayName,
                LaunchKind =
                    AppLaunchKind.Executable,
                LaunchTarget =
                    ExecutablePath,
                IconKey = ExecutablePath,
                Icon = Icon,
                IdentityKey = IdentityKey
            };
        }

        return null;
    }
}
