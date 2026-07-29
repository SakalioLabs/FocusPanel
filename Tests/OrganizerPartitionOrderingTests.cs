using System;
using System.Collections.Generic;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class OrganizerPartitionOrderingTests
{
    [Fact]
    public void ReorderWithinColumn_InsertsAfterTarget()
    {
        DesktopPartition first =
            Partition(1, "一", 0, 0);
        DesktopPartition second =
            Partition(2, "二", 0, 1);
        DesktopPartition third =
            Partition(3, "三", 0, 2);
        var items =
            new List<DesktopPartition>
            {
                first,
                second,
                third
            };

        bool changed =
            OrganizerPartitionOrdering.Reorder(
                items,
                "一",
                "二",
                true);

        Assert.True(changed);
        Assert.Equal(0, second.OrderIndex);
        Assert.Equal(1, first.OrderIndex);
        Assert.Equal(2, third.OrderIndex);
    }

    [Fact]
    public void ReorderAcrossColumns_ReindexesBothColumns()
    {
        DesktopPartition leftOne =
            Partition(1, "左一", 0, 4);
        DesktopPartition leftTwo =
            Partition(2, "左二", 0, 9);
        DesktopPartition rightOne =
            Partition(3, "右一", 1, 2);
        var items =
            new List<DesktopPartition>
            {
                leftOne,
                leftTwo,
                rightOne
            };

        bool changed =
            OrganizerPartitionOrdering.Reorder(
                items,
                "左一",
                "右一",
                false);

        Assert.True(changed);
        Assert.Equal(1, leftOne.ColumnIndex);
        Assert.Equal(0, leftOne.OrderIndex);
        Assert.Equal(1, rightOne.OrderIndex);
        Assert.Equal(0, leftTwo.OrderIndex);
    }

    [Fact]
    public void MoveToColumn_AppendsAndReindexes()
    {
        DesktopPartition source =
            Partition(1, "来源", 0, 7);
        DesktopPartition remaining =
            Partition(2, "保留", 0, 3);
        DesktopPartition target =
            Partition(3, "目标", 1, 5);
        var items =
            new List<DesktopPartition>
            {
                source,
                remaining,
                target
            };

        bool changed =
            OrganizerPartitionOrdering.MoveToColumn(
                items,
                "来源",
                1);

        Assert.True(changed);
        Assert.Equal(1, source.ColumnIndex);
        Assert.Equal(0, target.OrderIndex);
        Assert.Equal(1, source.OrderIndex);
        Assert.Equal(0, remaining.OrderIndex);
    }

    [Fact]
    public void MissingOrUnchangedPartition_ReturnsFalse()
    {
        var items =
            new List<DesktopPartition>
            {
                Partition(1, "一", 0, 0)
            };

        Assert.False(
            OrganizerPartitionOrdering.Reorder(
                items,
                "不存在",
                "一",
                false));
        Assert.False(
            OrganizerPartitionOrdering.MoveToColumn(
                items,
                "一",
                0));
    }

    [Fact]
    public void MoveToInvalidColumn_IsRejected()
    {
        var items =
            new List<DesktopPartition>
            {
                Partition(1, "一", 0, 0)
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                OrganizerPartitionOrdering
                    .MoveToColumn(
                        items,
                        "一",
                        2));
    }

    private static DesktopPartition Partition(
        int id,
        string name,
        int column,
        int order) =>
        new()
        {
            Id = id,
            Name = name,
            ColumnIndex = column,
            OrderIndex = order
        };
}
