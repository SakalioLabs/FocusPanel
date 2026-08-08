using System;
using System.Collections.Generic;
using System.Drawing;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowCommandExecutorTests
{
    private static readonly IntPtr Handle =
        new(42);

    [Fact]
    public void ZeroOrStaleHandle_IsRejectedWithoutNativeAction()
    {
        var native = new FakeBoundary
        {
            WindowExists = false
        };
        var executor = new WindowCommandExecutor(native);

        Assert.False(executor.Activate(IntPtr.Zero));
        Assert.False(executor.Close(Handle));
        Assert.False(executor.Minimize(Handle));
        Assert.False(executor.Maximize(Handle));
        Assert.False(executor.Restore(Handle));
        Assert.False(
            executor.Arrange(
                Handle,
                WindowLayoutTarget.LeftHalf));
        Assert.False(
            executor.SetTopmost(
                Handle,
                true));
        Assert.False(
            executor.ActivateOrMinimize(
                Task(Handle)));
        Assert.Empty(native.ShowCommands);
        Assert.Equal(0, native.ForegroundCalls);
        Assert.Equal(0, native.CloseCalls);
        Assert.Empty(native.TopmostRequests);
        Assert.Equal(0, native.SetBoundsCalls);
    }

    [Fact]
    public void ExplicitMinimize_IsConfirmed()
    {
        var native = new FakeBoundary
        {
            BecomeIconicWhenMinimized = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(executor.Minimize(Handle));
        Assert.Equal(
            WindowCommandExecutor.MinimizeCommand,
            Assert.Single(
                native.ShowCommands));
    }

    [Fact]
    public void ExplicitMaximize_IsConfirmed()
    {
        var native = new FakeBoundary
        {
            BecomeZoomedWhenMaximized = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(executor.Maximize(Handle));
        Assert.Equal(
            WindowCommandExecutor.MaximizeCommand,
            Assert.Single(
                native.ShowCommands));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Restore_ClearsMinimizedOrMaximizedState(
        bool iconic,
        bool zoomed)
    {
        var native = new FakeBoundary
        {
            Iconic = iconic,
            Zoomed = zoomed
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(executor.Restore(Handle));
        Assert.Equal(
            WindowCommandExecutor.RestoreCommand,
            Assert.Single(
                native.ShowCommands));
        Assert.False(native.Iconic);
        Assert.False(native.Zoomed);
    }

    [Fact]
    public void IgnoredStateChange_IsReportedAsFailure()
    {
        var native = new FakeBoundary();
        var executor =
            new WindowCommandExecutor(native);

        Assert.False(executor.Minimize(Handle));
        Assert.False(executor.Maximize(Handle));
    }

    [Fact]
    public void ForegroundWindow_IsMinimizedAndConfirmed()
    {
        var native = new FakeBoundary
        {
            Foreground = Handle,
            BecomeIconicWhenMinimized = true
        };
        var executor = new WindowCommandExecutor(native);

        Assert.True(
            executor.ActivateOrMinimize(
                Task(Handle)));
        Assert.Equal(
            new[]
            {
                WindowCommandExecutor.MinimizeCommand
            },
            native.ShowCommands);
        Assert.Equal(0, native.ForegroundCalls);
    }

    [Fact]
    public void MinimizeThatDoesNotChangeState_IsReportedAsFailure()
    {
        var native = new FakeBoundary
        {
            Foreground = Handle,
            BecomeIconicWhenMinimized = false
        };
        var executor = new WindowCommandExecutor(native);

        Assert.False(
            executor.ActivateOrMinimize(
                Task(Handle)));
    }

    [Fact]
    public void MinimizedWindow_IsRestoredBeforeActivation()
    {
        var native = new FakeBoundary
        {
            Iconic = true,
            ForegroundResult = true
        };
        var executor = new WindowCommandExecutor(native);

        Assert.True(executor.Activate(Handle));
        Assert.Equal(
            new[]
            {
                WindowCommandExecutor.RestoreCommand
            },
            native.ShowCommands);
        Assert.Equal(1, native.ForegroundCalls);
    }

    [Fact]
    public void ActivationUsesObservedForegroundAsFinalConfirmation()
    {
        var native = new FakeBoundary
        {
            ForegroundResult = false,
            BecomeForegroundWhenRequested = true
        };
        var executor = new WindowCommandExecutor(native);

        Assert.True(executor.Activate(Handle));
    }

    [Fact]
    public void ForegroundRestriction_IsReportedAsFailure()
    {
        var native = new FakeBoundary
        {
            ForegroundResult = false,
            BecomeForegroundWhenRequested = false
        };
        var executor = new WindowCommandExecutor(native);

        Assert.False(executor.Activate(Handle));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Close_ReturnsPostMessageResult(
        bool postResult)
    {
        var native = new FakeBoundary
        {
            CloseResult = postResult
        };
        var executor = new WindowCommandExecutor(native);

        Assert.Equal(
            postResult,
            executor.Close(Handle));
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void DifferentDisplay_IsMovedWithoutActivation()
    {
        var native = new FakeBoundary
        {
            WorkingArea =
                new Rectangle(
                    0,
                    0,
                    1920,
                    1040),
            RestoredBounds =
                new Rectangle(
                    960,
                    520,
                    960,
                    520),
            SetBoundsResult = true
        };
        var executor =
            new WindowCommandExecutor(native);
        var target = new Rectangle(
            1920,
            0,
            1280,
            680);

        Assert.True(
            executor.CanMoveToDisplay(
                Handle,
                target));
        Assert.True(
            executor.MoveToDisplay(
                Handle,
                target));
        Assert.Equal(
            new Rectangle(
                2240,
                160,
                960,
                520),
            native.LastSetBounds);
        Assert.Equal(0, native.ForegroundCalls);
    }

    [Fact]
    public void SameDisplay_DoesNotWriteWindowPlacement()
    {
        var area = new Rectangle(
            0,
            0,
            1920,
            1040);
        var native = new FakeBoundary
        {
            WorkingArea = area,
            RestoredBounds =
                new Rectangle(
                    120,
                    80,
                    1000,
                    700),
            SetBoundsResult = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.False(
            executor.CanMoveToDisplay(
                Handle,
                area));
        Assert.False(
            executor.MoveToDisplay(
                Handle,
                area));
        Assert.Equal(0, native.SetBoundsCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TopmostToggle_IsForwardedWithoutActivation(
        bool target)
    {
        var native = new FakeBoundary
        {
            SetTopmostResult = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(
            executor.SetTopmost(
                Handle,
                target));
        Assert.Equal(
            target,
            Assert.Single(
                native.TopmostRequests));
        Assert.Equal(0, native.ForegroundCalls);
        Assert.Empty(native.ShowCommands);
    }

    [Fact]
    public void RejectedTopmostToggle_IsReportedAsFailure()
    {
        var native = new FakeBoundary
        {
            SetTopmostResult = false
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.False(
            executor.SetTopmost(
                Handle,
                true));
        Assert.True(
            Assert.Single(
                native.TopmostRequests));
    }

    [Fact]
    public void Arrange_NormalWindowWritesTargetBoundsWithoutActivation()
    {
        var native = new FakeBoundary
        {
            WorkingArea =
                new Rectangle(
                    -1920,
                    0,
                    1920,
                    1040),
            SetBoundsResult = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(
            executor.Arrange(
                Handle,
                WindowLayoutTarget
                    .BottomRightQuarter));
        Assert.Equal(
            new Rectangle(
                -960,
                520,
                960,
                520),
            native.LastSetBounds);
        Assert.Empty(native.ShowCommands);
        Assert.Equal(0, native.ForegroundCalls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Arrange_RestoresIconicOrZoomedWindowAfterBoundsWrite(
        bool iconic,
        bool zoomed)
    {
        var native = new FakeBoundary
        {
            Iconic = iconic,
            Zoomed = zoomed,
            SetBoundsResult = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.True(
            executor.Arrange(
                Handle,
                WindowLayoutTarget.LeftHalf));
        Assert.Equal(
            WindowCommandExecutor.RestoreCommand,
            Assert.Single(native.ShowCommands));
        Assert.False(native.Iconic);
        Assert.False(native.Zoomed);
    }

    [Fact]
    public void Arrange_RejectedBoundsDoNotChangeWindowState()
    {
        var native = new FakeBoundary
        {
            Zoomed = true,
            SetBoundsResult = false
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.False(
            executor.Arrange(
                Handle,
                WindowLayoutTarget.RightHalf));
        Assert.Empty(native.ShowCommands);
        Assert.True(native.Zoomed);
    }

    [Fact]
    public void Arrange_InvalidWorkAreaDoesNotWritePlacement()
    {
        var native = new FakeBoundary
        {
            WorkingArea = Rectangle.Empty,
            SetBoundsResult = true
        };
        var executor =
            new WindowCommandExecutor(native);

        Assert.False(
            executor.Arrange(
                Handle,
                WindowLayoutTarget.LeftHalf));
        Assert.Equal(0, native.SetBoundsCalls);
    }

    private static WindowTaskItem Task(
        IntPtr handle) =>
        new()
        {
            AppKey = "demo",
            IdentityKey = "exe:demo",
            DisplayName = "Demo",
            Windows = new[]
            {
                new WindowReference(
                    handle,
                    "Demo")
            }
        };

    private sealed class FakeBoundary :
        IWindowCommandBoundary
    {
        internal bool WindowExists { get; set; } = true;
        internal IntPtr Foreground { get; set; }
        internal bool Iconic { get; set; }
        internal bool Zoomed { get; set; }
        internal bool BecomeIconicWhenMinimized { get; set; }
        internal bool BecomeZoomedWhenMaximized { get; set; }
        internal bool ForegroundResult { get; set; }
        internal bool BecomeForegroundWhenRequested { get; set; }
        internal bool CloseResult { get; set; } = true;
        internal List<int> ShowCommands { get; } =
            new();
        internal int ForegroundCalls { get; private set; }
        internal int CloseCalls { get; private set; }
        internal Rectangle WorkingArea { get; set; } =
            new(0, 0, 1920, 1040);
        internal Rectangle RestoredBounds { get; set; } =
            new(100, 100, 1200, 800);
        internal bool SetBoundsResult { get; set; }
        internal Rectangle LastSetBounds { get; private set; }
        internal int SetBoundsCalls { get; private set; }
        internal bool SetTopmostResult { get; set; }
        internal List<bool> TopmostRequests { get; } =
            new();

        public bool IsWindow(IntPtr handle) =>
            WindowExists;

        public IntPtr GetForegroundWindow() =>
            Foreground;

        public bool IsIconic(IntPtr handle) =>
            Iconic;

        public bool IsZoomed(IntPtr handle) =>
            Zoomed;

        public void ShowWindow(
            IntPtr handle,
            int command)
        {
            ShowCommands.Add(command);
            if (command
                    == WindowCommandExecutor.MinimizeCommand
                && BecomeIconicWhenMinimized)
            {
                Iconic = true;
                Zoomed = false;
            }
            else if (command
                     == WindowCommandExecutor.MaximizeCommand
                     && BecomeZoomedWhenMaximized)
            {
                Iconic = false;
                Zoomed = true;
            }
            else if (command
                     == WindowCommandExecutor.RestoreCommand)
            {
                Iconic = false;
                Zoomed = false;
            }
        }

        public bool SetForegroundWindow(
            IntPtr handle)
        {
            ForegroundCalls++;
            if (BecomeForegroundWhenRequested)
                Foreground = handle;
            return ForegroundResult;
        }

        public bool PostClose(IntPtr handle)
        {
            CloseCalls++;
            return CloseResult;
        }

        public bool TryGetRestoredBounds(
            IntPtr handle,
            out Rectangle bounds)
        {
            bounds = RestoredBounds;
            return bounds.Width > 0
                && bounds.Height > 0;
        }

        public Rectangle GetWorkingArea(
            IntPtr handle) =>
            WorkingArea;

        public bool SetRestoredBounds(
            IntPtr handle,
            Rectangle bounds)
        {
            SetBoundsCalls++;
            LastSetBounds = bounds;
            return SetBoundsResult;
        }

        public bool SetTopmost(
            IntPtr handle,
            bool isTopmost)
        {
            TopmostRequests.Add(isTopmost);
            return SetTopmostResult;
        }
    }
}
