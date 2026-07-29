using System;
using System.Windows.Media;

namespace FocusPanel.Models;

public enum ShellSearchResultKind
{
    Application,
    Window
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

    public bool IsWindow =>
        Kind
        == ShellSearchResultKind.Window;

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
}
