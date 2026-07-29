using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

internal interface IDwmThumbnailApi
{
    int Register(
        IntPtr destination,
        IntPtr source,
        out IntPtr thumbnail);

    int QuerySourceSize(
        IntPtr thumbnail,
        out DwmThumbnailSize size);

    int Update(
        IntPtr thumbnail,
        ref DwmThumbnailProperties properties);

    int Unregister(
        IntPtr thumbnail);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DwmThumbnailSize
{
    internal DwmThumbnailSize(
        int width,
        int height)
    {
        Width = width;
        Height = height;
    }

    internal readonly int Width;
    internal readonly int Height;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DwmThumbnailRect
{
    internal DwmThumbnailRect(
        int left,
        int top,
        int right,
        int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    internal readonly int Left;
    internal readonly int Top;
    internal readonly int Right;
    internal readonly int Bottom;
    internal int Width => Right - Left;
    internal int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DwmThumbnailProperties
{
    internal uint Flags;
    internal DwmThumbnailRect Destination;
    internal DwmThumbnailRect Source;
    internal byte Opacity;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool Visible;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool SourceClientAreaOnly;
}

internal static class DwmThumbnailLayout
{
    internal static int GetVisiblePreviewCount(
        int totalWindowCount,
        double availableHeightDip)
    {
        if (totalWindowCount <= 0
            || !double.IsFinite(
                availableHeightDip)
            || availableHeightDip <= 0)
        {
            return 0;
        }

        const double reservedHeightDip = 100;
        const double cardHeightDip = 219;
        int capacity = Math.Max(
            1,
            (int)Math.Floor(
                Math.Max(
                    0,
                    availableHeightDip
                    - reservedHeightDip)
                / cardHeightDip));
        return Math.Min(
            totalWindowCount,
            Math.Min(
                4,
                capacity));
    }

    internal static DwmThumbnailRect Fit(
        DwmThumbnailSize source,
        DwmThumbnailRect available)
    {
        if (source.Width <= 0
            || source.Height <= 0
            || available.Width <= 0
            || available.Height <= 0)
        {
            return default;
        }

        double scale = Math.Min(
            available.Width
                / (double)source.Width,
            available.Height
                / (double)source.Height);
        int width = Math.Max(
            1,
            (int)Math.Round(
                source.Width * scale));
        int height = Math.Max(
            1,
            (int)Math.Round(
                source.Height * scale));
        int left =
            available.Left
            + (available.Width - width) / 2;
        int top =
            available.Top
            + (available.Height - height) / 2;
        return new DwmThumbnailRect(
            left,
            top,
            left + width,
            top + height);
    }
}

internal sealed class DwmThumbnailSession :
    IDisposable
{
    private const uint DestinationFlag = 0x00000001;
    private const uint OpacityFlag = 0x00000004;
    private const uint VisibleFlag = 0x00000008;
    private const uint ClientAreaFlag = 0x00000010;

    private readonly IDwmThumbnailApi _api;
    private readonly List<IntPtr> _thumbnails =
        new();
    private bool _disposed;

    internal DwmThumbnailSession()
        : this(new WindowsDwmThumbnailApi())
    {
    }

    internal DwmThumbnailSession(
        IDwmThumbnailApi api)
    {
        _api = api;
    }

    internal bool TryAdd(
        IntPtr destination,
        IntPtr source,
        DwmThumbnailRect available)
    {
        if (_disposed
            || destination == IntPtr.Zero
            || source == IntPtr.Zero
            || available.Width <= 0
            || available.Height <= 0)
        {
            return false;
        }

        if (_api.Register(
                destination,
                source,
                out IntPtr thumbnail)
                < 0
            || thumbnail == IntPtr.Zero)
        {
            return false;
        }

        bool registered = false;
        try
        {
            if (_api.QuerySourceSize(
                    thumbnail,
                    out DwmThumbnailSize sourceSize)
                    < 0)
            {
                return false;
            }

            DwmThumbnailRect destinationRect =
                DwmThumbnailLayout.Fit(
                    sourceSize,
                    available);
            if (destinationRect.Width <= 0
                || destinationRect.Height <= 0)
            {
                return false;
            }

            var properties =
                new DwmThumbnailProperties
                {
                    Flags =
                        DestinationFlag
                        | OpacityFlag
                        | VisibleFlag
                        | ClientAreaFlag,
                    Destination =
                        destinationRect,
                    Opacity = byte.MaxValue,
                    Visible = true,
                    SourceClientAreaOnly = false
                };
            if (_api.Update(
                    thumbnail,
                    ref properties)
                    < 0)
            {
                return false;
            }

            _thumbnails.Add(thumbnail);
            registered = true;
            return true;
        }
        finally
        {
            if (!registered)
                _api.Unregister(thumbnail);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (IntPtr thumbnail
                 in _thumbnails)
        {
            _api.Unregister(thumbnail);
        }

        _thumbnails.Clear();
    }

    private sealed class WindowsDwmThumbnailApi :
        IDwmThumbnailApi
    {
        public int Register(
            IntPtr destination,
            IntPtr source,
            out IntPtr thumbnail) =>
            NativeMethods.DwmRegisterThumbnail(
                destination,
                source,
                out thumbnail);

        public int QuerySourceSize(
            IntPtr thumbnail,
            out DwmThumbnailSize size) =>
            NativeMethods.DwmQueryThumbnailSourceSize(
                thumbnail,
                out size);

        public int Update(
            IntPtr thumbnail,
            ref DwmThumbnailProperties properties) =>
            NativeMethods.DwmUpdateThumbnailProperties(
                thumbnail,
                ref properties);

        public int Unregister(
            IntPtr thumbnail) =>
            NativeMethods.DwmUnregisterThumbnail(
                thumbnail);
    }

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        internal static extern int
            DwmRegisterThumbnail(
                IntPtr destination,
                IntPtr source,
                out IntPtr thumbnail);

        [DllImport("dwmapi.dll")]
        internal static extern int
            DwmQueryThumbnailSourceSize(
                IntPtr thumbnail,
                out DwmThumbnailSize size);

        [DllImport("dwmapi.dll")]
        internal static extern int
            DwmUpdateThumbnailProperties(
                IntPtr thumbnail,
                ref DwmThumbnailProperties
                    properties);

        [DllImport("dwmapi.dll")]
        internal static extern int
            DwmUnregisterThumbnail(
                IntPtr thumbnail);
    }
}
