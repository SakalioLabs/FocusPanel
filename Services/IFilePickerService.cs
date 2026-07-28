using System.Windows;

namespace FocusPanel.Services;

public interface IFilePickerService
{
    FilePickerResult PickFile(
        FilePickerRequest request);
}

public sealed record FilePickerRequest(
    string Title,
    string Filter,
    string? InitialDirectory = null);

public enum FilePickerStatus
{
    Selected,
    Canceled,
    Failed
}

public sealed record FilePickerResult(
    FilePickerStatus Status,
    string? Path = null,
    string? Error = null)
{
    public static FilePickerResult Selected(
        string path) =>
        new(
            FilePickerStatus.Selected,
            path);

    public static FilePickerResult Canceled() =>
        new(FilePickerStatus.Canceled);

    public static FilePickerResult Failed(
        string error) =>
        new(
            FilePickerStatus.Failed,
            Error: error);
}

internal interface IFileDialogBoundary
{
    FilePickerResult Show(
        FilePickerRequest request,
        Window? owner);
}

internal sealed record FileSelectionDecision(
    bool ShouldOpen,
    string? Path = null,
    string? Error = null);

internal static class FileSelectionPolicy
{
    internal static FileSelectionDecision Resolve(
        FilePickerResult result) =>
        result.Status switch
        {
            FilePickerStatus.Selected
                when !string.IsNullOrWhiteSpace(
                    result.Path) =>
                new FileSelectionDecision(
                    true,
                    result.Path),
            FilePickerStatus.Canceled =>
                new FileSelectionDecision(false),
            _ =>
                new FileSelectionDecision(
                    false,
                    Error: result.Error
                        ?? "Windows 没有返回有效的文件路径。")
        };
}
