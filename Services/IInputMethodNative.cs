using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusPanel.Services;

internal interface IInputMethodNative
{
    IReadOnlyList<IntPtr>
        GetKeyboardLayouts();

    IntPtr GetForegroundWindow();

    IntPtr GetKeyboardLayoutForWindow(
        IntPtr window);

    string GetDescription(
        IntPtr keyboardLayout);

    bool TryRequestInputLanguage(
        IntPtr window,
        IntPtr keyboardLayout);
}

internal sealed class WindowsInputMethodNative :
    IInputMethodNative
{
    private const int
        WmInputLanguageChangeRequest =
            0x0050;

    public IReadOnlyList<IntPtr>
        GetKeyboardLayouts()
    {
        int required = NativeMethods
            .GetKeyboardLayoutList(
                0,
                null);
        if (required <= 0)
            return Array.Empty<IntPtr>();

        var layouts = new IntPtr[required];
        int copied = NativeMethods
            .GetKeyboardLayoutList(
                layouts.Length,
                layouts);
        if (copied <= 0)
            return Array.Empty<IntPtr>();
        if (copied == layouts.Length)
            return layouts;

        Array.Resize(
            ref layouts,
            copied);
        return layouts;
    }

    public IntPtr GetForegroundWindow() =>
        NativeMethods.GetForegroundWindow();

    public IntPtr GetKeyboardLayoutForWindow(
        IntPtr window)
    {
        uint threadId = window == IntPtr.Zero
            ? 0
            : NativeMethods
                .GetWindowThreadProcessId(
                    window,
                    out _);
        return NativeMethods.GetKeyboardLayout(
            threadId);
    }

    public string GetDescription(
        IntPtr keyboardLayout)
    {
        var buffer = new StringBuilder(128);
        _ = NativeMethods.ImmGetDescription(
            keyboardLayout,
            buffer,
            (uint)buffer.Capacity);
        return buffer.ToString();
    }

    public bool TryRequestInputLanguage(
        IntPtr window,
        IntPtr keyboardLayout) =>
        window != IntPtr.Zero
        && keyboardLayout != IntPtr.Zero
        && NativeMethods.PostMessage(
            window,
            WmInputLanguageChangeRequest,
            IntPtr.Zero,
            keyboardLayout);

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            SetLastError = true)]
        internal static extern int
            GetKeyboardLayoutList(
                int bufferCount,
                [Out] IntPtr[]? layouts);

        [DllImport("user32.dll")]
        internal static extern IntPtr
            GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint
            GetWindowThreadProcessId(
                IntPtr window,
                out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr
            GetKeyboardLayout(
                uint threadId);

        [DllImport(
            "imm32.dll",
            CharSet = CharSet.Unicode)]
        internal static extern uint
            ImmGetDescription(
                IntPtr keyboardLayout,
                StringBuilder description,
                uint bufferLength);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        internal static extern bool
            PostMessage(
                IntPtr window,
                int message,
                IntPtr wParam,
                IntPtr lParam);
    }
}
