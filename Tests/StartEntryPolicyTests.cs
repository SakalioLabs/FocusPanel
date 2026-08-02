using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class StartEntryPolicyTests
{
    [Fact]
    public void PlainLeftClick_OpensFocusPanelStart()
    {
        Assert.Equal(
            StartEntryAction.ToggleFocusPanelStart,
            StartEntryPolicy.FromLeftClick(
                shiftPressed: false));
    }

    [Fact]
    public void ShiftLeftClick_OpensWindowsStart()
    {
        Assert.Equal(
            StartEntryAction.OpenWindowsStart,
            StartEntryPolicy.FromLeftClick(
                shiftPressed: true));
    }
}
