using System;
using System.Windows.Media;
using FocusPanel.Services;

namespace FocusPanel.Models;

public enum ShellSearchResultKind
{
    Application,
    Window,
    SystemCommand
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
        return new ShellSearchResult
        {
            Kind =
                ShellSearchResultKind
                    .Window,
            StableKey =
                $"window:{window.Handle.ToInt64():X}",
            DisplayName = title,
            SecondaryText =
                $"{application.DisplayName} · {state}",
            AccessibleName =
                $"切换到{state} {title}，"
                + application.DisplayName,
            Icon =
                application.Icon,
            Window =
                window
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
}
