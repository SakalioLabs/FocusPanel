using FocusPanel.Services;
using System.Linq;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowsShellShortcutTests
{
    [Fact]
    public void WindowsChords_UseExpectedVirtualKeys()
    {
        var expected = new (WindowsShellAction Action, ushort Key)[]
        {
            (WindowsShellAction.Notifications, 0x4E),
            (WindowsShellAction.Widgets, 0x57),
            (WindowsShellAction.ProjectDisplay, 0x50),
            (WindowsShellAction.CastDevices, 0x4B),
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
    public void SoundOutput_UsesWindowsControlV()
    {
        WindowsShellShortcut shortcut =
            WindowsShellShortcutMap.Get(
                WindowsShellAction.SoundOutput);

        Assert.True(shortcut.UsesWindowsKey);
        Assert.True(shortcut.UsesControl);
        Assert.False(shortcut.UsesAlt);
        Assert.False(shortcut.UsesShift);
        Assert.Equal((ushort)0x56, shortcut.Key);
    }

    [Fact]
    public void ScreenSnipping_UsesWindowsShiftS()
    {
        WindowsShellShortcut shortcut =
            WindowsShellShortcutMap.Get(
                WindowsShellAction.ScreenSnipping);

        Assert.True(shortcut.UsesWindowsKey);
        Assert.False(shortcut.UsesControl);
        Assert.False(shortcut.UsesAlt);
        Assert.True(shortcut.UsesShift);
        Assert.Equal((ushort)0x53, shortcut.Key);

        WindowsShortcutKeyTransition[] transitions =
            WindowsShortcutSequence.Build(shortcut).ToArray();
        Assert.Equal(
            new ushort[]
            {
                0x5B,
                0x10,
                0x53,
                0x53,
                0x10,
                0x5B
            },
            transitions.Select(item => item.Key));
        Assert.Equal(
            new[]
            {
                true,
                true,
                true,
                false,
                false,
                false
            },
            transitions.Select(item => item.IsDown));
    }

    [Fact]
    public void VirtualDesktopChords_UseWindowsControlAndExpectedKey()
    {
        var expected =
            new[]
            {
                (
                    WindowsShellAction
                        .VirtualDesktopPrevious,
                    Key: (ushort)0x25),
                (
                    WindowsShellAction
                        .VirtualDesktopNext,
                    Key: (ushort)0x27),
                (
                    WindowsShellAction
                        .VirtualDesktopCreate,
                    Key: (ushort)0x44),
                (
                    WindowsShellAction
                        .VirtualDesktopClose,
                    Key: (ushort)0x73)
            };

        foreach (var item in expected)
        {
            WindowsShellShortcut shortcut =
                WindowsShellShortcutMap.Get(
                    item.Item1);

            Assert.True(
                shortcut.UsesWindowsKey);
            Assert.True(
                shortcut.UsesControl);
            Assert.False(shortcut.UsesAlt);
            Assert.False(shortcut.UsesShift);
            Assert.Equal(
                item.Key,
                shortcut.Key);
        }
    }

    [Fact]
    public void KeySequence_PressesModifiersThenReleasesInReverse()
    {
        WindowsShellShortcut shortcut =
            WindowsShellShortcutMap.Get(
                WindowsShellAction
                    .VirtualDesktopNext);

        WindowsShortcutKeyTransition[]
            transitions =
                WindowsShortcutSequence
                    .Build(shortcut)
                    .ToArray();

        Assert.Equal(
            new ushort[]
            {
                0x5B,
                0x11,
                0x27,
                0x27,
                0x11,
                0x5B
            },
            transitions.Select(
                transition =>
                    transition.Key));
        Assert.Equal(
            new[]
            {
                true,
                true,
                true,
                false,
                false,
                false
            },
            transitions.Select(
                transition =>
                    transition.IsDown));
    }

    [Theory]
    [InlineData(
        MediaTransportAction.PreviousTrack,
        0xB1)]
    [InlineData(
        MediaTransportAction.PlayPause,
        0xB3)]
    [InlineData(
        MediaTransportAction.NextTrack,
        0xB0)]
    public void MediaKeys_UseSdkVirtualKeysWithoutModifiers(
        MediaTransportAction mediaAction,
        int expectedKey)
    {
        WindowsShellAction shellAction =
            mediaAction switch
            {
                MediaTransportAction
                    .PreviousTrack =>
                    WindowsShellAction
                        .MediaPreviousTrack,
                MediaTransportAction
                    .PlayPause =>
                    WindowsShellAction
                        .MediaPlayPause,
                _ =>
                    WindowsShellAction
                        .MediaNextTrack
            };
        Assert.Equal(
            shellAction,
            MediaTransportShortcutMap.Get(
                mediaAction));
        WindowsShellShortcut shortcut =
            WindowsShellShortcutMap.Get(
                shellAction);

        Assert.False(shortcut.UsesWindowsKey);
        Assert.False(shortcut.UsesControl);
        Assert.False(shortcut.UsesAlt);
        Assert.False(shortcut.UsesShift);
        Assert.Equal(
            (ushort)expectedKey,
            shortcut.Key);

        WindowsShortcutKeyTransition[]
            transitions =
                WindowsShortcutSequence
                    .Build(shortcut)
                    .ToArray();
        Assert.Equal(
            new[]
            {
                (ushort)expectedKey,
                (ushort)expectedKey
            },
            transitions.Select(item =>
                item.Key));
        Assert.Equal(
            new[]
            {
                true,
                false
            },
            transitions.Select(item =>
                item.IsDown));
    }

    [Fact]
    public void InvalidMediaActionDoesNotInventAKey()
    {
        Assert.Throws<
            System.ArgumentOutOfRangeException>(
            () =>
                MediaTransportShortcutMap.Get(
                    (MediaTransportAction)99));
    }
}
