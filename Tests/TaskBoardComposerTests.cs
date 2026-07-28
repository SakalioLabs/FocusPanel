using System.Linq;
using FocusPanel.Models;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskBoardComposerTests
{
    [Fact]
    public void MissingConfigurationUsesThreeDefaultColumns()
    {
        var columns =
            TaskBoardComposer.Compose(
                new TodoItem[0],
                null);

        Assert.Equal(
            new[]
            {
                "To Do",
                "In Progress",
                "Done"
            },
            columns.Select(
                column => column.Header));
        Assert.Equal(
            new[]
            {
                "待处理",
                "进行中",
                "已完成"
            },
            columns.Select(
                column => column.DisplayHeader));
    }

    [Fact]
    public void CustomColumnsRemoveBlankAndDuplicateNames()
    {
        var names =
            TaskBoardComposer.GetColumnNames(
                "[\"待办\",\"\",\"处理中\",\"待办\"]");

        Assert.Equal(
            new[] { "待办", "处理中" },
            names);
    }

    [Fact]
    public void TasksAreGroupedByExactStatusIdentity()
    {
        var todo =
            new TodoItem
            {
                Title = "A",
                Status = "To Do"
            };
        var done =
            new TodoItem
            {
                Title = "B",
                Status = "Done"
            };

        var columns =
            TaskBoardComposer.Compose(
                new[] { todo, done },
                null);

        Assert.Same(todo, columns[0].Tasks.Single());
        Assert.Empty(columns[1].Tasks);
        Assert.Same(done, columns[2].Tasks.Single());
    }

    [Fact]
    public void UnknownStatusFallsBackToFirstColumnWithoutDroppingTask()
    {
        var task =
            new TodoItem
            {
                Title = "Legacy",
                Status = "Unknown"
            };

        var columns =
            TaskBoardComposer.Compose(
                new[] { task },
                "[\"Backlog\",\"Done\"]");

        Assert.Same(task, columns[0].Tasks.Single());
        Assert.Empty(columns[1].Tasks);
    }

    [Fact]
    public void InvalidJsonFallsBackToDefaults()
    {
        var names =
            TaskBoardComposer.GetColumnNames(
                "{not-json}");

        Assert.Equal(3, names.Count);
        Assert.Equal("To Do", names[0]);
    }

    [Fact]
    public void AdjacentStatusMatchesCurrentValueWithoutCaseSensitivity()
    {
        string? next =
            TaskBoardComposer.GetAdjacentStatus(
                "to do",
                null,
                1);

        Assert.Equal("In Progress", next);
    }

    [Fact]
    public void UnknownStatusBehavesLikeItsVisibleFirstColumn()
    {
        string? previous =
            TaskBoardComposer.GetAdjacentStatus(
                "Legacy",
                null,
                -1);
        string? next =
            TaskBoardComposer.GetAdjacentStatus(
                "Legacy",
                null,
                1);

        Assert.Null(previous);
        Assert.Equal("In Progress", next);
    }
}
