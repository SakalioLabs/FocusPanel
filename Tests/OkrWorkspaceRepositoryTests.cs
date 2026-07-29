using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using FocusPanel.Models;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OkrWorkspaceRepositoryTests
{
    [Fact]
    public void Load_NormalizesInvalidInterval()
    {
        var objective = new OkrObjective
        {
            Id = 7,
            Name = "稳定 Panel"
        };
        var repository =
            new OkrWorkspaceRepository(
                () =>
                    new OkrWorkspaceSnapshot(
                        true,
                        new[] { objective },
                        true,
                        0,
                        new DateTime(
                            2026,
                            7,
                            29),
                        "user-1"));

        OkrWorkspaceSnapshot snapshot =
            repository.Load();

        Assert.True(snapshot.IsValid);
        Assert.Equal(30, snapshot.SyncIntervalMinutes);
        Assert.True(snapshot.IsConfigured);
        Assert.Same(
            objective,
            Assert.Single(snapshot.Objectives));
        Assert.Equal("user-1", snapshot.UserId);
    }

    [Fact]
    public void Load_FailureReturnsInvalidSnapshot()
    {
        var repository =
            new OkrWorkspaceRepository(
                () =>
                    throw new InvalidOperationException(
                        "database busy"));

        OkrWorkspaceSnapshot snapshot =
            repository.Load();

        Assert.False(snapshot.IsValid);
        Assert.Empty(snapshot.Objectives);
        Assert.Equal(30, snapshot.SyncIntervalMinutes);
    }

    [Fact]
    public void SaveDraft_ForwardsTheExactObjective()
    {
        OkrObjective? saved = null;
        var repository =
            new OkrWorkspaceRepository(
                () =>
                    OkrWorkspaceSnapshot.Invalid,
                objective => saved = objective);
        var draft = new OkrObjective
        {
            Name = "AI 草稿"
        };

        repository.SaveDraft(draft);

        Assert.Same(draft, saved);
    }

    [Fact]
    public void SaveDraft_DoesNotHidePersistenceFailure()
    {
        var repository =
            new OkrWorkspaceRepository(
                () =>
                    OkrWorkspaceSnapshot.Invalid,
                _ =>
                    throw new InvalidOperationException(
                        "write failed"));

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    repository.SaveDraft(
                        new OkrObjective()));

        Assert.Equal("write failed", error.Message);
    }

    [Fact]
    public void ViewModelConstructor_DoesNotWaitForWorkspaceLoad()
    {
        using var loadStarted =
            new ManualResetEventSlim();
        using var releaseLoad =
            new ManualResetEventSlim();
        var repository =
            new OkrWorkspaceRepository(
                () =>
                {
                    loadStarted.Set();
                    releaseLoad.Wait(
                        TimeSpan.FromSeconds(5));
                    return new OkrWorkspaceSnapshot(
                        true,
                        Array.Empty<OkrObjective>(),
                        false,
                        30,
                        null,
                        null);
                });
        var stopwatch = Stopwatch.StartNew();
        var viewModel =
            new OkrViewModel(
                new OkrSyncService(),
                repository,
                Dispatcher.CurrentDispatcher);
        stopwatch.Stop();

        try
        {
            Assert.True(
                stopwatch.Elapsed
                < TimeSpan.FromSeconds(1));
            Assert.True(
                loadStarted.Wait(
                    TimeSpan.FromSeconds(2)));
            Assert.True(viewModel.IsLoading);
        }
        finally
        {
            viewModel.Dispose();
            releaseLoad.Set();
        }
    }
}
