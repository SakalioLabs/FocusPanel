using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellTransientSurfacePolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void CombinesEveryTransientSurfaceSource(
        bool explicitInteraction,
        bool mouseCapture,
        bool selectionPopup,
        bool expected)
    {
        Assert.Equal(
            expected,
            ShellTransientSurfacePolicy.IsActive(
                explicitInteraction,
                mouseCapture,
                selectionPopup));
    }
}
