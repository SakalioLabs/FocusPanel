using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OkrProgressCalculatorTests
{
    [Theory]
    [InlineData(0, 25, 100, 25)]
    [InlineData(50, 25, 0, 50)]
    [InlineData(10, 10, 10, 100)]
    [InlineData(10, 9, 10, 0)]
    [InlineData(0, 150, 100, 100)]
    public void KeyResultProgressSupportsBothDirectionsAndClamps(
        double start,
        double current,
        double target,
        double expected)
    {
        Assert.Equal(
            expected,
            OkrProgressCalculator
                .CalculateKeyResultProgress(
                    start,
                    current,
                    target));
    }

    [Fact]
    public void ObjectiveProgressUsesPositiveWeights()
    {
        var results = new[]
        {
            new OkrKeyResult
            {
                Progress = 25,
                Weight = 1
            },
            new OkrKeyResult
            {
                Progress = 75,
                Weight = 3
            },
            new OkrKeyResult
            {
                Progress = 100,
                Weight = 0
            }
        };

        Assert.Equal(
            62.5,
            OkrProgressCalculator
                .CalculateObjectiveProgress(results));
    }

    [Fact]
    public void DeletedResultsAreExcludedAndEmptyObjectiveResets()
    {
        var deleted = new OkrKeyResult
        {
            Progress = 100,
            Weight = 1,
            IsDeleted = true
        };

        Assert.Equal(
            0,
            OkrProgressCalculator
                .CalculateObjectiveProgress(
                    new[] { deleted }));
    }
}
