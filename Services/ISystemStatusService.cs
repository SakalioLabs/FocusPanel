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
    bool OpenNotificationOverflow();
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
    bool OpenSoundOutput();
    bool OpenScreenSnipping();
    bool OpenProjectDisplay();
    bool OpenCastDevices();
    bool OpenManagementTool(SystemManagementTool tool);
    bool OpenPowerSettings();
    bool OpenLocationPrivacySettings();
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
