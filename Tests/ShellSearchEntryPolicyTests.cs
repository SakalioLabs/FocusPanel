using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellSearchEntryPolicyTests
{
    [Fact]
    public void ClosedCompactEntryOpensCleanWindowOverview()
    {
        ShellSearchEntryState state =
            ShellSearchEntryPolicy
                .PrepareWindowOverview(
                    isSearchOpen: false,
                    ShellSearchScope.System,
                    "任务管理器");

        Assert.Equal(
            ShellSearchScope.Windows,
            state.Scope);
        Assert.Equal(string.Empty, state.Query);
    }

    [Fact]
    public void RepeatedCompactEntryClickPreservesOpenSurfaceState()
    {
        ShellSearchEntryState state =
            ShellSearchEntryPolicy
                .PrepareWindowOverview(
                    isSearchOpen: true,
                    ShellSearchScope.Applications,
                    "画图");

        Assert.Equal(
            ShellSearchScope.Applications,
            state.Scope);
        Assert.Equal("画图", state.Query);
    }

    [Fact]
    public void SummonHotkeyAlwaysReturnsToUnifiedSearch()
    {
        ShellSearchEntryState state =
            ShellSearchEntryPolicy
                .PrepareUnifiedSearch(
                    "季度方案");

        Assert.Equal(
            ShellSearchScope.All,
            state.Scope);
        Assert.Equal("季度方案", state.Query);
    }

    [Fact]
    public void ApplicationLauncherAlwaysStartsFromAllApplications()
    {
        ShellSearchEntryState state =
            ShellSearchEntryPolicy
                .PrepareApplicationLauncher();

        Assert.Equal(
            ShellSearchScope.Applications,
            state.Scope);
        Assert.Equal(string.Empty, state.Query);
        Assert.Equal(
            int.MaxValue,
            ShellSearchEntryPolicy
                .GetApplicationLimit(
                    state.Scope,
                    state.Query));
        Assert.Equal(
            int.MaxValue,
            ShellSearchEntryPolicy
                .GetResultLimit(
                    state.Scope,
                    state.Query));
    }

    [Fact]
    public void TypedApplicationSearchKeepsBoundedResultSet()
    {
        Assert.Equal(
            ShellSearchPolicy.DefaultLimit,
            ShellSearchEntryPolicy
                .GetApplicationLimit(
                    ShellSearchScope.Applications,
                    "画图"));
        Assert.Equal(
            ShellSearchPolicy.DefaultLimit,
            ShellSearchEntryPolicy
                .GetResultLimit(
                    ShellSearchScope.Applications,
                    "画图"));
    }
}
