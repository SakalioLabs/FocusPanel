using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class
    CompactTaskbarDragScrollPolicyTests
{
    [Theory]
    [InlineData(110, false, false)]
    [InlineData(56, false, false)]
    [InlineData(20, true, false)]
    [InlineData(200, false, true)]
    public void EdgeZones_SelectAvailableDirection(
        double pointerY,
        bool scrollsUp,
        bool scrollsDown)
    {
        CompactTaskbarDragScrollDecision
            decision =
                CompactTaskbarDragScrollPolicy
                    .GetDecision(
                        100,
                        300,
                        pointerY,
                        220,
                        true,
                        true,
                        true);

        Assert.Equal(
            scrollsUp,
            decision.ShouldScroll
            && decision.TargetOffset < 100);
        Assert.Equal(
            scrollsDown,
            decision.ShouldScroll
            && decision.TargetOffset > 100);
    }

    [Fact]
    public void DeeperEdgePosition_ScrollsFaster()
    {
        double nearBoundary =
            CompactTaskbarDragScrollPolicy
                .GetDecision(
                    100,
                    300,
                    50,
                    220,
                    true,
                    true,
                    true)
                .TargetOffset;
        double atEdge =
            CompactTaskbarDragScrollPolicy
                .GetDecision(
                    100,
                    300,
                    0,
                    220,
                    true,
                    true,
                    true)
                .TargetOffset;

        Assert.True(
            atEdge < nearBoundary);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 100, false)]
    [InlineData(300, 220, false)]
    public void UnavailableDirection_DoesNotScroll(
        double currentOffset,
        double pointerY,
        bool canScrollUp)
    {
        CompactTaskbarDragScrollDecision
            decision =
                CompactTaskbarDragScrollPolicy
                    .GetDecision(
                        currentOffset,
                        300,
                        pointerY,
                        220,
                        canScrollUp,
                        false,
                        true);

        Assert.False(
            decision.ShouldScroll);
        Assert.Equal(
            currentOffset,
            decision.TargetOffset);
    }

    [Fact]
    public void TargetOffset_IsClampedToScrollRange()
    {
        CompactTaskbarDragScrollDecision top =
            CompactTaskbarDragScrollPolicy
                .GetDecision(
                    2,
                    300,
                    -20,
                    220,
                    true,
                    true,
                    true);
        CompactTaskbarDragScrollDecision bottom =
            CompactTaskbarDragScrollPolicy
                .GetDecision(
                    298,
                    300,
                    260,
                    220,
                    true,
                    true,
                    true);

        Assert.Equal(0, top.TargetOffset);
        Assert.Equal(
            300,
            bottom.TargetOffset);
    }

    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(100, 144, false)]
    [InlineData(100, 145, true)]
    [InlineData(200, 10, true)]
    public void ScrollCadence_IsStable(
        long previousTick,
        long currentTick,
        bool expected)
    {
        Assert.Equal(
            expected,
            CompactTaskbarDragScrollPolicy
                .IsScrollDue(
                    previousTick,
                    currentTick));
    }

    [Fact]
    public void InvalidGeometry_DoesNotMove()
    {
        CompactTaskbarDragScrollDecision
            decision =
                CompactTaskbarDragScrollPolicy
                    .GetDecision(
                        double.NaN,
                        double.PositiveInfinity,
                        double.NaN,
                        double.NegativeInfinity,
                        true,
                        true,
                        true);

        Assert.False(
            decision.ShouldScroll);
        Assert.Equal(
            0,
            decision.TargetOffset);
    }
}
