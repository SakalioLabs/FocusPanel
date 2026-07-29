using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class EventSubscriberIsolationTests
{
    [Fact]
    public void FailingSubscriber_DoesNotBlockLaterSubscriber()
    {
        int called = 0;
        EventHandler handlers =
            (_, _) => throw new InvalidOperationException(
                "subscriber failed");
        handlers += (_, _) => called++;

        int failures =
            EventSubscriberIsolation.Publish(
                handlers,
                this);

        Assert.Equal(1, failures);
        Assert.Equal(1, called);
    }

    [Fact]
    public void DiagnosticFailure_DoesNotEscapeOrBlockSubscribers()
    {
        int called = 0;
        EventHandler handlers =
            (_, _) => throw new InvalidOperationException(
                "subscriber failed");
        handlers += (_, _) => called++;

        int failures =
            EventSubscriberIsolation.Publish(
                handlers,
                this,
                _ => throw new InvalidOperationException(
                    "diagnostic failed"));

        Assert.Equal(1, failures);
        Assert.Equal(1, called);
    }

    [Fact]
    public void NoSubscribers_IsAValidNoOp()
    {
        Assert.Equal(
            0,
            EventSubscriberIsolation.Publish(
                null,
                this));
    }
}
