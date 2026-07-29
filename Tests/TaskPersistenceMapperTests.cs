using System;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class TaskPersistenceMapperTests
{
    [Fact]
    public void CloneState_PreservesEveryPersistedScalarWithoutNavigationGraph()
    {
        DateTime created =
            new(
                2026,
                7,
                29,
                9,
                30,
                0,
                DateTimeKind.Local);
        var source = new TodoItem
        {
            Id = 42,
            Title = "发布 FocusPanel",
            IsCompleted = true,
            CreatedAt = created,
            ParentId = 7,
            Status = "In Progress",
            CustomValuesJson =
                "{\"priority\":\"high\"}",
            ViewMode = ProjectViewMode.Board,
            ColumnsJson =
                "[\"To Do\",\"Done\"]",
            CustomFieldsJson =
                "[{\"Name\":\"Priority\"}]",
            Parent = new TodoItem
            {
                Id = 7,
                Title = "Project"
            }
        };
        source.Children.Add(
            new TodoItem
            {
                Id = 43,
                Title = "Child"
            });

        TodoItem clone =
            TaskPersistenceMapper.CloneState(
                source);

        Assert.Equal(source.Id, clone.Id);
        Assert.Equal(source.Title, clone.Title);
        Assert.Equal(
            source.IsCompleted,
            clone.IsCompleted);
        Assert.Equal(
            source.CreatedAt,
            clone.CreatedAt);
        Assert.Equal(
            source.ParentId,
            clone.ParentId);
        Assert.Equal(
            source.Status,
            clone.Status);
        Assert.Equal(
            source.CustomValuesJson,
            clone.CustomValuesJson);
        Assert.Equal(
            source.ViewMode,
            clone.ViewMode);
        Assert.Equal(
            source.ColumnsJson,
            clone.ColumnsJson);
        Assert.Equal(
            source.CustomFieldsJson,
            clone.CustomFieldsJson);
        Assert.Null(clone.Parent);
        Assert.Empty(clone.Children);
    }
}
