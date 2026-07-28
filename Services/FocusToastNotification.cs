using System;

namespace FocusPanel.Services;

public enum FocusToastKind
{
    Information,
    Success,
    Warning
}

public sealed record FocusToastNotification(
    string Key,
    string Title,
    string Message,
    string Glyph,
    FocusToastKind Kind = FocusToastKind.Information,
    string? ActionLabel = null,
    Action? Action = null,
    TimeSpan? Duration = null);
