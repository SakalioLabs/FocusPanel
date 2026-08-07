using FocusPanel.Models;

namespace FocusPanel.Services;

internal enum PanelStartEntryAction
{
    OpenApplicationLauncher,
    CloseApplicationLauncher,
    OpenUnifiedSearch,
    CloseUnifiedSearch
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
            return isSearchOpen
                   && searchScope
                   == ShellSearchScope.All
                ? PanelStartEntryAction
                    .CloseUnifiedSearch
                : PanelStartEntryAction
                    .OpenUnifiedSearch;
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
