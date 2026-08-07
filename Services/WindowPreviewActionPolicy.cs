using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class WindowPreviewActionPolicy
{
    internal static WindowStateAction
        GetResizeAction(
            TrackedWindowState state) =>
        state == TrackedWindowState.Maximized
            ? WindowStateAction.Restore
            : WindowStateAction.Maximize;
}
