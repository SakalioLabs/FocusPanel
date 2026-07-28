using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellAutoHidePolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false, true)]
    [InlineData(false, false, true, false, false, false)]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(false, false, false, true, true, true)]
    public void DeterminesWhenShellCanHide(
        bool isDragging,
        bool transientInteraction,
        bool cursorInside,
        bool inputFocus,
        bool ignoreInputFocus,
        bool expected)
    {
        Assert.Equal(
            expected,
            ShellAutoHidePolicy.ShouldHide(
                isDragging,
                transientInteraction,
                cursorInside,
                inputFocus,
                ignoreInputFocus));
    }
}
