using System;
using System.IO;
using Xunit;

namespace FocusPanel.Tests;

public sealed class WindowTrackerBackgroundRefreshContractTests
{
    [Fact]
    public void WindowEnumeration_UsesCoalescedBackgroundCapture()
    {
        string tracker = ReadWindowTracker();

        Assert.Contains(
            "CoalescingBackgroundRefresh<",
            tracker);
        Assert.Contains(
            "PendingWindowSnapshot>",
            tracker);
        Assert.Contains(
            "CapturePendingSnapshot,",
            tracker);
        Assert.Contains(
            "ApplySnapshotAsync,",
            tracker);
        Assert.Contains(
            "_snapshotRefresh.Request();",
            tracker);
        Assert.Contains(
            "Interlocked.Increment(",
            tracker);
        Assert.DoesNotContain(
            "_snapshotStore.TryRefresh(\n"
            + "                CaptureSnapshot",
            NormalizeNewLines(tracker));
    }

    [Fact]
    public void Snapshot_IsPublishedOnUiDispatcherAndCancelledOnDispose()
    {
        string tracker = ReadWindowTracker();

        Assert.Contains(
            "_uiDispatcher.InvokeAsync(",
            tracker);
        Assert.Contains(
            "DispatcherPriority.Background",
            tracker);
        Assert.Contains(
            "IsCancellationRequested",
            tracker);
        Assert.Contains(
            "WindowSnapshotApplyPolicy.CanApply(",
            tracker);
        Assert.Contains(
            "_snapshotRefresh.Dispose();",
            tracker);
        Assert.Contains(
            "keeping the last valid snapshot",
            tracker);
        Assert.Contains(
            "EventSubscriberIsolation.Publish(",
            tracker);
    }

    [Fact]
    public void WindowVisualStateEvents_UseTheExistingCoalescedRefreshPath()
    {
        string tracker = ReadWindowTracker();

        Assert.Contains(
            "WindowTrackingEventPolicy.EventSystemMinimizeStart,\n"
            + "            WindowTrackingEventPolicy.EventSystemMinimizeEnd",
            NormalizeNewLines(tracker));
        Assert.Contains(
            "WindowTrackingEventPolicy.EventObjectLocationChange,\n"
            + "            WindowTrackingEventPolicy.EventObjectLocationChange",
            NormalizeNewLines(tracker));
        Assert.DoesNotContain(
            "CaptureSnapshot();\n        }\n\n        void RestartDebounce",
            NormalizeNewLines(tracker));
        Assert.Contains(
            "_refreshDebounce.Stop();\n"
            + "            _refreshDebounce.Start();",
            NormalizeNewLines(tracker));
    }

    [Fact]
    public void MainViewModel_SubscribesBeforeReadingInitialSnapshot()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(
            Path.Combine(
                root,
                "ViewModels",
                "MainViewModel.cs"));
        int subscription = viewModel.IndexOf(
            "_windowTracker.SnapshotChanged += OnWindowSnapshotChanged;",
            StringComparison.Ordinal);
        int initialRead = viewModel.IndexOf(
            "RefreshTaskbarApps();",
            subscription,
            StringComparison.Ordinal);

        Assert.True(subscription >= 0);
        Assert.True(initialRead > subscription);
    }

    private static string ReadWindowTracker()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                root,
                "Services",
                "WindowTracker.cs"));
    }

    private static string FindRepositoryRoot() =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                ".."));

    private static string NormalizeNewLines(string value) =>
        value.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
}
