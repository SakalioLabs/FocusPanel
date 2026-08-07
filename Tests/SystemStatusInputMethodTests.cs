using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemStatusInputMethodTests
{
    [Fact]
    public void WindowsBoundary_EnumeratesBlittableLayoutHandles()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var native =
            new WindowsInputMethodNative();

        IReadOnlyList<IntPtr> layouts =
            native.GetKeyboardLayouts();

        Assert.All(
            layouts,
            layout => Assert.NotEqual(
                IntPtr.Zero,
                layout));
    }

    [Fact]
    public void InstalledMethods_ExposeActiveForegroundLayout()
    {
        var native = new FakeInputMethodNative
        {
            Foreground = new IntPtr(77),
            ForegroundLayout =
                new IntPtr(0x00000409)
        };
        native.Layouts.Add(
            new IntPtr(0x00000409));
        native.Layouts.Add(
            new IntPtr(0x00000804));
        native.Descriptions[
            new IntPtr(0x00000804)] =
                "微软拼音";
        using var service =
            new SystemStatusService(
                _ => true,
                native);

        InputMethodOption[] methods =
            service.GetInputMethods()
                .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.Equal("EN", methods[0].ShortLabel);
        Assert.True(methods[0].IsActive);
        Assert.Equal("微软拼音", methods[1].DisplayName);
        Assert.False(methods[1].IsActive);
    }

    [Fact]
    public void Activate_PostsToRememberedAppAndCurrentPanel()
    {
        var native = new FakeInputMethodNative
        {
            Foreground = new IntPtr(90),
            ForegroundLayout =
                new IntPtr(0x00000409)
        };
        native.Layouts.Add(
            new IntPtr(0x00000409));
        native.Layouts.Add(
            new IntPtr(0x00000804));
        using var service =
            new SystemStatusService(
                _ => true,
                native);
        var option = new InputMethodOption(
            0x00000804,
            "微软拼音",
            "中文",
            "拼",
            false);

        bool succeeded =
            service.TryActivateInputMethod(
                option,
                new IntPtr(42));

        Assert.True(succeeded);
        Assert.Equal(
            new[]
            {
                (new IntPtr(42),
                    new IntPtr(0x00000804)),
                (new IntPtr(90),
                    new IntPtr(0x00000804))
            },
            native.Requests);
    }

    [Fact]
    public void Activate_RejectsStaleOrForgedLayout()
    {
        var native = new FakeInputMethodNative
        {
            Foreground = new IntPtr(90)
        };
        native.Layouts.Add(
            new IntPtr(0x00000409));
        using var service =
            new SystemStatusService(
                _ => true,
                native);

        bool succeeded =
            service.TryActivateInputMethod(
                new InputMethodOption(
                    999,
                    "未知",
                    string.Empty,
                    "—",
                    false),
                new IntPtr(42));

        Assert.False(succeeded);
        Assert.Empty(native.Requests);
    }

    [Fact]
    public void Activate_ReportsTargetApplicationRejection()
    {
        var native = new FakeInputMethodNative
        {
            Foreground = new IntPtr(90),
            RejectedWindow = new IntPtr(42)
        };
        native.Layouts.Add(
            new IntPtr(0x00000409));
        using var service =
            new SystemStatusService(
                _ => true,
                native);

        bool succeeded =
            service.TryActivateInputMethod(
                new InputMethodOption(
                    0x00000409,
                    "English",
                    "English",
                    "EN",
                    false),
                new IntPtr(42));

        Assert.False(succeeded);
        Assert.Single(native.Requests);
    }

    [Fact]
    public void Activate_DoesNotTreatQueuedButUnappliedRequestAsSuccess()
    {
        var native = new FakeInputMethodNative
        {
            Foreground = new IntPtr(90),
            ForegroundLayout =
                new IntPtr(0x00000409),
            IgnoreRequestedLayout = true
        };
        native.Layouts.Add(
            new IntPtr(0x00000409));
        native.Layouts.Add(
            new IntPtr(0x00000804));
        using var service =
            new SystemStatusService(
                _ => true,
                native);

        bool succeeded =
            service.TryActivateInputMethod(
                new InputMethodOption(
                    0x00000804,
                    "微软拼音",
                    "中文",
                    "中 / 拼",
                    false),
                new IntPtr(42));

        Assert.False(succeeded);
        Assert.Equal(2, native.Requests.Count);
    }

    private sealed class FakeInputMethodNative :
        IInputMethodNative
    {
        internal List<IntPtr> Layouts { get; } = new();
        internal Dictionary<IntPtr, string>
            Descriptions
        {
            get;
        } = new();
        internal Dictionary<IntPtr, IntPtr>
            WindowLayouts
        {
            get;
        } = new();
        internal List<(IntPtr Window, IntPtr Layout)>
            Requests
        {
            get;
        } = new();
        internal IntPtr Foreground { get; init; }
        internal IntPtr ForegroundLayout { get; init; }
        internal IntPtr RejectedWindow { get; init; }
        internal bool IgnoreRequestedLayout { get; init; }

        public IReadOnlyList<IntPtr>
            GetKeyboardLayouts() => Layouts;

        public IntPtr GetForegroundWindow() =>
            Foreground;

        public IntPtr GetKeyboardLayoutForWindow(
            IntPtr window) =>
            WindowLayouts.TryGetValue(
                window,
                out IntPtr layout)
                ? layout
                : ForegroundLayout;

        public string GetDescription(
            IntPtr keyboardLayout) =>
            Descriptions.TryGetValue(
                keyboardLayout,
                out string? description)
                ? description
                : string.Empty;

        public bool TryRequestInputLanguage(
            IntPtr window,
            IntPtr keyboardLayout)
        {
            Requests.Add((window, keyboardLayout));
            if (window == RejectedWindow)
                return false;

            if (!IgnoreRequestedLayout)
            {
                WindowLayouts[window] =
                    keyboardLayout;
            }
            return true;
        }
    }
}
