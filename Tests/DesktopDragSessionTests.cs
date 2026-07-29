using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopDragSessionTests
{
    [Fact]
    public void ExternalDrag_EndsWhenItLeavesShell()
    {
        var session =
            new DesktopDragSession();
        session.Begin(ownedByPanel: false);

        bool ended = session.EndExternal();

        Assert.True(ended);
        Assert.False(session.IsActive);
        Assert.False(session.IsOwnedByPanel);
    }

    [Fact]
    public void PanelOwnedDrag_DoesNotEndOnShellLeave()
    {
        var session =
            new DesktopDragSession();
        session.Begin(ownedByPanel: true);

        bool ended = session.EndExternal();

        Assert.False(ended);
        Assert.True(session.IsActive);
        Assert.True(session.IsOwnedByPanel);
    }

    [Fact]
    public void RepeatedExternalEnter_DoesNotDowngradeOwnedDrag()
    {
        var session =
            new DesktopDragSession();
        session.Begin(ownedByPanel: true);
        session.Begin(ownedByPanel: false);

        Assert.False(session.EndExternal());
        Assert.True(session.IsOwnedByPanel);
    }

    [Fact]
    public void ExplicitEnd_ResetsEverySessionKind()
    {
        var session =
            new DesktopDragSession();
        session.Begin(ownedByPanel: true);

        session.End();

        Assert.False(session.IsActive);
        Assert.False(session.IsOwnedByPanel);
        Assert.False(session.EndExternal());
    }
}
