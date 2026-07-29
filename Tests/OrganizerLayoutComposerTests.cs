using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OrganizerLayoutComposerTests
{
    [Fact]
    public void PersonalizedView_AssignsFilesAndUsesLastPreference()
    {
        DesktopFile assigned = File(
            "notes.txt",
            DateTime.Today);
        DesktopFile unassigned = File(
            "photo.png",
            DateTime.Today);
        var snapshot =
            Snapshot(
                new[]
                {
                    new OrganizerPartitionSnapshot(
                        "旧分区",
                        0,
                        0),
                    new OrganizerPartitionSnapshot(
                        "工作",
                        1,
                        1)
                },
                new[]
                {
                    new OrganizerFilePreferenceSnapshot(
                        "notes.txt",
                        "旧分区"),
                    new OrganizerFilePreferenceSnapshot(
                        "notes.txt",
                        "工作")
                });

        var result =
            OrganizerLayoutComposer.Compose(
                snapshot,
                true,
                new[] { assigned, unassigned });

        Assert.Equal(2, result.Count);
        Assert.Empty(result[0].Files);
        Assert.Same(
            assigned,
            Assert.Single(result[1].Files));
        Assert.Equal("工作", assigned.CustomPartition);
        Assert.Null(unassigned.CustomPartition);
        Assert.Equal(1, result[1].ColumnIndex);
    }

    [Fact]
    public void TimelineView_OrdersGroupsAndAlternatesColumns()
    {
        DesktopFile today = File(
            "today.txt",
            DateTime.Today);
        today.CustomPartition = "旧分区";
        DesktopFile yesterday = File(
            "yesterday.txt",
            DateTime.Today.AddDays(-1));
        DesktopFile older = File(
            "older.txt",
            DateTime.Today.AddDays(-60));

        var result =
            OrganizerLayoutComposer.Compose(
                Snapshot(
                    Array.Empty<
                        OrganizerPartitionSnapshot>(),
                    Array.Empty<
                        OrganizerFilePreferenceSnapshot>()),
                false,
                new[] { older, yesterday, today });

        Assert.Equal(
            new[] { "今天", "昨天", "更早" },
            new[]
            {
                result[0].Name,
                result[1].Name,
                result[2].Name
            });
        Assert.Equal(0, result[0].ColumnIndex);
        Assert.Equal(1, result[1].ColumnIndex);
        Assert.Equal(0, result[2].ColumnIndex);
        Assert.Null(today.CustomPartition);
    }

    private static OrganizerLayoutSnapshot Snapshot(
        OrganizerPartitionSnapshot[] partitions,
        OrganizerFilePreferenceSnapshot[] preferences) =>
        new(
            true,
            new OrganizerLayoutOptions(
                1,
                false,
                true,
                false),
            partitions,
            preferences);

    private static DesktopFile File(
        string name,
        DateTime createdAt) =>
        new()
        {
            Name = name,
            FullPath = $@"C:\Desktop\{name}",
            CreatedAt = createdAt
        };
}
