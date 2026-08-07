using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class PanelStartEntryPolicyTests
{
    [Theory]
    [InlineData(false, ShellSearchScope.All)]
    [InlineData(true, ShellSearchScope.Windows)]
    [InlineData(true, ShellSearchScope.System)]
    public void PlainClick_OpensPanelLauncher(
        bool isSearchOpen,
        ShellSearchScope scope)
    {
        Assert.Equal(
            PanelStartEntryAction
                .OpenApplicationLauncher,
            PanelStartEntryPolicy.Decide(
                shiftPressed: false,
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
    public void ShiftClick_OpensPanelUnifiedSearch()
    {
        Assert.Equal(
            PanelStartEntryAction
                .OpenUnifiedSearch,
            PanelStartEntryPolicy.Decide(
                shiftPressed: true,
                isSearchOpen: true,
                ShellSearchScope.Applications));
    }

    [Fact]
    public void RepeatedShiftClick_ClosesOnlyUnifiedSearch()
    {
        Assert.Equal(
            PanelStartEntryAction
                .CloseUnifiedSearch,
            PanelStartEntryPolicy.Decide(
                shiftPressed: true,
                isSearchOpen: true,
                ShellSearchScope.All));
    }
}
