namespace FocusPanel.Services;

public readonly record struct SystemStatusSnapshot(
    AudioStatusSnapshot Audio,
    NetworkStatusSnapshot Network,
    InputMethodStatusSnapshot InputMethod,
    BatteryStatusSnapshot Battery);
