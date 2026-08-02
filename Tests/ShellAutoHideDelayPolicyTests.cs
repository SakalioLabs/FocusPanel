using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellAutoHideDelayPolicyTests
{
    [Fact]
    public void Options_AreDistinctOrderedAndIncludeDefault()
    {
        int[] values =
            ShellAutoHideDelayPolicy
                .Options
                .Select(option => option.Value)
                .ToArray();

        Assert.Equal(
            new[]
            {
                300,
                500,
                800,
                1200
            },
            values);
        Assert.Equal(
            values.Length,
            values.Distinct().Count());
        Assert.Contains(
            ShellAutoHideDelayPolicy
                .DefaultMilliseconds,
            values);
        Assert.All(
            ShellAutoHideDelayPolicy.Options,
            option =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        option.DisplayName)));
    }

    [Theory]
    [InlineData(300, 300)]
    [InlineData(500, 500)]
    [InlineData(800, 800)]
    [InlineData(1200, 1200)]
    [InlineData(0, 500)]
    [InlineData(350, 500)]
    [InlineData(5000, 500)]
    public void Normalize_OnlyAcceptsExposedChoices(
        int input,
        int expected)
    {
        Assert.Equal(
            expected,
            ShellAutoHideDelayPolicy
                .Normalize(input));
    }
}
