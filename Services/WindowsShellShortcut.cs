namespace FocusPanel.Services;

internal enum WindowsShellAction
{
    StartMenu,
    QuickSettings,
    Notifications,
    InputSwitcher,
    TaskView,
    Search,
    Widgets,
    RunDialog,
    ShowDesktop
}

internal readonly record struct WindowsShellShortcut(ushort Key, bool UsesWindowsKey);

internal static class WindowsShellShortcutMap
{
    internal static WindowsShellShortcut Get(WindowsShellAction action) => action switch
    {
        WindowsShellAction.StartMenu => new(0x5B, false),
        WindowsShellAction.QuickSettings => new(0x41, true),
        WindowsShellAction.Notifications => new(0x4E, true),
        WindowsShellAction.InputSwitcher => new(0x20, true),
        WindowsShellAction.TaskView => new(0x09, true),
        WindowsShellAction.Search => new(0x53, true),
        WindowsShellAction.Widgets => new(0x57, true),
        WindowsShellAction.RunDialog => new(0x52, true),
        WindowsShellAction.ShowDesktop => new(0x44, true),
        _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, null)
    };
}
