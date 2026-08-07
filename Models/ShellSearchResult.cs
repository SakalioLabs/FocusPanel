using System;
using System.Windows.Media;
using FocusPanel.Services;

namespace FocusPanel.Models;

public enum ShellSearchResultKind
{
    Application,
    Window,
    SystemCommand,
    Calculation,
    AudioCommand,
    BrightnessCommand,
    FocusCommand,
    TaskCapture,
    Task
}

public sealed record ShellSearchResult
{
    public ShellSearchResultKind Kind
    {
        get;
        init;
    }

    public string StableKey
    {
        get;
        init;
    } = string.Empty;

    public string DisplayName
    {
        get;
        init;
    } = string.Empty;

    public string SecondaryText
    {
        get;
        init;
    } = string.Empty;

    public string AccessibleName
    {
        get;
        init;
    } = string.Empty;

    public ImageSource? Icon
    {
        get;
        init;
    }

    public AppLaunchItem? Application
    {
        get;
        init;
    }

    public WindowReference? Window
    {
        get;
        init;
    }

    public string WindowApplicationName
    {
        get;
        init;
    } = string.Empty;

    public SystemManagementTool? ManagementTool
    {
        get;
        init;
    }

    internal WindowsShellAction? ShellAction
    {
        get;
        init;
    }

    public string CalculationResult
    {
        get;
        init;
    } = string.Empty;

    internal AudioSearchCommand? AudioCommand
    {
        get;
        init;
    }

    internal BrightnessSearchCommand?
        BrightnessCommand
    {
        get;
        init;
    }

    internal PomodoroSearchCommand? FocusCommand
    {
        get;
        init;
    }

    internal TaskCaptureCommand? TaskCaptureCommand
    {
        get;
        init;
    }

    internal TaskSearchItem? TaskItem
    {
        get;
        init;
    }

    public string Glyph
    {
        get;
        init;
    } = string.Empty;

    public bool IsWindow =>
        Kind
        == ShellSearchResultKind.Window;

    public bool IsSystemCommand =>
        Kind
        == ShellSearchResultKind.SystemCommand;

    public bool IsCalculation =>
        Kind
        == ShellSearchResultKind.Calculation;

    public bool IsAudioCommand =>
        Kind
        == ShellSearchResultKind.AudioCommand;

    public bool IsBrightnessCommand =>
        Kind
        == ShellSearchResultKind.BrightnessCommand;

    public bool IsFocusCommand =>
        Kind
        == ShellSearchResultKind.FocusCommand;

    public bool IsTaskCapture =>
        Kind
        == ShellSearchResultKind.TaskCapture;

    public bool IsTask =>
        Kind
        == ShellSearchResultKind.Task;

    public bool UsesGlyph =>
        IsSystemCommand
        || IsCalculation
        || IsAudioCommand
        || IsBrightnessCommand
        || IsFocusCommand
        || IsTaskCapture
        || IsTask;

    public bool CanCompleteTask =>
        TaskItem != null;

    public bool CanTogglePin =>
        Application != null;

