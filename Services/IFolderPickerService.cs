using System;

namespace FocusPanel.Services;

public interface IFolderPickerService
{
    FolderPickerResult PickFolder(
        FolderPickerRequest request);
}

public sealed record FolderPickerRequest(
    string Title,
    string? InitialPath = null,
    string ConfirmButtonText = "选择文件夹");

public enum FolderPickerStatus
{
    Selected,
    Canceled,
    Failed
}

public sealed record FolderPickerResult(
    FolderPickerStatus Status,
    string? Path = null,
    string? Error = null)
{
    public static FolderPickerResult Selected(
        string path) =>
        new(
            FolderPickerStatus.Selected,
            path);

    public static FolderPickerResult Canceled() =>
        new(FolderPickerStatus.Canceled);

    public static FolderPickerResult Failed(
        string error) =>
        new(
            FolderPickerStatus.Failed,
            Error: error);
}

internal interface IShellFolderDialogBoundary
{
    FolderPickerResult Show(
        FolderPickerRequest request,
        IntPtr ownerHandle);
}

internal sealed record FolderSelectionDecision(
    bool ShouldApply,
    string? Path = null,
    string? Error = null);

internal static class FolderSelectionPolicy
{
    internal static FolderSelectionDecision Resolve(
        FolderPickerResult result) =>
        result.Status switch
        {
            FolderPickerStatus.Selected
                when !string.IsNullOrWhiteSpace(
                    result.Path) =>
                new FolderSelectionDecision(
                    true,
                    result.Path),
            FolderPickerStatus.Canceled =>
                new FolderSelectionDecision(false),
            _ =>
                new FolderSelectionDecision(
                    false,
                    Error: result.Error
                        ?? "Windows 没有返回有效的文件夹路径。")
        };
}
