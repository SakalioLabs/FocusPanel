using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ShellAutoHidePolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false, false, true)]
    [InlineData(true, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, true, true, false)]
    [InlineData(false, false, false, true, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, true, true, true)]
    public void DeterminesWhenShellCanHide(
        bool workspacePinned,
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
                workspacePinned,
                isDragging,
                transientInteraction,
                cursorInside,
                inputFocus,
                ignoreInputFocus));
    }

    [Theory]
    [InlineData(false, false, ShellAutoHideAction.HideShell)]
    [InlineData(false, true, ShellAutoHideAction.HideShell)]
    [InlineData(true, false, ShellAutoHideAction.None)]
    [InlineData(true, true, ShellAutoHideAction.CollapseToCompact)]
    public void PersistentCompactMode_OnlyCollapsesExpandedWorkspace(
        bool keepCompactDockVisible,
        bool workspaceExpanded,
        ShellAutoHideAction expected)
    {
        Assert.Equal(
            expected,
            ShellAutoHidePolicy.Decide(
                keepCompactDockVisible,
                workspaceExpanded,
                isWorkspacePinned: false,
                isDragging: false,
                isTransientInteractionActive: false,
                isCursorInside: false,
                isInputFocusActive: false,
                ignoreInputFocus: false));
    }

    [Theory]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(false, false, false, false, true, false)]
    public void PersistentCompactMode_RespectsEveryInteractionLock(
        bool workspacePinned,
        bool dragging,
        bool transientInteraction,
        bool cursorInside,
        bool inputFocus,
        bool ignoreInputFocus)
    {
        Assert.Equal(
            ShellAutoHideAction.None,
            ShellAutoHidePolicy.Decide(
                keepCompactDockVisible: true,
                isWorkspaceExpanded: true,
                workspacePinned,
                dragging,
                transientInteraction,
                cursorInside,
                inputFocus,
                ignoreInputFocus));
    }

    [Fact]
    public void ForcedFocusRelease_CanCollapsePersistentWorkspace()
    {
        Assert.Equal(
            ShellAutoHideAction.CollapseToCompact,
            ShellAutoHidePolicy.Decide(
                keepCompactDockVisible: true,
                isWorkspaceExpanded: true,
                isWorkspacePinned: false,
                isDragging: false,
                isTransientInteractionActive: false,
                isCursorInside: false,
                isInputFocusActive: true,
                ignoreInputFocus: true));
    }

    [Theory]
    [InlineData(true, true, false, false, true, false,
        PersistentCompactDockAvailabilityAction.None, false)]
    [InlineData(false, true, false, false, true, false,
        PersistentCompactDockAvailabilityAction.HideForUnavailableEdge, true)]
    [InlineData(false, false, false, false, true, false,
        PersistentCompactDockAvailabilityAction.None, false)]
    [InlineData(false, true, true, false, true, false,
        PersistentCompactDockAvailabilityAction.None, false)]
    [InlineData(false, true, false, true, true, false,
        PersistentCompactDockAvailabilityAction.None, false)]
    [InlineData(true, true, false, false, false, true,
        PersistentCompactDockAvailabilityAction.RestoreAfterUnavailableEdge, false)]
    [InlineData(true, true, true, false, false, true,
        PersistentCompactDockAvailabilityAction.None, false)]
    [InlineData(true, false, false, false, false, true,
        PersistentCompactDockAvailabilityAction.None, false)]
    public void AvailabilityTransition_RespectsPersistentAndExplicitHideState(
        bool isAvailable,
        bool keepCompactDockVisible,
        bool isHiddenToTray,
        bool isExiting,
        bool isShellVisible,
        bool wasHiddenForUnavailableEdge,
        PersistentCompactDockAvailabilityAction expectedAction,
        bool expectedHiddenState)
    {
        PersistentCompactDockAvailabilityDecision decision =
            ShellAutoHidePolicy.DecideAvailabilityChange(
                isAvailable,
                keepCompactDockVisible,
                isHiddenToTray,
                isExiting,
                isShellVisible,
                wasHiddenForUnavailableEdge);

        Assert.Equal(expectedAction, decision.Action);
        Assert.Equal(
            expectedHiddenState,
            decision.IsHiddenForUnavailableEdge);
    }
}
