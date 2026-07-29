using System;
using System.Collections.Generic;
using System.Windows.Threading;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SafeDispatcherProgressTests
{
    [Fact]
    public void Report_OnDispatcher_AppliesValue()
    {
        var values = new List<int>();
        var progress =
            new SafeDispatcherProgress<int>(
                Dispatcher.CurrentDispatcher,
                values.Add);

        progress.Report(42);

        Assert.Equal(
            new[] { 42 },
            values);
    }

    [Fact]
    public void HandlerFailure_IsReportedWithoutEscaping()
    {
        Exception? reported = null;
        var progress =
            new SafeDispatcherProgress<int>(
                Dispatcher.CurrentDispatcher,
                _ => throw new InvalidOperationException(
                    "render failed"),
                error => reported = error);

        Exception? escaped =
            Record.Exception(
                () => progress.Report(1));

        Assert.Null(escaped);
        Assert.IsType<
            InvalidOperationException>(reported);
    }

    [Fact]
    public void DiagnosticFailure_DoesNotEscape()
    {
        var progress =
            new SafeDispatcherProgress<int>(
                Dispatcher.CurrentDispatcher,
                _ => throw new InvalidOperationException(),
                _ => throw new InvalidOperationException());

        Exception? escaped =
            Record.Exception(
                () => progress.Report(1));

        Assert.Null(escaped);
    }
}
