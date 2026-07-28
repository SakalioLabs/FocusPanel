namespace FocusPanel.Services;

internal static class WindowTrackingEventPolicy
{
    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;
    internal const uint EventObjectNameChange = 0x800C;
    internal const int ObjectIdWindow = 0;

    internal static bool ShouldQueueRefresh(
        uint eventType,
        int objectId)
    {
        if (objectId != ObjectIdWindow)
            return false;

        return eventType == EventSystemForeground
            || eventType
                is >= EventObjectCreate
                and <= EventObjectHide
            || eventType == EventObjectNameChange;
    }
}
