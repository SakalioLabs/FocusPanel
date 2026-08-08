using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CompactDockDensityPolicyTests
{
    [Theory]
    [InlineData(488, 44)]
    [InlineData(639.9, 44)]
    [InlineData(640, 54)]
    [InlineData(820, 54)]
    public void EntryHeight_PreservesApplicationSpaceOnShortPanels(
        double panelHeight,
        double expected)
    {
        Assert.Equal(
            expected,
            CompactDockDensityPolicy
                .GetEntryHeight(panelHeight));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EntryHeight_UsesNormalSizeBeforePlacement(
        double panelHeight)
    {
        Assert.Equal(
            CompactDockDensityPolicy
                .NormalEntryHeightDip,
            CompactDockDensityPolicy
                .GetEntryHeight(panelHeight));
    }
}
