using System;

namespace FocusPanel.Services;

public interface ISystemStatusService : IDisposable
{
    float MasterVolume { get; set; }
    bool IsMuted { get; set; }
    bool IsNetworkAvailable { get; }
    string NetworkDisplayName { get; }
    string NetworkDetail { get; }
    bool HasBattery { get; }
    int BatteryPercent { get; }
    bool IsCharging { get; }
    bool OpenQuickSettings();
    bool OpenNotifications();
    bool OpenInputSwitcher();
    void OpenPowerSettings();
    void ShowDesktop();
    void Lock();
    void Sleep();
    void Restart();
    void Shutdown();
}
