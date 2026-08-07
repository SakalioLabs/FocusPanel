using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum PanelStartEntryAction
{
    OpenApplicationLauncher,
    CloseApplicationLauncher,
    OpenWindowsStartMenu
}

internal static class PanelStartEntryPolicy
{
    internal static PanelStartEntryAction
        Decide(
        bool shiftPressed,
        bool isSearchOpen,
        ShellSearchScope searchScope)
    {
        if (shiftPressed)
        {
            return PanelStartEntryAction
                .OpenWindowsStartMenu;
        }

        return isSearchOpen
               && searchScope
               == ShellSearchScope
                   .Applications
            ? PanelStartEntryAction
                .CloseApplicationLauncher
            : PanelStartEntryAction
                .OpenApplicationLauncher;
    }
}
