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

    internal static ShellSearchEntryState
        PrepareWindowOverviewFromHotkey() =>
        new(
            ShellSearchScope.Windows,
            string.Empty);

    internal static ShellSearchEntryState
        PrepareApplicationLauncher() =>
        new(
            ShellSearchScope.Applications,
            string.Empty);

    internal static int GetApplicationLimit(
        ShellSearchScope scope,
        string? query) =>
        scope == ShellSearchScope.Applications
        && string.IsNullOrWhiteSpace(query)
            ? int.MaxValue
            : ShellSearchPolicy.DefaultLimit;

    internal static int GetResultLimit(
        ShellSearchScope scope,
        string? query) =>
        scope == ShellSearchScope.Windows
        || (scope
                == ShellSearchScope.Applications
            && string.IsNullOrWhiteSpace(query))
            ? int.MaxValue
            : ShellSearchPolicy.DefaultLimit;
}
