using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FocusPanel.Services;

public sealed class ShellFolderPickerService
    : IFolderPickerService
{
    private readonly IShellFolderDialogBoundary _boundary;
    private readonly Func<IntPtr> _ownerHandleProvider;
    private readonly Func<
        IFocusDialogInteractionHost?>
        _interactionHostProvider;

    public ShellFolderPickerService()
        : this(
            new WindowsShellFolderDialogBoundary(),
            GetOwnerHandle,
            GetInteractionHost)
    {
    }

    internal ShellFolderPickerService(
        IShellFolderDialogBoundary boundary,
        Func<IntPtr> ownerHandleProvider,
        Func<IFocusDialogInteractionHost?>?
            interactionHostProvider = null)
    {
        _boundary = boundary;
        _ownerHandleProvider = ownerHandleProvider;
        _interactionHostProvider =
            interactionHostProvider
            ?? (() => null);
    }

    public FolderPickerResult PickFolder(
        FolderPickerRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Title))
        {
            return FolderPickerResult.Failed(
                "文件夹选择器缺少标题。");
        }

        try
        {
            using FocusDialogInteractionLease interaction =
                FocusDialogInteractionLease.Enter(
                    _interactionHostProvider());
            return _boundary.Show(
                request,
                _ownerHandleProvider());
        }
        catch (Exception ex)
        {
            return FolderPickerResult.Failed(
                $"无法打开 Windows 文件夹选择器：{ex.Message}");
        }
    }

    private static IntPtr GetOwnerHandle()
    {
        Window? owner = Application.Current?
            .Windows
            .OfType<Window>()
            .Where(window =>
                window.IsVisible)
            .OrderByDescending(window =>
                window.IsActive)
            .FirstOrDefault();
        if (owner != null)
        {
            IntPtr handle =
                new WindowInteropHelper(owner).Handle;
            if (handle != IntPtr.Zero)
                return handle;
        }

        return NativeMethods.GetForegroundWindow();
    }

    private static IFocusDialogInteractionHost?
        GetInteractionHost() =>
        Application.Current?
            .Windows
            .OfType<Window>()
            .Where(window =>
                window.IsVisible)
            .OfType<IFocusDialogInteractionHost>()
            .FirstOrDefault();

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
    }
}

