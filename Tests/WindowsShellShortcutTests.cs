using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowsShellShortcutTests
{
    [Fact]
    public void WindowsChords_UseExpectedVirtualKeys()
    {
        var expected = new (WindowsShellAction Action, ushort Key)[]
        {
            (WindowsShellAction.QuickSettings, 0x41),
            (WindowsShellAction.Notifications, 0x4E),
            (WindowsShellAction.InputSwitcher, 0x20),
            (WindowsShellAction.TaskView, 0x09),
            (WindowsShellAction.Search, 0x53),
            (WindowsShellAction.Widgets, 0x57),
            (WindowsShellAction.RunDialog, 0x52),
            (WindowsShellAction.ShowDesktop, 0x44)
        };

        foreach ((WindowsShellAction action, ushort key) in expected)
        {
            WindowsShellShortcut shortcut = WindowsShellShortcutMap.Get(action);
            Assert.True(shortcut.UsesWindowsKey);
            Assert.Equal(key, shortcut.Key);
        }
    }

    [Fact]
    public void StartMenu_UsesWindowsKeyAlone()
    {
        WindowsShellShortcut shortcut = WindowsShellShortcutMap.Get(
            WindowsShellAction.StartMenu);

        Assert.False(shortcut.UsesWindowsKey);
        Assert.Equal(0x5B, shortcut.Key);
    }
}
