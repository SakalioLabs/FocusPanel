using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
                new FakePersistence
                {
                    Snapshot =
                        new OkrWorkspaceSnapshot(
                            true,
                            new[] { objective },
                            true,
                            0,
                            new DateTime(
                                2026,
                                7,
                                29),
                            "user-1")
                });

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
                new FakePersistence
                {
                    LoadError =
                        new InvalidOperationException(
                            "database busy")
                });

        OkrWorkspaceSnapshot snapshot =
            repository.Load();

        Assert.False(snapshot.IsValid);
        Assert.Empty(snapshot.Objectives);
        Assert.Equal(30, snapshot.SyncIntervalMinutes);
    }

    [Fact]
    public async Task SaveDraft_CopiesAndReturnsPersistedObjective()
    {
        var persistence = new FakePersistence();
        var repository =
            new OkrWorkspaceRepository(persistence);
        var draft = new OkrObjective
        {
            Name = "AI 草稿"
        };

        OkrObjective saved =
            await repository.SaveDraftAsync(draft);

        Assert.NotSame(draft, saved);
        Assert.Same(saved, persistence.LastDraft);
        Assert.Equal("AI 草稿", saved.Name);
    }

    [Fact]
    public async Task SaveDraft_DoesNotHidePersistenceFailure()
    {
        var repository =
            new OkrWorkspaceRepository(
                new FakePersistence
                {
                    SaveDraftError =
                        new InvalidOperationException(
                            "write failed")
                });

        InvalidOperationException error =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    repository.SaveDraftAsync(
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
                new FakePersistence
                {
                    LoadHandler = () =>
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
                    }
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

    [Fact]
    public async Task Mutation_RunsOffCallingThread()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int callingThread =
            Environment.CurrentManagedThreadId;
        int persistenceThread = callingThread;
        var persistence = new FakePersistence
        {
            UpdateObjectiveHandler = _ =>
            {
                persistenceThread =
                    Environment.CurrentManagedThreadId;
                started.Set();
                release.Wait(TimeSpan.FromSeconds(5));
            }
        };
        var repository =
            new OkrWorkspaceRepository(persistence);

        Task operation =
            repository.UpdateObjectiveAsync(
                ObjectiveWrite(7));

        Assert.True(
            started.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotEqual(
            callingThread,
            persistenceThread);
        Assert.False(operation.IsCompleted);

        release.Set();
        await operation;
    }

    [Fact]
    public async Task Mutations_AreStrictlySerialized()
    {
        using var firstStarted =
            new ManualResetEventSlim();
        using var releaseFirst =
            new ManualResetEventSlim();
        int concurrent = 0;
        int maxConcurrent = 0;
        var persistence = new FakePersistence
        {
            AddObjectiveHandler = _ =>
            {
                int active =
                    Interlocked.Increment(
                        ref concurrent);
                maxConcurrent =
                    Math.Max(maxConcurrent, active);
                firstStarted.Set();
                releaseFirst.Wait(
                    TimeSpan.FromSeconds(5));
                Interlocked.Decrement(
                    ref concurrent);
                return 11;
            },
            UpdateObjectiveHandler = _ =>
            {
                int active =
                    Interlocked.Increment(
                        ref concurrent);
                maxConcurrent =
                    Math.Max(maxConcurrent, active);
                Interlocked.Decrement(
                    ref concurrent);
            }
        };
        var repository =
            new OkrWorkspaceRepository(persistence);

        Task<int> first =
            repository.AddObjectiveAsync(
                ObjectiveWrite(0));
        Assert.True(
            firstStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task second =
            repository.UpdateObjectiveAsync(
                ObjectiveWrite(7));
        await Task.Delay(50);

        Assert.False(second.IsCompleted);
        releaseFirst.Set();
        await Task.WhenAll(first, second);
        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task Load_WaitsForInFlightMutation()
    {
        using var mutationStarted =
            new ManualResetEventSlim();
        using var releaseMutation =
            new ManualResetEventSlim();
        var persistence = new FakePersistence
        {
            Snapshot = new OkrWorkspaceSnapshot(
                true,
                Array.Empty<OkrObjective>(),
                false,
                30,
                null,
                null),
            AddObjectiveHandler = _ =>
            {
                mutationStarted.Set();
                releaseMutation.Wait(
                    TimeSpan.FromSeconds(5));
                return 11;
            }
        };
        var repository =
            new OkrWorkspaceRepository(persistence);

        Task<int> mutation =
            repository.AddObjectiveAsync(
                ObjectiveWrite(0));
        Assert.True(
            mutationStarted.Wait(
                TimeSpan.FromSeconds(2)));
        Task<OkrWorkspaceSnapshot> load =
            Task.Run(repository.Load);
        await Task.Delay(50);

        Assert.False(load.IsCompleted);
        releaseMutation.Set();
        await mutation;
        Assert.True((await load).IsValid);
    }

    private static OkrObjectiveWrite ObjectiveWrite(
        int id) =>
        new(
            id,
            null,
            null,
            "目标",
            null,
            0,
            null,
            1,
            DateTime.Now,
            DateTime.Now,
            OkrSyncStatus.LocalCreated);

    private sealed class FakePersistence
        : IOkrWorkspacePersistence
    {
        internal OkrWorkspaceSnapshot Snapshot { get; set; } =
            OkrWorkspaceSnapshot.Invalid;
        internal Func<OkrWorkspaceSnapshot>? LoadHandler
        {
            get;
            set;
        }
        internal Exception? LoadError { get; set; }
        internal Exception? SaveDraftError { get; set; }
        internal OkrObjective? LastDraft { get; private set; }
        internal Func<OkrObjectiveWrite, int>?
            AddObjectiveHandler { get; set; }
        internal Action<OkrObjectiveWrite>?
            UpdateObjectiveHandler { get; set; }

        public OkrWorkspaceSnapshot Load()
        {
            if (LoadError != null)
                throw LoadError;
            return LoadHandler?.Invoke() ?? Snapshot;
        }

        public int AddObjective(
            OkrObjectiveWrite objective) =>
            AddObjectiveHandler?.Invoke(objective)
            ?? 1;

        public void DeleteObjective(int objectiveId)
        {
        }

        public void UpdateObjective(
            OkrObjectiveWrite objective) =>
            UpdateObjectiveHandler?.Invoke(objective);

        public int AddKeyResult(
            OkrKeyResultWrite keyResult,
            OkrObjectiveAggregateWrite objective) =>
            1;

        public void DeleteKeyResult(
            int keyResultId,
            OkrObjectiveAggregateWrite objective)
        {
        }

        public void UpdateKeyResult(
            OkrKeyResultWrite keyResult,
            OkrObjectiveAggregateWrite objective)
        {
        }

        public OkrObjective SaveDraft(
            OkrObjective objective)
        {
            if (SaveDraftError != null)
                throw SaveDraftError;
            LastDraft = objective;
            return objective;
        }
    }
}
