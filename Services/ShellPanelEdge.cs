namespace FocusPanel.Services;

internal enum ShellPanelEdge
{
    Left,
    Right
}

internal static class ShellPanelEdgePolicy
{
    internal const string LeftValue = "Left";
    internal const string RightValue = "Right";

    internal static ShellPanelEdge Parse(
        string? value) =>
        value?.Trim() == LeftValue
            ? ShellPanelEdge.Left
            : ShellPanelEdge.Right;

    internal static string NormalizeValue(
        string? value) =>
        Parse(value).ToString();

    internal static bool IsLeft(
        string? value) =>
        Parse(value) == ShellPanelEdge.Left;
}
