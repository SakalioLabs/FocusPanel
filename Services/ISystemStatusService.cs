using System;
using System.Collections.Generic;

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
    IReadOnlyList<InputMethodOption>
        GetInputMethods();
    bool TryActivateInputMethod(
        InputMethodOption inputMethod,
        IntPtr preferredTargetWindow);
    BatteryStatusSnapshot GetBatteryStatus();
    bool OpenQuickSettings();
    bool OpenNotifications();
    bool OpenInputSwitcher();
    bool OpenTaskView();
    bool SwitchVirtualDesktop(
        VirtualDesktopDirection direction);
    bool CreateVirtualDesktop();
    bool CloseCurrentVirtualDesktop();
    bool OpenWidgets();
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
