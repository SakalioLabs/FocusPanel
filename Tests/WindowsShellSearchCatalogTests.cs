using System;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowsShellSearchCatalogTests
{
    [Fact]
    public void Catalog_ContainsOnlyTwelveNonDestructiveShortcutActions()
    {
        WindowsShellAction[] expected =
        {
            WindowsShellAction.QuickSettings,
            WindowsShellAction.Notifications,
            WindowsShellAction.InputSwitcher,
            WindowsShellAction.Widgets,
            WindowsShellAction.SoundOutput,
            WindowsShellAction.ScreenSnipping,
            WindowsShellAction.ProjectDisplay,
            WindowsShellAction.CastDevices,
            WindowsShellAction.ShowDesktop,
            WindowsShellAction.MediaPreviousTrack,
            WindowsShellAction.MediaPlayPause,
            WindowsShellAction.MediaNextTrack
        };

        Assert.Equal(
            expected,
            WindowsShellSearchCatalog
                .All
                .Select(entry => entry.Action));
        Assert.Equal(
            expected.Length,
            WindowsShellSearchCatalog
                .All
                .Select(entry => entry.Action)
                .Distinct()
                .Count());
        Assert.DoesNotContain(
            WindowsShellSearchCatalog.All,
            entry =>
                entry.Action
                is WindowsShellAction.VirtualDesktopCreate
                or WindowsShellAction.VirtualDesktopClose);
    }

    [Fact]
    public void Catalog_ProvidesVisibleNameGlyphAndAliases()
    {
        Assert.All(
            WindowsShellSearchCatalog.All,
            entry =>
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.DisplayName));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.Glyph));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        entry.Aliases));
            });
    }
}
