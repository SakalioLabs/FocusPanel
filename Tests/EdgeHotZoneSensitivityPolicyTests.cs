using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class EdgeHotZoneSensitivityPolicyTests
{
    [Fact]
    public void Options_AreOrderedDistinctAndIncludeDefault()
    {
        int[] values =
            EdgeHotZoneSensitivityPolicy
                .Options
                .Select(option =>
                    option.DwellMilliseconds)
                .ToArray();

        Assert.Equal(
            new[]
            {
                40,
                100,
                180,
                300
            },
            values);
        Assert.Equal(
            values.Length,
            values.Distinct().Count());
        Assert.Contains(
            EdgeHotZoneSensitivityPolicy
                .DefaultDwellMilliseconds,
            values);
        Assert.All(
            EdgeHotZoneSensitivityPolicy.Options,
            option =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        option.DisplayName)));
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(100, 100)]
    [InlineData(180, 180)]
    [InlineData(300, 300)]
    [InlineData(0, 100)]
    [InlineData(120, 100)]
    [InlineData(1000, 100)]
    public void NormalizeDwell_OnlyAcceptsVisibleChoices(
        int input,
        int expected)
    {
        Assert.Equal(
            expected,
            EdgeHotZoneSensitivityPolicy
                .NormalizeDwell(input));
    }
}
