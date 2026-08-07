using System;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

internal enum VirtualDesktopPresence
{
    Unknown,
    Current,
    Other
}

internal enum VirtualDesktopPlacementResult
{
    AlreadyCurrent,
    Moved,
    Unavailable,
    Failed
}

internal interface IVirtualDesktopPlacementNative
{
    IntPtr GetForegroundWindow();

    bool TryIsWindowOnCurrentDesktop(
        IntPtr windowHandle,
        out bool isCurrent);

    bool TryGetWindowDesktopId(
        IntPtr windowHandle,
        out Guid desktopId);

    bool TryMoveWindowToDesktop(
        IntPtr windowHandle,
        Guid desktopId);
}

internal interface IVirtualDesktopPlacementService
{
    VirtualDesktopPresence GetPresence(
        IntPtr panelWindow);

    VirtualDesktopPlacementResult EnsureOnCurrentDesktop(
        IntPtr panelWindow,
        IntPtr preferredReferenceWindow = default);
}

internal sealed class VirtualDesktopPlacementService :
    IVirtualDesktopPlacementService
{
    private readonly IVirtualDesktopPlacementNative
        _native;

    internal VirtualDesktopPlacementService()
        : this(new VirtualDesktopPlacementNative())
    {
    }

    internal VirtualDesktopPlacementService(
        IVirtualDesktopPlacementNative native)
    {
        _native = native
            ?? throw new ArgumentNullException(
                nameof(native));
    }

    public VirtualDesktopPresence GetPresence(
        IntPtr panelWindow)
    {
        if (panelWindow == IntPtr.Zero
            || !_native.TryIsWindowOnCurrentDesktop(
                panelWindow,
                out bool isCurrent))
        {
            return VirtualDesktopPresence.Unknown;
        }

        return isCurrent
            ? VirtualDesktopPresence.Current
            : VirtualDesktopPresence.Other;
    }

    public VirtualDesktopPlacementResult
        EnsureOnCurrentDesktop(
            IntPtr panelWindow,
            IntPtr preferredReferenceWindow = default)
    {
        if (panelWindow == IntPtr.Zero)
        {
            return VirtualDesktopPlacementResult
                .Unavailable;
        }

        if (GetPresence(panelWindow)
            == VirtualDesktopPresence.Current)
        {
            return VirtualDesktopPlacementResult
                .AlreadyCurrent;
        }

        IntPtr referenceWindow =
            preferredReferenceWindow;
        if (referenceWindow == IntPtr.Zero
            || referenceWindow == panelWindow)
        {
            referenceWindow =
                _native.GetForegroundWindow();
        }

        if (referenceWindow == IntPtr.Zero
            || referenceWindow == panelWindow
            || !_native.TryGetWindowDesktopId(
                referenceWindow,
                out Guid desktopId)
            || desktopId == Guid.Empty)
        {
            return VirtualDesktopPlacementResult
                .Unavailable;
        }

        return _native.TryMoveWindowToDesktop(
                panelWindow,
                desktopId)
            ? VirtualDesktopPlacementResult.Moved
            : VirtualDesktopPlacementResult.Failed;
    }
}

internal sealed class VirtualDesktopPlacementNative :
    IVirtualDesktopPlacementNative
{
    private static readonly Guid ManagerClassId =
        new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    public IntPtr GetForegroundWindow() =>
        NativeMethods.GetForegroundWindow();

    public bool TryIsWindowOnCurrentDesktop(
        IntPtr windowHandle,
        out bool isCurrent)
    {
        isCurrent = false;
        IVirtualDesktopManagerCom? manager =
            TryCreateManager();
        if (manager == null)
            return false;

        try
        {
            return manager
                    .IsWindowOnCurrentVirtualDesktop(
                        windowHandle,
                        out isCurrent)
                >= 0;
        }
        catch
        {
            isCurrent = false;
            return false;
        }
        finally
        {
            ReleaseManager(manager);
        }
    }

    public bool TryGetWindowDesktopId(
        IntPtr windowHandle,
        out Guid desktopId)
    {
        desktopId = Guid.Empty;
        IVirtualDesktopManagerCom? manager =
            TryCreateManager();
        if (manager == null)
            return false;

        try
        {
            return manager.GetWindowDesktopId(
                    windowHandle,
                    out desktopId)
                >= 0;
        }
        catch
        {
            desktopId = Guid.Empty;
            return false;
        }
        finally
        {
            ReleaseManager(manager);
        }
    }

    public bool TryMoveWindowToDesktop(
        IntPtr windowHandle,
        Guid desktopId)
    {
        IVirtualDesktopManagerCom? manager =
            TryCreateManager();
        if (manager == null)
            return false;

        try
        {
            return manager.MoveWindowToDesktop(
                    windowHandle,
                    ref desktopId)
                >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseManager(manager);
        }
    }

    private static IVirtualDesktopManagerCom?
        TryCreateManager()
    {
        try
        {
            Type? managerType =
                Type.GetTypeFromCLSID(
                    ManagerClassId,
                    throwOnError: false);
            return managerType == null
                ? null
                : Activator.CreateInstance(
                        managerType)
                    as IVirtualDesktopManagerCom;
        }
        catch
        {
            return null;
        }
    }

    private static void ReleaseManager(
        IVirtualDesktopManagerCom manager)
    {
        try
        {
            if (Marshal.IsComObject(manager))
                Marshal.FinalReleaseComObject(manager);
        }
        catch
        {
            // Explorer can invalidate the COM object while it restarts.
        }
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManagerCom
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(
            IntPtr topLevelWindow,
            [MarshalAs(UnmanagedType.Bool)]
            out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(
            IntPtr topLevelWindow,
            out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(
            IntPtr topLevelWindow,
            ref Guid desktopId);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr
            GetForegroundWindow();
    }
}
