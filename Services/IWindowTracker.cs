using System;
using System.Collections.Generic;
using FocusPanel.Models;

namespace FocusPanel.Services;

public interface IWindowTracker : IDisposable
{
    event EventHandler? SnapshotChanged;
    IReadOnlyList<WindowTaskItem> GetSnapshot();
    void ActivateOrMinimize(WindowTaskItem task);
    void Activate(IntPtr handle);
    void Close(IntPtr handle);
    bool IsForegroundFullscreen();
}
