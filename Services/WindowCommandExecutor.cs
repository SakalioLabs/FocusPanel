using System;
using System.Drawing;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal interface IWindowCommandBoundary
{
    bool IsWindow(IntPtr handle);
    IntPtr GetForegroundWindow();
    bool IsIconic(IntPtr handle);
    bool IsZoomed(IntPtr handle);
    void ShowWindow(IntPtr handle, int command);
    bool SetForegroundWindow(IntPtr handle);
    bool PostClose(IntPtr handle);
    bool TryGetRestoredBounds(
        IntPtr handle,
        out Rectangle bounds);
    Rectangle GetWorkingArea(IntPtr handle);
    bool SetRestoredBounds(
        IntPtr handle,
        Rectangle bounds);
    bool SetTopmost(
        IntPtr handle,
        bool isTopmost);
}

internal sealed class WindowCommandExecutor
{
    internal const int MinimizeCommand = 6;
    internal const int MaximizeCommand = 3;
    internal const int RestoreCommand = 9;

    private readonly IWindowCommandBoundary _native;

    internal WindowCommandExecutor(
        IWindowCommandBoundary native)
    {
        _native = native;
    }

    internal bool ActivateOrMinimize(
        WindowTaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        IntPtr handle = task.PrimaryHandle;
        if (!IsUsable(handle))
            return false;

        if (_native.GetForegroundWindow() == handle)
        {
            _native.ShowWindow(
                handle,
                MinimizeCommand);
            return _native.IsIconic(handle);
        }

        return Activate(handle);
    }

    internal bool Activate(IntPtr handle)
    {
        if (!IsUsable(handle))
            return false;

        if (_native.IsIconic(handle))
        {
            _native.ShowWindow(
                handle,
                RestoreCommand);
        }

        return _native.SetForegroundWindow(handle)
            || _native.GetForegroundWindow() == handle;
    }

    internal bool Close(IntPtr handle) =>
        IsUsable(handle)
        && _native.PostClose(handle);

    internal bool CanMoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea) =>
        IsUsable(handle)
        && WindowDisplayMovePolicy.CanMove(
            _native.GetWorkingArea(handle),
            targetWorkArea);

    internal bool MoveToDisplay(
        IntPtr handle,
        Rectangle targetWorkArea)
    {
        if (!IsUsable(handle))
            return false;

        Rectangle sourceWorkArea =
            _native.GetWorkingArea(handle);
        if (!WindowDisplayMovePolicy.CanMove(
                sourceWorkArea,
                targetWorkArea)
            || !_native.TryGetRestoredBounds(
                handle,
                out Rectangle currentBounds))
        {
            return false;
        }

        Rectangle targetBounds =
            WindowDisplayMovePolicy.CalculateBounds(
                currentBounds,
                sourceWorkArea,
                targetWorkArea);
        return targetBounds != Rectangle.Empty
            && _native.SetRestoredBounds(
                handle,
                targetBounds);
    }

    internal bool SetTopmost(
        IntPtr handle,
        bool isTopmost) =>
        IsUsable(handle)
        && _native.SetTopmost(
            handle,
            isTopmost);

    internal bool Minimize(IntPtr handle)
    {
        if (!IsUsable(handle))
            return false;

        _native.ShowWindow(
            handle,
            MinimizeCommand);
        return _native.IsIconic(handle);
    }

    internal bool Maximize(IntPtr handle)
    {
        if (!IsUsable(handle))
            return false;

        _native.ShowWindow(
            handle,
            MaximizeCommand);
        return _native.IsZoomed(handle);
    }

    internal bool Restore(IntPtr handle)
    {
        if (!IsUsable(handle))
            return false;

        _native.ShowWindow(
            handle,
            RestoreCommand);
        return !_native.IsIconic(handle)
            && !_native.IsZoomed(handle);
    }

    private bool IsUsable(IntPtr handle) =>
        handle != IntPtr.Zero
        && _native.IsWindow(handle);
}
