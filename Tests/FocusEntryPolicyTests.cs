using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusEntryPolicyTests
{
    [Fact]
    public void PlainClick_OpensFocusCenter()
    {
        Assert.Equal(
            FocusEntryAction.ToggleFocusCenter,
            FocusEntryPolicy.FromLeftClick(
                shiftPressed: false));
    }

    [Fact]
    public void ShiftClick_OpensLastWorkspace()
    {
        Assert.Equal(
            FocusEntryAction.OpenLastWorkspace,
            FocusEntryPolicy.FromLeftClick(
                shiftPressed: true));
    }
}
