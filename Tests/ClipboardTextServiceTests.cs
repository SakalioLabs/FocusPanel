using System;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class ClipboardTextServiceTests
{
    [Fact]
    public async Task TrySetTextAsync_SucceedsWithoutDelay()
    {
        string? copied = null;
        int delayCount = 0;
        var service =
            new ClipboardTextService(
                text => copied = text,
                _ =>
                {
                    delayCount++;
                    return Task.CompletedTask;
                });

        bool succeeded =
            await service
                .TrySetTextAsync("42");

        Assert.True(succeeded);
        Assert.Equal("42", copied);
        Assert.Equal(0, delayCount);
    }

    [Fact]
    public async Task TrySetTextAsync_RetriesTransientFailure()
    {
        int attempts = 0;
        int delays = 0;
        var service =
            new ClipboardTextService(
                _ =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new
                            InvalidOperationException();
                    }
                },
                _ =>
                {
                    delays++;
                    return Task.CompletedTask;
                });

        bool succeeded =
            await service
                .TrySetTextAsync("结果");

        Assert.True(succeeded);
        Assert.Equal(3, attempts);
        Assert.Equal(2, delays);
    }

    [Fact]
    public async Task TrySetTextAsync_ContainsPersistentFailure()
    {
        int attempts = 0;
        var service =
            new ClipboardTextService(
                _ =>
                {
                    attempts++;
                    throw new
                        InvalidOperationException();
                },
                _ => Task.CompletedTask);

        bool succeeded =
            await service
                .TrySetTextAsync("结果");

        Assert.False(succeeded);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task TrySetTextAsync_RejectsEmptyTextWithoutCallingClipboard()
    {
        int attempts = 0;
        var service =
            new ClipboardTextService(
                _ => attempts++,
                _ => Task.CompletedTask);

        Assert.False(
            await service
                .TrySetTextAsync(
                    string.Empty));
        Assert.Equal(0, attempts);
    }
}