    internal static ShellSearchResult
        FromApplication(
            AppLaunchItem application) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .Application,
            StableKey =
                "app:"
                + (string.IsNullOrWhiteSpace(
                        application.IdentityKey)
                    ? $"{(int)application.LaunchKind}:"
                      + application.LaunchTarget
                    : application.IdentityKey),
            DisplayName =
                application.DisplayName,
            SecondaryText =
                application.IsPinned
                    ? "应用 · 已固定"
                    : "应用",
            AccessibleName =
                $"启动应用 {application.DisplayName}",
            Icon =
                application.Icon,
            Application =
                application
        };

    internal static ShellSearchResult
        FromWindow(
            WindowTaskItem application,
            WindowReference window)
    {
        string title =
            string.IsNullOrWhiteSpace(
                window.Title)
                ? application.DisplayName
                  + " 窗口"
                : window.Title.Trim();
        string state =
            window.IsActive
                ? "当前窗口"
                : "已打开窗口";
        string topmost =
            window.IsTopmost
                ? " · 已置顶"
                : string.Empty;
        return new ShellSearchResult
        {
            Kind =
                ShellSearchResultKind
                    .Window,
            StableKey =
                $"window:{window.Handle.ToInt64():X}",
            DisplayName = title,
            SecondaryText =
                $"{application.DisplayName} · {state}"
                + topmost,
            AccessibleName =
                $"切换到{state} {title}，"
                + application.DisplayName
                + (window.IsTopmost
                    ? "，已置顶"
                    : string.Empty),
            Icon =
                application.Icon,
            Window =
                window,
            WindowApplicationName =
                application.DisplayName
        };
    }

    internal static ShellSearchResult
        FromSystemCommand(
            SystemManagementSearchEntry entry) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .SystemCommand,
            StableKey =
                "system:"
                + entry.Tool,
            DisplayName =
                entry.DisplayName,
            SecondaryText =
                "Windows 系统命令",
            AccessibleName =
                $"打开系统命令 {entry.DisplayName}",
            Glyph =
                entry.Glyph,
            ManagementTool =
                entry.Tool
        };

    internal static ShellSearchResult
        FromShellCommand(
            WindowsShellSearchEntry entry) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .SystemCommand,
            StableKey =
                "shell:"
                + entry.Action,
            DisplayName =
                entry.DisplayName,
            SecondaryText =
                "Windows 快捷命令",
            AccessibleName =
                $"执行快捷命令 {entry.DisplayName}",
            Glyph =
                entry.Glyph,
            ShellAction =
                entry.Action
        };

    internal static ShellSearchResult
        FromCalculation(
            string expression,
            string result) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .Calculation,
            StableKey =
                "calculation:"
                + expression.Trim(),
            DisplayName =
                result,
            SecondaryText =
                "计算结果 · 点击或按 Enter 复制",
            AccessibleName =
                $"计算结果 {result}，"
                + "点击或按回车复制",
            Glyph =
                "\uE1D0",
            CalculationResult =
                result
        };

    internal static ShellSearchResult
        FromAudioCommand(
            AudioSearchCommand command) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .AudioCommand,
            StableKey =
                command.StableKey,
            DisplayName =
                command.DisplayName,
            SecondaryText =
                "音频快捷命令 · 点击或按 Enter 执行",
            AccessibleName =
                $"执行音频快捷命令 {command.DisplayName}",
            Glyph =
                command.Glyph,
            AudioCommand =
                command
        };

    internal static ShellSearchResult
        FromBrightnessCommand(
            BrightnessSearchCommand command) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .BrightnessCommand,
            StableKey = command.StableKey,
            DisplayName = command.DisplayName,
            SecondaryText =
                "显示快捷命令 · 点击或按 Enter 执行",
            AccessibleName =
                $"执行显示快捷命令 {command.DisplayName}",
            Glyph = "\uE706",
            BrightnessCommand = command
        };

    internal static ShellSearchResult
        FromFocusCommand(
            PomodoroSearchCommand command) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .FocusCommand,
            StableKey =
                command.StableKey,
            DisplayName =
                command.DisplayName,
            SecondaryText =
                "专注快捷命令 · 点击或按 Enter 开始",
            AccessibleName =
                $"执行专注快捷命令 {command.DisplayName}",
            Glyph =
                "\uE823",
            FocusCommand =
                command
        };

    internal static ShellSearchResult
        FromTaskCapture(
            TaskCaptureCommand command) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .TaskCapture,
            StableKey =
                command.StableKey,
            DisplayName =
                command.DisplayName,
            SecondaryText =
                "任务快捷收集 · 点击或按 Enter 保存到 Inbox",
            AccessibleName =
                $"保存到任务收件箱 {command.Title}",
            Glyph =
                "\uE73E",
            TaskCaptureCommand =
                command
        };

    internal static ShellSearchResult
        FromTask(
            TaskSearchItem item) =>
        new()
        {
            Kind =
                ShellSearchResultKind
                    .Task,
            StableKey =
                item.StableKey,
            DisplayName =
                item.Title,
            SecondaryText =
                string.IsNullOrWhiteSpace(
                    item.ParentTitle)
                    ? "待办任务"
                    : $"{item.ParentTitle} · "
                      + (string.IsNullOrWhiteSpace(
                              item.Status)
                          ? "待办任务"
                          : item.Status),
            AccessibleName =
                $"打开待办任务 {item.Title}",
            Glyph =
                "\uE8FD",
            TaskItem =
                item
        };
}
