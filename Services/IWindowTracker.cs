using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

public interface IWindowTracker : IDisposable
{
    event EventHandler? SnapshotChanged;
    IReadOnlyList<WindowTaskItem> GetSnapshot();
    void SetTrackingActive(bool isActive);
    bool ActivateOrMinimize(WindowTaskItem task);
    bool Activate(IntPtr handle);
    bool Minimize(IntPtr handle);
    bool Maximize(IntPtr handle);
    bool Restore(IntPtr handle);
    bool Close(IntPtr handle);
    bool IsForegroundFullscreen();
}
