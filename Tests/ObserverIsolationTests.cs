using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ObserverIsolationTests
{
    [Fact]
    public void ThrowingObserver_DoesNotBlockRemainingObservers()
    {
        int completed = 0;
        Action observers =
            () => throw new InvalidOperationException(
                "first failed");
        observers += () => completed++;

        Exception? escaped =
            Record.Exception(
                () =>
                    ObserverIsolation.Notify(
                        observers));

        Assert.Null(escaped);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void DiagnosticFailure_DoesNotEscapeOrBlockObservers()
    {
        int completed = 0;
        Action observers =
            () => throw new InvalidOperationException(
                "first failed");
        observers += () => completed++;

        Exception? escaped =
            Record.Exception(
                () =>
                    ObserverIsolation.Notify(
                        observers,
                        _ => throw new InvalidOperationException(
                            "diagnostic failed")));

        Assert.Null(escaped);
        Assert.Equal(1, completed);
    }
}
