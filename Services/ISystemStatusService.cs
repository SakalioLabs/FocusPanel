using System;

namespace FocusPanel.Services;

public interface ISystemStatusService : IDisposable
{
    float MasterVolume { get; set; }
    bool IsMuted { get; set; }
    bool IsNetworkAvailable { get; }
    string NetworkDisplayName { get; }
    bool HasBattery { get; }
    int BatteryPercent { get; }
    void OpenQuickSettings();
    void OpenNotifications();
    void OpenInputSwitcher();
    void OpenPowerSettings();
    void ShowDesktop();
    void Lock();
    void Sleep();
    void Restart();
    void Shutdown();
}
