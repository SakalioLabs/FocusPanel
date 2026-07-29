using System;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AsyncInteractionRunnerTests
{
    [Fact]
    public async Task SuccessfulOperation_CompletesWithoutFailure()
    {
        bool operated = false;
        bool failed = false;
        bool completed = false;

        await AsyncInteractionRunner.RunAsync(
            () =>
            {
                operated = true;
                return Task.CompletedTask;
            },
            _ => failed = true,
            () => completed = true);

        Assert.True(operated);
        Assert.False(failed);
        Assert.True(completed);
    }

    [Fact]
    public async Task FailedOperation_IsObservedAndStillCompletes()
    {
        Exception? observed = null;
        bool completed = false;
        var failure =
            new InvalidOperationException("drop failed");

        await AsyncInteractionRunner.RunAsync(
            () => Task.FromException(failure),
            error => observed = error,
            () => completed = true);

        Assert.Same(failure, observed);
        Assert.True(completed);
    }

    [Fact]
    public async Task SynchronousFailure_IsObserved()
    {
        Exception? observed = null;

        await AsyncInteractionRunner.RunAsync(
            () => throw new InvalidOperationException(
                "synchronous failure"),
            error => observed = error);

        Assert.IsType<InvalidOperationException>(
            observed);
    }

    [Fact]
    public async Task FeedbackFailure_DoesNotSkipCleanupOrEscape()
    {
        bool completed = false;

        await AsyncInteractionRunner.RunAsync(
            () => Task.FromException(
                new InvalidOperationException(
                    "operation failed")),
            _ => throw new InvalidOperationException(
                "feedback failed"),
            () => completed = true);

        Assert.True(completed);
    }

    [Fact]
    public async Task CleanupFailure_DoesNotEscapeUiBoundary()
    {
        await AsyncInteractionRunner.RunAsync(
            () => Task.CompletedTask,
            onCompleted: () =>
                throw new InvalidOperationException(
                    "cleanup failed"));
    }
}
