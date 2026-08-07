using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct ShellSearchEntryState(
    ShellSearchScope Scope,
    string Query);

internal static class ShellSearchEntryPolicy
{
    internal static ShellSearchEntryState
        PrepareWindowOverview(
            bool isSearchOpen,
            ShellSearchScope currentScope,
            string? currentQuery) =>
        isSearchOpen
            ? new ShellSearchEntryState(
                currentScope,
                currentQuery ?? string.Empty)
            : new ShellSearchEntryState(
                ShellSearchScope.Windows,
                string.Empty);

    internal static ShellSearchEntryState
        PrepareUnifiedSearch(
            string? currentQuery) =>
        new(
            ShellSearchScope.All,
            currentQuery ?? string.Empty);
}
