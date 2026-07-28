using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AppSearchSelectionPolicyTests
{
    [Theory]
    [InlineData(0, -1, 1, -1)]
    [InlineData(3, -1, 1, 0)]
    [InlineData(3, -1, -1, 2)]
    [InlineData(3, 0, 1, 1)]
    [InlineData(3, 2, 1, 2)]
    [InlineData(3, 2, -1, 1)]
    [InlineData(3, 0, -1, 0)]
    public void Move_ChoosesPredictableClampedResult(
        int itemCount,
        int currentIndex,
        int direction,
        int expected)
    {
        Assert.Equal(
            expected,
            AppSearchSelectionPolicy.Move(
                itemCount,
                currentIndex,
                direction));
    }

    [Theory]
    [InlineData(0, -1, -1)]
    [InlineData(3, -1, 0)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 9, 2)]
    public void ResolveLaunchIndex_DefaultsToFirstAvailableResult(
        int itemCount,
        int selectedIndex,
        int expected)
    {
        Assert.Equal(
            expected,
            AppSearchSelectionPolicy.ResolveLaunchIndex(
                itemCount,
                selectedIndex));
    }
}
