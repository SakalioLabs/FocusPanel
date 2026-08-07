using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class VirtualDesktopPlacementServiceTests
{
    private static readonly Guid CurrentDesktop =
        new("A835B13D-42CA-4A0F-BECF-9F13C7A3D426");

    [Theory]
    [InlineData(true, true, (int)VirtualDesktopPresence.Current)]
    [InlineData(true, false, (int)VirtualDesktopPresence.Other)]
    [InlineData(false, false, (int)VirtualDesktopPresence.Unknown)]
    public void GetPresence_MapsNativeResult(
        bool readSucceeded,
        bool isCurrent,
        int expectedValue)
    {
        var native = new FakeNative
        {
            PresenceReadSucceeded = readSucceeded,
            IsCurrent = isCurrent
        };
        var service =
            new VirtualDesktopPlacementService(native);

        Assert.Equal(
            (VirtualDesktopPresence)expectedValue,
            service.GetPresence(new IntPtr(10)));
    }

    [Fact]
    public void EnsureOnCurrentDesktop_DoesNothingWhenAlreadyCurrent()
    {
        var native = new FakeNative
        {
            IsCurrent = true
        };
        var service =
            new VirtualDesktopPlacementService(native);

        VirtualDesktopPlacementResult result =
            service.EnsureOnCurrentDesktop(
                new IntPtr(10),
                new IntPtr(20));

        Assert.Equal(
            VirtualDesktopPlacementResult.AlreadyCurrent,
            result);
        Assert.Equal(0, native.MoveCalls);
        Assert.Equal(0, native.DesktopIdCalls);
    }

    [Fact]
    public void EnsureOnCurrentDesktop_MovesPanelToPreferredWindowsDesktop()
    {
        var native = new FakeNative
        {
            IsCurrent = false,
            DesktopId = CurrentDesktop
        };
        var service =
            new VirtualDesktopPlacementService(native);

        VirtualDesktopPlacementResult result =
            service.EnsureOnCurrentDesktop(
                new IntPtr(10),
                new IntPtr(20));

        Assert.Equal(
            VirtualDesktopPlacementResult.Moved,
            result);
        Assert.Equal(new IntPtr(20), native.LastDesktopIdWindow);
        Assert.Equal(new IntPtr(10), native.LastMovedWindow);
        Assert.Equal(CurrentDesktop, native.LastMovedDesktop);
        Assert.Equal(0, native.ForegroundCalls);
    }

    [Fact]
    public void EnsureOnCurrentDesktop_FallsBackToForegroundWindow()
    {
        var native = new FakeNative
        {
            IsCurrent = false,
            ForegroundWindow = new IntPtr(30),
            DesktopId = CurrentDesktop
        };
        var service =
            new VirtualDesktopPlacementService(native);

        VirtualDesktopPlacementResult result =
            service.EnsureOnCurrentDesktop(
                new IntPtr(10),
                new IntPtr(10));

        Assert.Equal(
            VirtualDesktopPlacementResult.Moved,
            result);
        Assert.Equal(1, native.ForegroundCalls);
        Assert.Equal(new IntPtr(30), native.LastDesktopIdWindow);
    }

    [Theory]
    [InlineData(false, true, (int)VirtualDesktopPlacementResult.Unavailable)]
    [InlineData(true, false, (int)VirtualDesktopPlacementResult.Failed)]
    public void EnsureOnCurrentDesktop_ReportsUnavailableOrMoveFailure(
        bool desktopReadSucceeded,
        bool moveSucceeded,
        int expectedValue)
    {
        var native = new FakeNative
        {
            IsCurrent = false,
            ForegroundWindow = new IntPtr(30),
            DesktopReadSucceeded = desktopReadSucceeded,
            DesktopId = CurrentDesktop,
            MoveSucceeded = moveSucceeded
        };
        var service =
            new VirtualDesktopPlacementService(native);

        Assert.Equal(
            (VirtualDesktopPlacementResult)expectedValue,
            service.EnsureOnCurrentDesktop(
                new IntPtr(10)));
    }

    [Fact]
    public void EnsureOnCurrentDesktop_RejectsInvalidPanelHandle()
    {
        var native = new FakeNative();
        var service =
            new VirtualDesktopPlacementService(native);

        Assert.Equal(
            VirtualDesktopPlacementResult.Unavailable,
            service.EnsureOnCurrentDesktop(
                IntPtr.Zero));
        Assert.Equal(0, native.PresenceCalls);
        Assert.Equal(0, native.MoveCalls);
    }

    private sealed class FakeNative :
        IVirtualDesktopPlacementNative
    {
        internal bool PresenceReadSucceeded { get; set; } = true;
        internal bool IsCurrent { get; set; }
        internal IntPtr ForegroundWindow { get; set; }
        internal bool DesktopReadSucceeded { get; set; } = true;
        internal Guid DesktopId { get; set; } = CurrentDesktop;
        internal bool MoveSucceeded { get; set; } = true;
        internal int PresenceCalls { get; private set; }
        internal int ForegroundCalls { get; private set; }
        internal int DesktopIdCalls { get; private set; }
        internal int MoveCalls { get; private set; }
        internal IntPtr LastDesktopIdWindow { get; private set; }
        internal IntPtr LastMovedWindow { get; private set; }
        internal Guid LastMovedDesktop { get; private set; }

        public IntPtr GetForegroundWindow()
        {
            ForegroundCalls++;
            return ForegroundWindow;
        }

        public bool TryIsWindowOnCurrentDesktop(
            IntPtr windowHandle,
            out bool isCurrent)
        {
            PresenceCalls++;
            isCurrent = IsCurrent;
            return PresenceReadSucceeded;
        }

        public bool TryGetWindowDesktopId(
            IntPtr windowHandle,
            out Guid desktopId)
        {
            DesktopIdCalls++;
            LastDesktopIdWindow = windowHandle;
            desktopId = DesktopId;
            return DesktopReadSucceeded;
        }

        public bool TryMoveWindowToDesktop(
            IntPtr windowHandle,
            Guid desktopId)
        {
            MoveCalls++;
            LastMovedWindow = windowHandle;
            LastMovedDesktop = desktopId;
            return MoveSucceeded;
        }
    }
}
