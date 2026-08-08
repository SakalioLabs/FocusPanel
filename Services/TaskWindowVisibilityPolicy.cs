namespace FocusPanel.Services;

internal static class TaskWindowVisibilityPolicy
{
    internal static bool ShouldInclude(
        bool isVisible,
        bool isToolWindow,
        bool isNoActivate,
        bool hasOwner,
        bool isAppWindow,
        bool isCloaked) =>
        isVisible
        && !isToolWindow
        && !isNoActivate
        && (!hasOwner || isAppWindow)
        && !isCloaked;
}
