using System;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskSearchPolicyTests
{
    private static readonly DateTime Now =
        new(2026, 7, 30, 12, 0, 0);

    [Fact]
    public void Search_ExactTitleOutranksParentAndSubstring()
    {
        TaskSearchItem[] items =
        {
            Item(1, "整理发布说明", "FocusPanel"),
            Item(2, "发布", "其他项目"),
            Item(3, "检查版本", "发布")
        };

        var results =
            TaskSearchPolicy.Search(
                items,
                "发布");

        Assert.Equal(
            new[] { 2, 3, 1 },
            results.Select(item =>
                item.Id));
    }

    [Fact]
    public void Search_UsesNormalizedMultiWordMatching()
    {
        TaskSearchItem result =
            Assert.Single(
                TaskSearchPolicy.Search(
                    new[]
                    {
                        Item(
                            8,
                            "Prepare Release Notes",
                            "FocusPanel")
                    },
                    "release notes"));

        Assert.Equal(8, result.Id);
    }

    [Fact]
    public void Search_TiedMatchesPreferNewestThenStableId()
    {
        TaskSearchItem[] items =
        {
            Item(
                9,
                "修复任务",
                "项目",
                Now.AddMinutes(-1)),
            Item(
                8,
                "修复任务",
                "项目",
                Now),
            Item(
                7,
                "修复任务",
                "项目",
                Now)
        };

        Assert.Equal(
            new[] { 7, 8, 9 },
            TaskSearchPolicy
                .Search(
                    items,
                    "修复任务")
                .Select(item =>
                    item.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Search_BlankQueryDoesNotExposeTaskList(
        string? query)
    {
        Assert.Empty(
            TaskSearchPolicy.Search(
                new[]
                {
                    Item(
                        1,
                        "私密待办",
                        "Inbox")
                },
                query));
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        Assert.Equal(
            2,
            TaskSearchPolicy.Search(
                Enumerable.Range(1, 8)
                    .Select(index =>
                        Item(
                            index,
                            $"任务 {index}",
                            "Inbox")),
                "任务",
                2)
            .Count);
    }

    private static TaskSearchItem Item(
        int id,
        string title,
        string parent,
        DateTime? createdAt = null) =>
        new(
            id,
            title,
            1,
            parent,
            "To Do",
            createdAt ?? Now);
}
