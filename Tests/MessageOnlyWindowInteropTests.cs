using System;
using System.Runtime.InteropServices;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class MessageOnlyWindowInteropTests
{
    private static readonly IntPtr HwndMessage = new(-3);

    [Fact]
    public void PublicFindWindowEx_EnumeratesRealMessageOnlyWindow()
    {
        IntPtr window = NativeMethods.CreateWindowEx(
            0,
            "STATIC",
            "FocusPanel message-only test",
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, window);

        try
        {
            var discovered =
                MessageOnlyWindowEnumerator.Enumerate(
                    previous =>
                        NativeMethods.FindWindowEx(
                            HwndMessage,
                            previous,
                            null,
                            null));

            Assert.Contains(window, discovered);
        }
        finally
        {
            Assert.True(
                NativeMethods.DestroyWindow(window));
        }
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr FindWindowEx(
            IntPtr parent,
            IntPtr childAfter,
            string? className,
            string? windowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(
            IntPtr window);
    }
}
