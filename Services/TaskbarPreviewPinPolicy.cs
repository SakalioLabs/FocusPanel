namespace FocusPanel.Services;

internal enum TaskbarPreviewClickAction
{
    ActivateOrLaunch,
    OpenPinnedPreview,
    PinExistingPreview,
    ClosePinnedPreview
}

internal static class TaskbarPreviewPinPolicy
{
    internal static TaskbarPreviewClickAction Resolve(
        int windowCount,
        bool isSamePreviewVisible,
        bool isPreviewPinned)
    {
        if (windowCount <= 1)
        {
            return TaskbarPreviewClickAction
                .ActivateOrLaunch;
        }

        if (!isSamePreviewVisible)
        {
            return TaskbarPreviewClickAction
                .OpenPinnedPreview;
        }

        return isPreviewPinned
            ? TaskbarPreviewClickAction
                .ClosePinnedPreview
            : TaskbarPreviewClickAction
                .PinExistingPreview;
    }
}
