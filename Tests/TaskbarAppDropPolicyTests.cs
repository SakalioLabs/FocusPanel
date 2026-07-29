using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskbarAppDropPolicyTests
{
    [Theory]
    [InlineData(0, 44, TaskbarDropPlacement.Before)]
    [InlineData(21.9, 44, TaskbarDropPlacement.Before)]
    [InlineData(22, 44, TaskbarDropPlacement.After)]
    [InlineData(43, 44, TaskbarDropPlacement.After)]
    [InlineData(
        double.NaN,
        44,
        TaskbarDropPlacement.After)]
    [InlineData(
        10,
        0,
        TaskbarDropPlacement.After)]
    public void PointerHalf_SelectsDropPlacement(
        double pointerY,
        double itemHeight,
        TaskbarDropPlacement expected)
    {
        Assert.Equal(
            expected,
            TaskbarAppDropPolicy.GetPlacement(
                pointerY,
                itemHeight));
    }

    [Theory]
    [InlineData(
        true,
        false,
        4,
        TaskbarDropPlacement.Before)]
    [InlineData(
        true,
        false,
        40,
        TaskbarDropPlacement.After)]
    [InlineData(
        false,
        true,
        40,
        TaskbarDropPlacement.Before)]
    public void CuePlacement_UsesOnlyRealPinnedBoundary(
        bool targetIsPinned,
        bool isFirstUnpinned,
        double pointerY,
        TaskbarDropPlacement expected)
    {
        Assert.Equal(
            expected,
            TaskbarAppDropPolicy
                .GetCuePlacement(
                    targetIsPinned,
                    isFirstUnpinned,
                    pointerY,
                    44));
    }

    [Fact]
    public void LaterUnpinnedItem_HasNoFalseCue()
    {
        Assert.Null(
            TaskbarAppDropPolicy
                .GetCuePlacement(
                    false,
                    false,
                    4,
                    44));
    }

    [Theory]
    [InlineData(
        0,
        2,
        TaskbarDropPlacement.Before,
        1)]
    [InlineData(
        0,
        2,
        TaskbarDropPlacement.After,
        2)]
    [InlineData(
        2,
        0,
        TaskbarDropPlacement.Before,
        0)]
    [InlineData(
        2,
        0,
        TaskbarDropPlacement.After,
        1)]
    public void PinnedSource_InsertsBeforeOrAfterTarget(
        int sourceIndex,
        int targetIndex,
        TaskbarDropPlacement placement,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarAppDropPolicy
                .GetInsertionIndex(
                    true,
                    sourceIndex,
                    true,
                    targetIndex,
                    3,
                    placement));
    }

    [Theory]
    [InlineData(
        1,
        TaskbarDropPlacement.Before,
        1)]
    [InlineData(
        1,
        TaskbarDropPlacement.After,
        2)]
    public void UnpinnedSource_UsesTargetSide(
        int targetIndex,
        TaskbarDropPlacement placement,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarAppDropPolicy
                .GetInsertionIndex(
                    false,
                    -1,
                    true,
                    targetIndex,
                    3,
                    placement));
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    public void UnpinnedTarget_AppendsToPinnedRegion(
        bool sourceIsPinned,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarAppDropPolicy
                .GetInsertionIndex(
                    sourceIsPinned,
                    sourceIsPinned
                        ? 0
                        : -1,
                    false,
                    3,
                    3,
                    TaskbarDropPlacement
                        .Before));
    }

    [Fact]
    public void InvalidIndices_AreClamped()
    {
        Assert.Equal(
            0,
            TaskbarAppDropPolicy
                .GetInsertionIndex(
                    true,
                    99,
                    true,
                    -20,
                    -3,
                    TaskbarDropPlacement
                        .Before));
    }

    [Theory]
    [InlineData(0, 3, 1, 1)]
    [InlineData(1, 3, -1, 0)]
    [InlineData(1, 3, 1, 2)]
    [InlineData(2, 3, -1, 1)]
    public void StepMove_ReturnsAdjacentPinnedIndex(
        int currentIndex,
        int pinnedCount,
        int offset,
        int expected)
    {
        Assert.Equal(
            expected,
            TaskbarPinnedStepPolicy
                .GetTargetIndex(
                    currentIndex,
                    pinnedCount,
                    offset));
    }

    [Theory]
    [InlineData(0, 3, -1)]
    [InlineData(2, 3, 1)]
    [InlineData(-1, 3, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 3, 0)]
    public void StepMove_RejectsUnavailableDirection(
        int currentIndex,
        int pinnedCount,
        int offset)
    {
        Assert.Null(
            TaskbarPinnedStepPolicy
                .GetTargetIndex(
                    currentIndex,
                    pinnedCount,
                    offset));
    }
}
