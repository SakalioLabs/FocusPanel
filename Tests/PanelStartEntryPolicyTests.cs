using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelStartEntryPolicyTests
{
    [Theory]
    [InlineData(false, false, ShellSearchScope.All)]
    [InlineData(false, true, ShellSearchScope.Windows)]
    [InlineData(false, true, ShellSearchScope.System)]
    public void PlainClick_OpensPanelLauncher(
        bool shiftPressed,
        bool isSearchOpen,
        ShellSearchScope scope)
    {
        Assert.Equal(
            PanelStartEntryAction
                .OpenApplicationLauncher,
            PanelStartEntryPolicy.Decide(
                shiftPressed,
                isSearchOpen,
                scope));
    }

    [Fact]
    public void RepeatedClick_ClosesOnlyOpenPanelLauncher()
    {
        Assert.Equal(
            PanelStartEntryAction
                .CloseApplicationLauncher,
            PanelStartEntryPolicy.Decide(
                shiftPressed: false,
                isSearchOpen: true,
                ShellSearchScope.Applications));
    }

    [Fact]
    public void ShiftClick_UsesWindowsCompatibilityEntry()
    {
        Assert.Equal(
            PanelStartEntryAction
                .OpenWindowsStartMenu,
            PanelStartEntryPolicy.Decide(
                shiftPressed: true,
                isSearchOpen: true,
                ShellSearchScope.Applications));
    }
}
