using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class CompactOverlayTogglePolicyTests
{
    [Fact]
    public void ClosedSurface_ExpandsAndOpensOnFirstClick()
    {
        Assert.Equal(
            CompactOverlayToggleAction.ExpandAndOpen,
            CompactOverlayTogglePolicy.Decide(false));
    }

    [Fact]
    public void OpenOwnedSurface_CollapsesOnSecondClick()
    {
        Assert.Equal(
            CompactOverlayToggleAction.Collapse,
            CompactOverlayTogglePolicy.Decide(true));
    }
}
