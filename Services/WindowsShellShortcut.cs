using System.Collections.Generic;
using System.Linq;

namespace FocusPanel.Services;

internal enum WindowsShellAction
{
    StartMenu,
    QuickSettings,
    Notifications,
    InputSwitcher,
    TaskView,
    VirtualDesktopPrevious,
    VirtualDesktopNext,
    VirtualDesktopCreate,
    VirtualDesktopClose,
    Search,
    Widgets,
    RunDialog,
    ShowDesktop,
    MediaPreviousTrack,
    MediaPlayPause,
    MediaNextTrack
}

internal readonly record struct WindowsShellShortcut(
    ushort Key,
    bool UsesWindowsKey,
    bool UsesControl = false,
    bool UsesAlt = false,
    bool UsesShift = false);

internal readonly record struct
    WindowsShortcutKeyTransition(
        ushort Key,
        bool IsDown);

internal static class WindowsShortcutSequence
{
    private const ushort VkLeftWindows =
        0x5B;
    private const ushort VkControl =
        0x11;
    private const ushort VkAlt =
        0x12;
    private const ushort VkShift =
        0x10;

    internal static IReadOnlyList<
        WindowsShortcutKeyTransition> Build(
            WindowsShellShortcut shortcut)
    {
        var keys =
            new List<ushort>(5);
        if (shortcut.UsesWindowsKey)
            keys.Add(VkLeftWindows);
        if (shortcut.UsesControl)
            keys.Add(VkControl);
        if (shortcut.UsesAlt)
            keys.Add(VkAlt);
        if (shortcut.UsesShift)
            keys.Add(VkShift);
        keys.Add(shortcut.Key);

        var transitions =
            new List<
                WindowsShortcutKeyTransition>(
                keys.Count * 2);
        transitions.AddRange(
            keys.Select(
                key =>
                    new WindowsShortcutKeyTransition(
                        key,
                        true)));
        for (int index = keys.Count - 1;
             index >= 0;
             index--)
        {
            transitions.Add(
                new WindowsShortcutKeyTransition(
                    keys[index],
                    false));
        }

        return transitions;
    }
}

internal static class WindowsShellShortcutMap
{
    internal static WindowsShellShortcut Get(WindowsShellAction action) => action switch
    {
        WindowsShellAction.StartMenu => new(0x5B, false),
        WindowsShellAction.QuickSettings => new(0x41, true),
        WindowsShellAction.Notifications => new(0x4E, true),
        WindowsShellAction.InputSwitcher => new(0x20, true),
        WindowsShellAction.TaskView => new(0x09, true),
        WindowsShellAction.VirtualDesktopPrevious =>
            new(0x25, true, UsesControl: true),
        WindowsShellAction.VirtualDesktopNext =>
            new(0x27, true, UsesControl: true),
        WindowsShellAction.VirtualDesktopCreate =>
            new(0x44, true, UsesControl: true),
        WindowsShellAction.VirtualDesktopClose =>
            new(0x73, true, UsesControl: true),
        WindowsShellAction.Search => new(0x53, true),
        WindowsShellAction.Widgets => new(0x57, true),
        WindowsShellAction.RunDialog => new(0x52, true),
        WindowsShellAction.ShowDesktop => new(0x44, true),
        WindowsShellAction.MediaPreviousTrack =>
            new(0xB1, false),
        WindowsShellAction.MediaPlayPause =>
            new(0xB3, false),
        WindowsShellAction.MediaNextTrack =>
            new(0xB0, false),
        _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, null)
    };
}

internal static class MediaTransportShortcutMap
{
    internal static WindowsShellAction Get(
        MediaTransportAction action) =>
        action switch
        {
            MediaTransportAction
                .PreviousTrack =>
                WindowsShellAction
                    .MediaPreviousTrack,
            MediaTransportAction
                .PlayPause =>
                WindowsShellAction
                    .MediaPlayPause,
            MediaTransportAction
                .NextTrack =>
                WindowsShellAction
                    .MediaNextTrack,
            _ =>
                throw new System
                    .ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        null)
        };
}
