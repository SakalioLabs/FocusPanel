using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellPanelEdgePolicyTests
{
    [Theory]
    [InlineData("Left", "Left")]
    [InlineData("Right", "Right")]
    [InlineData("invalid", "Right")]
    [InlineData(null, "Right")]
    public void Parse_UsesRightAsSafeDefault(
        string? value,
        string expected)
    {
        Assert.Equal(
            expected,
            ShellPanelEdgePolicy
                .Parse(value)
                .ToString());
    }
}
