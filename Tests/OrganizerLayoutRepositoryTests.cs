using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OrganizerLayoutRepositoryTests
{
    private static readonly OrganizerLegacyLayout Legacy =
        new(
            new OrganizerLayoutOptions(
                1,
                false,
                true,
                false),
            Array.Empty<string>(),
            new Dictionary<string, string>());

    [Fact]
    public void Load_NormalizesIconScale()
    {
        var repository =
            new OrganizerLayoutRepository(
                _ =>
                    new OrganizerLayoutSnapshot(
                        true,
                        new OrganizerLayoutOptions(
                            9,
                            true,
                            false,
                            true),
                        Array.Empty<
                            OrganizerPartitionSnapshot>(),
                        Array.Empty<
                            OrganizerFilePreferenceSnapshot>()));

        OrganizerLayoutSnapshot snapshot =
            repository.Load(Legacy);

        Assert.True(snapshot.IsValid);
        Assert.Equal(2, snapshot.Options.IconScale);
        Assert.True(snapshot.Options.IsListView);
        Assert.False(
            snapshot.Options.IsPersonalizedView);
        Assert.True(
            snapshot.Options.IsAutoOrganizeEnabled);
    }

    [Fact]
    public void Load_FailureReturnsInvalidSnapshot()
    {
        var repository =
            new OrganizerLayoutRepository(
                _ =>
                    throw new InvalidOperationException(
                        "database busy"));

        OrganizerLayoutSnapshot snapshot =
            repository.Load(Legacy);

        Assert.False(snapshot.IsValid);
        Assert.Empty(snapshot.Partitions);
        Assert.Empty(snapshot.Preferences);
    }

    [Fact]
    public void SaveOptions_ForwardsNormalizedSnapshot()
    {
        OrganizerLayoutOptions? saved = null;
        var repository =
            new OrganizerLayoutRepository(
                _ => OrganizerLayoutSnapshot.Invalid,
                options => saved = options);

        repository.SaveOptions(
            new OrganizerLayoutOptions(
                0,
                true,
                false,
                true));

        Assert.NotNull(saved);
        Assert.Equal(1, saved!.IconScale);
        Assert.True(saved.IsListView);
        Assert.False(saved.IsPersonalizedView);
        Assert.True(saved.IsAutoOrganizeEnabled);
    }

    [Fact]
    public async Task LoadAndSaveOptions_DoNotOverlap()
    {
        using var loadEntered =
            new ManualResetEventSlim();
        using var releaseLoad =
            new ManualResetEventSlim();
        using var saveEntered =
            new ManualResetEventSlim();
        var repository =
            new OrganizerLayoutRepository(
                _ =>
                {
                    loadEntered.Set();
                    releaseLoad.Wait(
                        TimeSpan.FromSeconds(5));
                    return OrganizerLayoutSnapshot.Invalid;
                },
                _ => saveEntered.Set());

        Task<OrganizerLayoutSnapshot> load =
            Task.Run(() => repository.Load(Legacy));
        Assert.True(
            loadEntered.Wait(
                TimeSpan.FromSeconds(2)));
        Task save =
            Task.Run(
                () =>
                    repository.SaveOptions(
                        Legacy.FallbackOptions));

        Assert.False(
            saveEntered.Wait(
                TimeSpan.FromMilliseconds(100)));
        releaseLoad.Set();
        await Task.WhenAll(load, save);
        Assert.True(saveEntered.IsSet);
    }

    [Fact]
    public async Task LoadAndPartitionMutation_DoNotOverlap()
    {
        using var loadEntered =
            new ManualResetEventSlim();
        using var releaseLoad =
            new ManualResetEventSlim();
        using var mutationEntered =
            new ManualResetEventSlim();
        var handlers =
            new OrganizerLayoutMutationHandlers(
                _ =>
                {
                    mutationEntered.Set();
                    return true;
                },
                (_, _) => false,
                _ => false,
                (_, _, _) => false,
                (_, _) => false,
                (_, _) => false);
        var repository =
            new OrganizerLayoutRepository(
                _ =>
                {
                    loadEntered.Set();
                    releaseLoad.Wait(
                        TimeSpan.FromSeconds(5));
                    return OrganizerLayoutSnapshot.Invalid;
                },
                mutations: handlers);

        Task<OrganizerLayoutSnapshot> load =
            Task.Run(() => repository.Load(Legacy));
        Assert.True(
            loadEntered.Wait(
                TimeSpan.FromSeconds(2)));
        Task<bool> mutation =
            Task.Run(
                () =>
                    repository.CreatePartition(
                        "工作"));

        Assert.False(
            mutationEntered.Wait(
                TimeSpan.FromMilliseconds(100)));
        releaseLoad.Set();
        await load;
        bool mutationResult = await mutation;
        Assert.True(mutationEntered.IsSet);
        Assert.True(mutationResult);
    }

    [Fact]
    public void SaveState_AlwaysReturnsLatestOptions()
    {
        var state =
            new OrganizerLayoutSaveState(
                Legacy.FallbackOptions);
        var latest =
            new OrganizerLayoutOptions(
                1.5,
                true,
                false,
                true);

        state.Update(latest);

        Assert.Equal(latest, state.Read());
    }

    [Fact]
    public void PartitionMutations_TrimAndForwardArguments()
    {
        string? createdName = null;
        (string source, string target, bool after)?
            reorder = null;
        (string file, string partition)? assignment =
            null;
        var handlers =
            new OrganizerLayoutMutationHandlers(
                name =>
                {
                    createdName = name;
                    return true;
                },
                (_, _) => false,
                _ => false,
                (source, target, after) =>
                {
                    reorder =
                        (source, target, after);
                    return true;
                },
                (_, _) => false,
                (file, partition) =>
                {
                    assignment =
                        (file, partition);
                    return true;
                });
        var repository =
            new OrganizerLayoutRepository(
                _ => OrganizerLayoutSnapshot.Invalid,
                mutations: handlers);

        Assert.True(
            repository.CreatePartition("  工作  "));
        Assert.True(
            repository.ReorderPartition(
                "  工作 ",
                "  归档 ",
                true));
        Assert.True(
            repository.AssignFileToPartition(
                " notes.txt ",
                "  工作 "));

        Assert.Equal("工作", createdName);
        Assert.Equal(
            ("工作", "归档", true),
            reorder);
        Assert.Equal(
            ("notes.txt", "工作"),
            assignment);
    }

    [Fact]
    public void PartitionMutationFailure_IsNotHidden()
    {
        var handlers =
            new OrganizerLayoutMutationHandlers(
                _ =>
                    throw new InvalidOperationException(
                        "write failed"),
                (_, _) => false,
                _ => false,
                (_, _, _) => false,
                (_, _) => false,
                (_, _) => false);
        var repository =
            new OrganizerLayoutRepository(
                _ => OrganizerLayoutSnapshot.Invalid,
                mutations: handlers);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () =>
                    repository.CreatePartition(
                        "工作"));

        Assert.Equal("write failed", error.Message);
    }
}
