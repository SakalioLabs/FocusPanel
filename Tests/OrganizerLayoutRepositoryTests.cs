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
}
