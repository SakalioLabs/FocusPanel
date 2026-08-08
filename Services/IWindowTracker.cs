using System;
using System.Collections.Generic;
using System.Drawing;
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
    bool Arrange(
        IntPtr handle,
        WindowLayoutTarget target);
    bool CanMoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea);
    bool MoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea);
    bool SetTopmost(
        IntPtr handle,
        bool isTopmost);
    bool Close(IntPtr handle);
    bool IsForegroundFullscreen();
}
