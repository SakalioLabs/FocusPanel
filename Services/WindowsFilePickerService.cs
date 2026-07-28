using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace FocusPanel.Services;

public sealed class WindowsFilePickerService
    : IFilePickerService
{
    private readonly IFileDialogBoundary _boundary;
    private readonly Func<Window?> _ownerProvider;
    private readonly Func<
        IFocusDialogInteractionHost?>
        _interactionHostProvider;

    public WindowsFilePickerService()
        : this(
            new WpfFileDialogBoundary(),
            GetOwner,
            GetInteractionHost)
    {
    }

    internal WindowsFilePickerService(
        IFileDialogBoundary boundary,
        Func<Window?> ownerProvider,
        Func<IFocusDialogInteractionHost?>?
            interactionHostProvider = null)
    {
        _boundary = boundary;
        _ownerProvider = ownerProvider;
        _interactionHostProvider =
            interactionHostProvider
            ?? (() => null);
    }

    public FilePickerResult PickFile(
        FilePickerRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Title))
        {
            return FilePickerResult.Failed(
                "文件选择器缺少标题。");
        }
        if (string.IsNullOrWhiteSpace(
                request.Filter))
        {
            return FilePickerResult.Failed(
                "文件选择器缺少文件类型过滤条件。");
        }

        try
        {
            using FocusDialogInteractionLease interaction =
                FocusDialogInteractionLease.Enter(
                    _interactionHostProvider());
            return _boundary.Show(
                request,
                _ownerProvider());
        }
        catch (Exception ex)
        {
            return FilePickerResult.Failed(
                $"无法打开 Windows 文件选择器：{ex.Message}");
        }
    }

    private static Window? GetOwner() =>
        Application.Current?
            .Windows
            .OfType<Window>()
            .Where(window =>
                window.IsVisible)
            .OrderByDescending(window =>
                window.IsActive)
            .FirstOrDefault();

    private static IFocusDialogInteractionHost?
        GetInteractionHost() =>
        Application.Current?
            .Windows
            .OfType<Window>()
            .Where(window =>
                window.IsVisible)
            .OfType<IFocusDialogInteractionHost>()
            .FirstOrDefault();
}

internal sealed class WpfFileDialogBoundary
    : IFileDialogBoundary
{
    public FilePickerResult Show(
        FilePickerRequest request,
        Window? owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = request.Title,
            Filter = request.Filter,
            InitialDirectory =
                request.InitialDirectory
                ?? string.Empty,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            AddExtension = true
        };

        bool? accepted = owner != null
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();
        if (accepted != true)
            return FilePickerResult.Canceled();

        return string.IsNullOrWhiteSpace(
            dialog.FileName)
            ? FilePickerResult.Failed(
                "Windows 没有返回有效的文件路径。")
            : FilePickerResult.Selected(
                dialog.FileName);
    }
}
