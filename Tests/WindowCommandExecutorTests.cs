using System;
using System.Collections.Generic;
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
        Assert.False(
            executor.ActivateOrMinimize(
                Task(Handle)));
        Assert.Empty(native.ShowCommands);
        Assert.Equal(0, native.ForegroundCalls);
        Assert.Equal(0, native.CloseCalls);
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
        internal bool BecomeIconicWhenMinimized { get; set; }
        internal bool ForegroundResult { get; set; }
        internal bool BecomeForegroundWhenRequested { get; set; }
        internal bool CloseResult { get; set; } = true;
        internal List<int> ShowCommands { get; } =
            new();
        internal int ForegroundCalls { get; private set; }
        internal int CloseCalls { get; private set; }

        public bool IsWindow(IntPtr handle) =>
            WindowExists;

        public IntPtr GetForegroundWindow() =>
            Foreground;

        public bool IsIconic(IntPtr handle) =>
            Iconic;

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
            }
            else if (command
                     == WindowCommandExecutor.RestoreCommand)
            {
                Iconic = false;
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
    }
}
