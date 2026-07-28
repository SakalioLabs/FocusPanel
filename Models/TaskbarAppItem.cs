using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace FocusPanel.Models;

public sealed class TaskbarAppItem
{
    public string IdentityKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ImageSource? Icon { get; init; }
    public AppLaunchItem? LaunchItem { get; init; }
    public IReadOnlyList<AppLaunchItem> PinnedLaunches { get; init; } = Array.Empty<AppLaunchItem>();
    public WindowTaskItem? RunningTask { get; init; }
    public bool IsPinned => PinnedLaunches.Count > 0;
    public bool IsRunning => RunningTask != null;
    public bool IsActive => RunningTask?.IsActive == true;
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
    public string InteractionHint =>
        WindowCount > 1
            ? "左键打开窗口列表，右键管理应用"
            : IsRunning
                ? "左键切换或最小化，右键管理应用"
                : "左键启动，右键管理应用";

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
