using FocusPanel.Services;
using System.Windows.Input;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarKeyboardNavigationPolicyTests
{
    [Theory]
    [InlineData(Key.Up, "Previous")]
    [InlineData(Key.Down, "Next")]
    [InlineData(Key.Home, "First")]
    [InlineData(Key.End, "Last")]
    [InlineData(Key.PageUp, "PreviousPage")]
    [InlineData(Key.PageDown, "NextPage")]
    public void MapsVerticalTaskbarNavigationKeys(
        Key key,
        string expectedName)
    {
        Assert.Equal(
            ParseAction(expectedName),
            TaskbarKeyboardNavigationPolicy
                .GetAction(
                    key,
                    ModifierKeys.None));
    }

    [Theory]
    [InlineData(Key.Left, ModifierKeys.None)]
    [InlineData(Key.Enter, ModifierKeys.None)]
    [InlineData(Key.Up, ModifierKeys.Control)]
    [InlineData(Key.Down, ModifierKeys.Alt)]
    public void LeavesUnrelatedOrModifiedKeysToExistingGestures(
        Key key,
        ModifierKeys modifiers)
    {
        Assert.Equal(
            TaskbarKeyboardNavigationAction.None,
            TaskbarKeyboardNavigationPolicy
                .GetAction(key, modifiers));
    }

    [Theory]
    [InlineData(4, 10, "Previous", 3, 3)]
    [InlineData(4, 10, "Next", 3, 5)]
    [InlineData(4, 10, "First", 3, 0)]
    [InlineData(4, 10, "Last", 3, 9)]
    [InlineData(7, 10, "PreviousPage", 3, 4)]
    [InlineData(4, 10, "NextPage", 3, 7)]
    public void ResolvesEverySupportedNavigationAction(
        int currentIndex,
        int itemCount,
        string actionName,
        int pageSize,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarKeyboardNavigationPolicy
                .GetTargetIndex(
                    currentIndex,
                    itemCount,
                    ParseAction(actionName),
                    pageSize));
    }

    [Theory]
    [InlineData(0, 4, "Previous", 0)]
    [InlineData(3, 4, "Next", 3)]
    [InlineData(1, 4, "PreviousPage", 0)]
    [InlineData(2, 4, "NextPage", 3)]
    public void ClampsNavigationAtCollectionEdges(
        int currentIndex,
        int itemCount,
        string actionName,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarKeyboardNavigationPolicy
                .GetTargetIndex(
                    currentIndex,
                    itemCount,
                    ParseAction(actionName),
                    pageSize: 8));
    }

    [Theory]
    [InlineData(-1, 5, "Next")]
    [InlineData(5, 5, "Previous")]
    [InlineData(0, 0, "First")]
    [InlineData(0, 5, "None")]
    public void RejectsInvalidOrNoActionRequests(
        int currentIndex,
        int itemCount,
        string actionName)
    {
        Assert.Equal(
            -1,
            TaskbarKeyboardNavigationPolicy
                .GetTargetIndex(
                    currentIndex,
                    itemCount,
                    ParseAction(actionName),
                    pageSize: 3));
    }

    [Theory]
    [InlineData(322, 46, 46, 46, 5)]
    [InlineData(100, 46, 46, 46, 1)]
    [InlineData(230, 46, 0, 0, 5)]
    [InlineData(double.NaN, 46, 0, 0, 1)]
    [InlineData(230, 0, 0, 0, 1)]
    public void PageSizeUsesOnlyUncoveredVisibleItems(
        double viewportHeight,
        double itemExtent,
        double leadingInset,
        double trailingInset,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarKeyboardNavigationPolicy
                .GetPageSize(
                    viewportHeight,
                    itemExtent,
                    leadingInset,
                    trailingInset));
    }

    private static TaskbarKeyboardNavigationAction
        ParseAction(string name) =>
        System.Enum.Parse<
            TaskbarKeyboardNavigationAction>(name);
}
