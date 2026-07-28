using System;

namespace FocusPanel.Services;

public interface ISystemStatusService : IDisposable
{
    AudioStatusSnapshot GetAudioStatus();
    bool TrySetMasterVolume(float value);
    bool TrySetMuted(bool value);
    NetworkStatusSnapshot GetNetworkStatus();
    InputMethodStatusSnapshot GetInputMethodStatus();
    BatteryStatusSnapshot GetBatteryStatus();
    bool OpenQuickSettings();
    bool OpenNotifications();
    bool OpenInputSwitcher();
    bool OpenStartMenu();
    bool OpenTaskView();
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
