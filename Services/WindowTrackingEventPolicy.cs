namespace FocusPanel.Services;

internal static class WindowTrackingEventPolicy
{
    internal const uint EventSystemAlert = 0x0002;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventSystemMinimizeStart = 0x0016;
    internal const uint EventSystemMinimizeEnd = 0x0017;
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;
    internal const uint EventObjectLocationChange = 0x800B;
    internal const uint EventObjectNameChange = 0x800C;
    internal const int ObjectIdWindow = 0;

    internal static bool ShouldQueueRefresh(
        uint eventType,
        int objectId)
    {
        if (eventType == EventSystemAlert)
            return true;

        if (objectId != ObjectIdWindow)
            return false;

        return eventType == EventSystemForeground
            || eventType
                is >= EventSystemMinimizeStart
                and <= EventSystemMinimizeEnd
            || eventType
                is >= EventObjectCreate
                and <= EventObjectHide
            || eventType
                == EventObjectLocationChange
            || eventType == EventObjectNameChange;
    }
}
