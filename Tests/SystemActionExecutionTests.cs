using System;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SystemActionExecutionTests
{
    [Fact]
    public void Try_ReturnsNativeResult()
    {
        Assert.True(SystemActionExecution.Try(() => true));
        Assert.False(SystemActionExecution.Try(() => false));
    }

    [Fact]
    public void Try_ConvertsNativeExceptionToFailure()
    {
        Assert.False(
            SystemActionExecution.Try(
                () => throw new InvalidOperationException("native failure")));
    }

    [Fact]
    public void TryStart_ReportsCompletionAndContainsExceptions()
    {
        bool invoked = false;

        Assert.True(
            SystemActionExecution.TryStart(() => invoked = true));
        Assert.True(invoked);
        Assert.False(
            SystemActionExecution.TryStart(
                () => throw new InvalidOperationException("launch failure")));
    }

    [Fact]
    public void Fallback_RunsOnlyWhenPrimaryFails()
    {
        int fallbackCalls = 0;

        Assert.True(
            SystemActionExecution.TryWithFallback(
                () => true,
                () =>
                {
                    fallbackCalls++;
                    return true;
                }));
        Assert.Equal(0, fallbackCalls);

        Assert.True(
            SystemActionExecution.TryWithFallback(
                () => false,
                () =>
                {
                    fallbackCalls++;
                    return true;
                }));
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public void Fallback_ContainsBothFailures()
    {
        Assert.False(
            SystemActionExecution.TryWithFallback(
                () => throw new InvalidOperationException("primary"),
                () => throw new InvalidOperationException("fallback")));
    }
}
