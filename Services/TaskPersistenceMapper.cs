using FocusPanel.Models;

namespace FocusPanel.Services;

internal static class TaskPersistenceMapper
{
    internal static TodoItem CloneState(
        TodoItem source) =>
        new()
        {
            Id = source.Id,
            Title = source.Title,
            IsCompleted =
                source.IsCompleted,
            CreatedAt = source.CreatedAt,
            ParentId = source.ParentId,
            Status = source.Status,
            CustomValuesJson =
                source.CustomValuesJson,
            ViewMode = source.ViewMode,
            ColumnsJson =
                source.ColumnsJson,
            CustomFieldsJson =
                source.CustomFieldsJson
        };
}
