using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    WindowOverviewHotkeySelectionPolicyTests
{
    [Theory]
    [InlineData(0, -1, false, -1)]
    [InlineData(1, 0, false, 0)]
    [InlineData(4, 0, false, 1)]
    [InlineData(4, 1, true, 2)]
    [InlineData(4, 3, true, 0)]
    [InlineData(4, -1, true, 1)]
    [InlineData(1, -1, true, 0)]
    public void Select_PrefersPreviousThenCycles(
        int itemCount,
        int currentIndex,
        bool repeated,
        int expected)
    {
        Assert.Equal(
            expected,
            WindowOverviewHotkeySelectionPolicy
                .Select(
                    itemCount,
                    currentIndex,
                    repeated));
    }
}
