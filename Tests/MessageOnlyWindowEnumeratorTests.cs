using System;
using System.Collections.Generic;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class MessageOnlyWindowEnumeratorTests
{
    [Fact]
    public void Enumerate_WalksUntilNativeBoundaryReturnsZero()
    {
        var sequence = new Queue<IntPtr>(new[]
        {
            new IntPtr(11),
            new IntPtr(12),
            IntPtr.Zero
        });

        IReadOnlyList<IntPtr> result =
            MessageOnlyWindowEnumerator.Enumerate(
                _ => sequence.Dequeue());

        Assert.Equal(
            new[] { new IntPtr(11), new IntPtr(12) },
            result);
    }

    [Fact]
    public void Enumerate_PassesPreviousWindowToNextLookup()
    {
        var previousValues = new List<IntPtr>();

        MessageOnlyWindowEnumerator.Enumerate(
            previous =>
            {
                previousValues.Add(previous);
                return previous == IntPtr.Zero
                    ? new IntPtr(21)
                    : IntPtr.Zero;
            });

        Assert.Equal(
            new[] { IntPtr.Zero, new IntPtr(21) },
            previousValues);
    }

    [Fact]
    public void Enumerate_RepeatedHandleStopsWithoutLooping()
    {
        int calls = 0;

        IReadOnlyList<IntPtr> result =
            MessageOnlyWindowEnumerator.Enumerate(
                _ =>
                {
                    calls++;
                    return new IntPtr(31);
                });

        Assert.Single(result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Enumerate_RespectsSafetyLimit()
    {
        int next = 40;

        IReadOnlyList<IntPtr> result =
            MessageOnlyWindowEnumerator.Enumerate(
                _ => new IntPtr(++next),
                limit: 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Enumerate_NonPositiveLimitDoesNotCallNativeBoundary()
    {
        int calls = 0;

        IReadOnlyList<IntPtr> result =
            MessageOnlyWindowEnumerator.Enumerate(
                _ =>
                {
                    calls++;
                    return new IntPtr(1);
                },
                limit: 0);

        Assert.Empty(result);
        Assert.Equal(0, calls);
    }
}
