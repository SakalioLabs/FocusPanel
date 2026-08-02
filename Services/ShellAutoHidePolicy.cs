namespace FocusPanel.Services;

public enum ShellAutoHideAction
{
    None,
    CollapseToCompact,
    HideShell
}

public enum PersistentCompactDockAvailabilityAction
{
    None,
    HideForUnavailableEdge,
    RestoreAfterUnavailableEdge
}

public readonly record struct
    PersistentCompactDockAvailabilityDecision(
        PersistentCompactDockAvailabilityAction Action,
        bool IsHiddenForUnavailableEdge);

public static class ShellAutoHidePolicy
{
    public static PersistentCompactDockAvailabilityDecision
        DecideAvailabilityChange(
            bool isAvailable,
            bool keepCompactDockVisible,
            bool isHiddenToTray,
            bool isExiting,
            bool isShellVisible,
            bool wasHiddenForUnavailableEdge)
    {
        if (!isAvailable)
        {
            bool shouldHide =
                keepCompactDockVisible
                && !isHiddenToTray
                && !isExiting
                && isShellVisible;
            return new(
                shouldHide
                    ? PersistentCompactDockAvailabilityAction
                        .HideForUnavailableEdge
                    : PersistentCompactDockAvailabilityAction
                        .None,
                shouldHide
                || wasHiddenForUnavailableEdge);
        }

        if (!wasHiddenForUnavailableEdge)
        {
            return new(
                PersistentCompactDockAvailabilityAction.None,
                false);
        }

        bool shouldRestore =
            keepCompactDockVisible
            && !isHiddenToTray
            && !isExiting;
        return new(
            shouldRestore
                ? PersistentCompactDockAvailabilityAction
                    .RestoreAfterUnavailableEdge
                : PersistentCompactDockAvailabilityAction.None,
            false);
    }

    public static ShellAutoHideAction Decide(
        bool keepCompactDockVisible,
        bool isWorkspaceExpanded,
        bool isWorkspacePinned,
        bool isDragging,
        bool isTransientInteractionActive,
        bool isCursorInside,
        bool isInputFocusActive,
        bool ignoreInputFocus)
    {
        if (isWorkspacePinned
            || isDragging
            || isTransientInteractionActive
            || isCursorInside
            || (!ignoreInputFocus
                && isInputFocusActive))
        {
            return ShellAutoHideAction.None;
        }

        if (!keepCompactDockVisible)
            return ShellAutoHideAction.HideShell;

        return isWorkspaceExpanded
            ? ShellAutoHideAction.CollapseToCompact
            : ShellAutoHideAction.None;
    }

    public static bool ShouldHide(
        bool isWorkspacePinned,
        bool isDragging,
        bool isTransientInteractionActive,
        bool isCursorInside,
        bool isInputFocusActive,
        bool ignoreInputFocus)
        => Decide(
                keepCompactDockVisible: false,
                isWorkspaceExpanded: false,
                isWorkspacePinned,
                isDragging,
                isTransientInteractionActive,
                isCursorInside,
                isInputFocusActive,
                ignoreInputFocus)
            == ShellAutoHideAction.HideShell;
}
