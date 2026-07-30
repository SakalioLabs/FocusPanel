using System;

namespace FocusPanel.Services;

public interface ISystemStatusService : IDisposable
{
    SystemStatusSnapshot GetStatusSnapshot();
    AudioStatusSnapshot GetAudioStatus();
    bool TrySetMasterVolume(float value);
    bool TrySetMuted(bool value);
    bool SendMediaCommand(
        MediaTransportAction action);
    NetworkStatusSnapshot GetNetworkStatus();
    InputMethodStatusSnapshot GetInputMethodStatus();
    BatteryStatusSnapshot GetBatteryStatus();
    bool OpenQuickSettings();
    bool OpenNotifications();
    bool OpenInputSwitcher();
    bool OpenStartMenu();
    bool OpenTaskView();
    bool SwitchVirtualDesktop(
        VirtualDesktopDirection direction);
    bool CreateVirtualDesktop();
    bool CloseCurrentVirtualDesktop();
    bool OpenWindowsSearch();
    bool OpenWidgets();
    bool OpenRunDialog();
    bool OpenManagementTool(SystemManagementTool tool);
    bool OpenPowerSettings();
    bool ShowDesktop();
    bool Lock();
    bool Sleep();
    bool Restart();
    bool Shutdown();
}

public enum VirtualDesktopDirection
{
    Previous,
    Next
}

public enum MediaTransportAction
{
    PreviousTrack,
    PlayPause,
    NextTrack
}
