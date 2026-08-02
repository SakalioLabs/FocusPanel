using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TimeEntryPolicyTests
{
    [Fact]
    public void PlainClick_OpensCalendar()
    {
        Assert.Equal(
            TimeEntryAction.ToggleCalendar,
            TimeEntryPolicy.FromLeftClick(
                shiftPressed: false));
    }

    [Fact]
    public void ShiftClick_ShowsDesktop()
    {
        Assert.Equal(
            TimeEntryAction.ShowDesktop,
            TimeEntryPolicy.FromLeftClick(
                shiftPressed: true));
    }
}