internal sealed class WindowsShellFolderDialogBoundary
    : IShellFolderDialogBoundary
{
    private const int OperationCanceled =
        unchecked((int)0x800704C7);
    private static readonly Guid ShellItemId =
        new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    public FolderPickerResult Show(
        FolderPickerRequest request,
        IntPtr ownerHandle)
    {
        IFileOpenDialogNative? dialog = null;
        IShellItemNative? initialFolder = null;
        IShellItemNative? result = null;
        try
        {
            dialog =
                (IFileOpenDialogNative)
                (object)
                new FileOpenDialogComObject();
            int optionsResult =
                dialog.GetOptions(
                    out FileOpenDialogOptions options);
            if (optionsResult < 0)
                return FailedFromHResult(optionsResult);

            options |=
                FileOpenDialogOptions.PickFolders
                | FileOpenDialogOptions.ForceFileSystem
                | FileOpenDialogOptions.PathMustExist
                | FileOpenDialogOptions.NoChangeDirectory
                | FileOpenDialogOptions.DoNotAddToRecent;
            int setOptionsResult =
                dialog.SetOptions(options);
            if (setOptionsResult < 0)
                return FailedFromHResult(setOptionsResult);

            _ = dialog.SetTitle(request.Title);
            if (!string.IsNullOrWhiteSpace(
                    request.ConfirmButtonText))
            {
                _ = dialog.SetOkButtonLabel(
                    request.ConfirmButtonText);
            }

            if (!string.IsNullOrWhiteSpace(
                    request.InitialPath)
                && Directory.Exists(
                    request.InitialPath))
            {
                Guid shellItemId = ShellItemId;
                int itemResult =
                    SHCreateItemFromParsingName(
                        request.InitialPath,
                        IntPtr.Zero,
                        ref shellItemId,
                        out initialFolder);
                if (itemResult >= 0
                    && initialFolder != null)
                {
                    _ = dialog.SetFolder(
                        initialFolder);
                }
            }

            int showResult =
                dialog.Show(ownerHandle);
            if (showResult == OperationCanceled)
                return FolderPickerResult.Canceled();
            if (showResult < 0)
                return FailedFromHResult(showResult);

            int resultCode =
                dialog.GetResult(out result);
            if (resultCode < 0 || result == null)
                return FailedFromHResult(resultCode);

            int pathResult =
                result.GetDisplayName(
                    ShellItemDisplayName.FileSystemPath,
                    out IntPtr pathPointer);
            if (pathResult < 0
                || pathPointer == IntPtr.Zero)
            {
                return FailedFromHResult(pathResult);
            }

            try
            {
                string? path =
                    Marshal.PtrToStringUni(
                        pathPointer);
                return string.IsNullOrWhiteSpace(path)
                    ? FolderPickerResult.Failed(
                        "Windows 没有返回有效的文件夹路径。")
                    : FolderPickerResult.Selected(path);
            }
            finally
            {
                Marshal.FreeCoTaskMem(
                    pathPointer);
            }
        }
        catch (COMException ex)
            when (ex.HResult == OperationCanceled)
        {
            return FolderPickerResult.Canceled();
        }
        catch (Exception ex)
        {
            return FolderPickerResult.Failed(
                $"Windows 文件夹选择器失败：{ex.Message}");
        }
        finally
        {
            ReleaseComObject(result);
            ReleaseComObject(initialFolder);
            ReleaseComObject(dialog);
        }
    }

    private static FolderPickerResult FailedFromHResult(
        int result)
    {
        string message =
            Marshal.GetExceptionForHR(result)?.Message
            ?? $"HRESULT 0x{result:X8}";
        return FolderPickerResult.Failed(
            $"Windows 文件夹选择器失败：{message}");
    }

    private static void ReleaseComObject(
        object? value)
    {
        if (value != null
            && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        PreserveSig = true)]
    private static extern int
        SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)]
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)]
            out IShellItemNative shellItem);

    [Flags]
    private enum FileOpenDialogOptions : uint
    {
        NoChangeDirectory = 0x00000008,
        PickFolders = 0x00000020,
        ForceFileSystem = 0x00000040,
        PathMustExist = 0x00000800,
        DoNotAddToRecent = 0x02000000
    }

    private enum ShellItemDisplayName : uint
    {
        FileSystemPath = 0x80058000
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private sealed class FileOpenDialogComObject
    {
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialogNative
    {
        [PreserveSig]
        int Show(IntPtr owner);

        [PreserveSig]
        int SetFileTypes(
            uint count,
            IntPtr filterSpecifications);

        [PreserveSig]
        int SetFileTypeIndex(uint index);

        [PreserveSig]
        int GetFileTypeIndex(out uint index);

        [PreserveSig]
        int Advise(
            IntPtr events,
            out uint cookie);

        [PreserveSig]
        int Unadvise(uint cookie);

        [PreserveSig]
        int SetOptions(
            FileOpenDialogOptions options);

        [PreserveSig]
        int GetOptions(
            out FileOpenDialogOptions options);

        [PreserveSig]
        int SetDefaultFolder(
            IShellItemNative folder);

        [PreserveSig]
        int SetFolder(
            IShellItemNative folder);

        [PreserveSig]
        int GetFolder(
            out IShellItemNative folder);

        [PreserveSig]
        int GetCurrentSelection(
            out IShellItemNative selection);

        [PreserveSig]
        int SetFileName(
            [MarshalAs(UnmanagedType.LPWStr)]
            string name);

        [PreserveSig]
        int GetFileName(out IntPtr name);

        [PreserveSig]
        int SetTitle(
            [MarshalAs(UnmanagedType.LPWStr)]
            string title);

        [PreserveSig]
        int SetOkButtonLabel(
            [MarshalAs(UnmanagedType.LPWStr)]
            string label);

        [PreserveSig]
        int SetFileNameLabel(
            [MarshalAs(UnmanagedType.LPWStr)]
            string label);

        [PreserveSig]
        int GetResult(
            out IShellItemNative item);

        [PreserveSig]
        int AddPlace(
            IShellItemNative item,
            uint alignment);

        [PreserveSig]
        int SetDefaultExtension(
            [MarshalAs(UnmanagedType.LPWStr)]
            string extension);

        [PreserveSig]
        int Close(int result);

        [PreserveSig]
        int SetClientGuid(ref Guid clientGuid);

        [PreserveSig]
        int ClearClientData();

        [PreserveSig]
        int SetFilter(IntPtr filter);

        [PreserveSig]
        int GetResults(out IntPtr items);

        [PreserveSig]
        int GetSelectedItems(out IntPtr items);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemNative
    {
        [PreserveSig]
        int BindToHandler(
            IntPtr bindContext,
            ref Guid handlerId,
            ref Guid interfaceId,
            out IntPtr result);

        [PreserveSig]
        int GetParent(
            out IShellItemNative parent);

        [PreserveSig]
        int GetDisplayName(
            ShellItemDisplayName displayName,
            out IntPtr name);

        [PreserveSig]
        int GetAttributes(
            uint mask,
            out uint attributes);

        [PreserveSig]
        int Compare(
            IShellItemNative item,
            uint hint,
            out int order);
    }
}
