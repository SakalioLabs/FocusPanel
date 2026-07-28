using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopCreatedPathSuppressionTests
{
    [Fact]
    public void SuppressedPath_IsConsumedExactlyOnce()
    {
        var suppression =
            new DesktopCreatedPathSuppression();
        var now =
            new DateTimeOffset(
                2026,
                7,
                29,
                8,
                0,
                0,
                TimeSpan.Zero);
        suppression.Suppress(
            @"C:\Desktop\Restored.txt",
            now);

        Assert.True(
            suppression.TryConsume(
                @"c:\desktop\RESTORED.txt",
                now.AddSeconds(1)));
        Assert.False(
            suppression.TryConsume(
                @"C:\Desktop\Restored.txt",
                now.AddSeconds(2)));
    }

    [Fact]
    public void ExpiredSuppression_DoesNotHideLaterExternalCreate()
    {
        var suppression =
            new DesktopCreatedPathSuppression();
        var now =
            DateTimeOffset.UtcNow;
        suppression.Suppress(
            @"C:\Desktop\Recovered",
            now,
            TimeSpan.FromSeconds(2));

        Assert.False(
            suppression.TryConsume(
                @"C:\Desktop\Recovered",
                now.AddSeconds(3)));
    }

    [Fact]
    public void InvalidPath_IsNeverSuppressed()
    {
        var suppression =
            new DesktopCreatedPathSuppression();
        var now =
            DateTimeOffset.UtcNow;

        suppression.Suppress(null, now);
        suppression.Suppress("", now);

        Assert.False(
            suppression.TryConsume(null, now));
        Assert.False(
            suppression.TryConsume("", now));
    }
}

