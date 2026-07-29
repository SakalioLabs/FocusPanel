using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DwmThumbnailSessionTests
{
    [Fact]
    public void NativeStructs_HaveDocumentedBlittableLayout()
    {
        Assert.Equal(
            8,
            Marshal.SizeOf<DwmThumbnailSize>());
        Assert.Equal(
            16,
            Marshal.SizeOf<DwmThumbnailRect>());
        Assert.Equal(
            48,
            Marshal.SizeOf<DwmThumbnailProperties>());
    }

    [Theory]
    [InlineData(8, 1080, 4)]
    [InlineData(8, 768, 3)]
    [InlineData(8, 600, 2)]
    [InlineData(8, 420, 1)]
    [InlineData(2, 1080, 2)]
    [InlineData(0, 1080, 0)]
    [InlineData(8, double.NaN, 0)]
    public void VisiblePreviewCount_AdaptsToTargetHeight(
        int totalWindowCount,
        double availableHeightDip,
        int expected)
    {
        Assert.Equal(
            expected,
            DwmThumbnailLayout
                .GetVisiblePreviewCount(
                    totalWindowCount,
                    availableHeightDip));
    }

    [Fact]
    public void Fit_WideSourceUsesFullWidthAndCentersVertically()
    {
        DwmThumbnailRect result =
            DwmThumbnailLayout.Fit(
                new DwmThumbnailSize(
                    1920,
                    1080),
                new DwmThumbnailRect(
                    10,
                    20,
                    310,
                    220));

        Assert.Equal(10, result.Left);
        Assert.Equal(310, result.Right);
        Assert.Equal(35, result.Top);
        Assert.Equal(204, result.Bottom);
    }

    [Fact]
    public void Fit_TallSourceUsesFullHeightAndCentersHorizontally()
    {
        DwmThumbnailRect result =
            DwmThumbnailLayout.Fit(
                new DwmThumbnailSize(
                    900,
                    1600),
                new DwmThumbnailRect(
                    0,
                    0,
                    300,
                    180));

        Assert.Equal(99, result.Left);
        Assert.Equal(200, result.Right);
        Assert.Equal(0, result.Top);
        Assert.Equal(180, result.Bottom);
    }

    [Theory]
    [InlineData(0, 1080, 300, 180)]
    [InlineData(1920, 0, 300, 180)]
    [InlineData(1920, 1080, 0, 180)]
    [InlineData(1920, 1080, 300, 0)]
    public void Fit_InvalidMetricsReturnsEmpty(
        int sourceWidth,
        int sourceHeight,
        int availableWidth,
        int availableHeight)
    {
        DwmThumbnailRect result =
            DwmThumbnailLayout.Fit(
                new DwmThumbnailSize(
                    sourceWidth,
                    sourceHeight),
                new DwmThumbnailRect(
                    0,
                    0,
                    availableWidth,
                    availableHeight));

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    [Fact]
    public void SuccessfulRegistration_IsReleasedExactlyOnce()
    {
        var api = new FakeDwmThumbnailApi();
        var session =
            new DwmThumbnailSession(api);

        Assert.True(
            session.TryAdd(
                new IntPtr(1),
                new IntPtr(2),
                new DwmThumbnailRect(
                    0,
                    0,
                    300,
                    180)));

        session.Dispose();
        session.Dispose();

        Assert.Single(api.Unregistered);
        Assert.Equal(
            new IntPtr(10),
            api.Unregistered[0]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NativeFailure_UnregistersPartialThumbnail(
        bool queryFails,
        bool updateFails)
    {
        var api =
            new FakeDwmThumbnailApi
            {
                QueryFails = queryFails,
                UpdateFails = updateFails
            };
        using var session =
            new DwmThumbnailSession(api);

        Assert.False(
            session.TryAdd(
                new IntPtr(1),
                new IntPtr(2),
                new DwmThumbnailRect(
                    0,
                    0,
                    300,
                    180)));
        Assert.Single(api.Unregistered);
    }

    [Fact]
    public void InvalidHandles_DoNotCallNativeBoundary()
    {
        var api = new FakeDwmThumbnailApi();
        using var session =
            new DwmThumbnailSession(api);

        Assert.False(
            session.TryAdd(
                IntPtr.Zero,
                new IntPtr(2),
                new DwmThumbnailRect(
                    0,
                    0,
                    300,
                    180)));
        Assert.Equal(0, api.RegisterCount);
    }

    private sealed class FakeDwmThumbnailApi :
        IDwmThumbnailApi
    {
        public bool QueryFails { get; init; }
        public bool UpdateFails { get; init; }
        public int RegisterCount { get; private set; }
        public List<IntPtr> Unregistered { get; } =
            new();

        public int Register(
            IntPtr destination,
            IntPtr source,
            out IntPtr thumbnail)
        {
            RegisterCount++;
            thumbnail = new IntPtr(10);
            return 0;
        }

        public int QuerySourceSize(
            IntPtr thumbnail,
            out DwmThumbnailSize size)
        {
            size = new DwmThumbnailSize(
                1920,
                1080);
            return QueryFails ? -1 : 0;
        }

        public int Update(
            IntPtr thumbnail,
            ref DwmThumbnailProperties properties) =>
            UpdateFails ? -1 : 0;

        public int Unregister(
            IntPtr thumbnail)
        {
            Unregistered.Add(thumbnail);
            return 0;
        }
    }
}
