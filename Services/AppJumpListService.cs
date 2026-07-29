using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record AppJumpListItem(
    string DisplayName,
    string LaunchTarget,
    string? Arguments)
{
    internal AppJumpListItemSource
        Source
    {
        get;
        init;
    } = AppJumpListItemSource
        .ShellItem;

    internal string IdentityKey =>
        LaunchTarget.Trim()
        + "\n"
        + (Arguments?.Trim()
           ?? string.Empty);
}

internal enum AppJumpListItemSource
{
    ShellItem,
    ShellLink
}

internal sealed record
    AppJumpListApplicationLaunch(
        AppLaunchKind LaunchKind,
        string LaunchTarget,
        string? Arguments);

internal interface IAppJumpListNative
{
    IReadOnlyList<AppJumpListItem>
        ReadRecent(
            string applicationUserModelId,
            int limit);
}

internal interface IAppJumpListService :
    IDisposable
{
    Task<IReadOnlyList<AppJumpListItem>>
        GetRecentAsync(
            string applicationUserModelId,
            int limit,
            CancellationToken
                cancellationToken =
                    default);

    Task<bool> OpenAsync(
        AppJumpListItem item,
        AppJumpListApplicationLaunch?
            application);
}

internal static class AppJumpListPolicy
{
    internal const int MaximumItemCount = 8;
    private const int MaximumTitleLength = 96;

    internal static IReadOnlyList<
        AppJumpListItem> Normalize(
            IEnumerable<AppJumpListItem>?
                source,
            int limit)
    {
        if (source == null
            || limit <= 0)
        {
            return Array.Empty<
                AppJumpListItem>();
        }

        int boundedLimit =
            Math.Min(
                MaximumItemCount,
                limit);
        var identities =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);
        var result =
            new List<AppJumpListItem>(
                boundedLimit);
        foreach (AppJumpListItem item
                 in source)
        {
            string target =
                item.LaunchTarget
                    ?.Trim()
                ?? string.Empty;
            if (target.Length == 0)
                continue;

            string? arguments =
                string.IsNullOrWhiteSpace(
                    item.Arguments)
                    ? null
                    : item.Arguments.Trim();
            string identity =
                target
                + "\n"
                + (arguments
                   ?? string.Empty);
            if (!identities.Add(
                    identity))
            {
                continue;
            }

            string title =
                NormalizeTitle(
                    item.DisplayName,
                    target,
                    arguments);
            result.Add(
                new AppJumpListItem(
                    title,
                    target,
                    arguments)
                {
                    Source =
                        item.Source
                });
            if (result.Count
                == boundedLimit)
            {
                break;
            }
        }

        return result;
    }

    private static string NormalizeTitle(
        string? title,
        string target,
        string? arguments)
    {
        string normalized =
            string.Join(
                " ",
                (title
                 ?? string.Empty)
                    .Split(
                        new[]
                        {
                            '\r',
                            '\n',
                            '\t'
                        },
                        StringSplitOptions
                            .RemoveEmptyEntries))
                .Trim();
        if (normalized.Length == 0)
        {
            normalized =
                TryGetFileName(
                    arguments)
                ?? TryGetFileName(
                    target)
                ?? target;
        }

        int[] textElements =
            StringInfo
                .ParseCombiningCharacters(
                    normalized);
        return textElements.Length
               <= MaximumTitleLength
            ? normalized
            : normalized[
                ..textElements[
                    MaximumTitleLength
                    - 1]]
              + "…";
    }

    private static string? TryGetFileName(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        string candidate =
            value.Trim()
                .Trim('"');
        try
        {
            string name =
                Path.GetFileName(
                    candidate);
            return string
                .IsNullOrWhiteSpace(name)
                ? null
                : name;
        }
        catch
        {
            return null;
        }
    }
}

internal static class
    AppJumpListOpenRequestPolicy
{
    internal static bool TryBuild(
        AppJumpListItem item,
        AppJumpListApplicationLaunch?
            application,
        out ProcessStartInfo? request)
    {
        request = null;
        if (item == null
            || string.IsNullOrWhiteSpace(
                item.LaunchTarget))
        {
            return false;
        }

        string target =
            item.LaunchTarget.Trim();
        string? arguments =
            string.IsNullOrWhiteSpace(
                item.Arguments)
                ? null
                : item.Arguments.Trim();
        if (item.Source
                == AppJumpListItemSource
                    .ShellItem
            && Path.IsPathFullyQualified(
                target)
            && application
                is
                {
                    LaunchKind:
                        AppLaunchKind
                            .Executable
                        or AppLaunchKind
                            .Shortcut
                }
            && Path.IsPathFullyQualified(
                application
                    .LaunchTarget))
        {
            arguments =
                AppendDocumentArgument(
                    application.Arguments,
                    target);
            target =
                application
                    .LaunchTarget
                    .Trim();
        }

        request =
            new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
        if (!string.IsNullOrWhiteSpace(
                arguments))
        {
            request.Arguments =
                arguments;
        }

        return true;
    }

    private static string
        AppendDocumentArgument(
            string? existingArguments,
            string documentPath)
    {
        string documentArgument =
            "\""
            + documentPath
                .Replace(
                    "\"",
                    "\\\"",
                    StringComparison
                        .Ordinal)
            + "\"";
        return string.IsNullOrWhiteSpace(
                existingArguments)
            ? documentArgument
            : existingArguments.Trim()
              + " "
              + documentArgument;
    }
}

internal sealed class AppJumpListService :
    IAppJumpListService
{
    private readonly IAppJumpListNative
        _native;
    private readonly Func<
        ProcessStartInfo,
        bool> _start;
    private volatile bool _disposed;

    internal AppJumpListService()
        : this(
            new WindowsAppJumpListNative(),
            request =>
                AppLaunchExecution.TryStart(
                    request))
    {
    }

    internal AppJumpListService(
        IAppJumpListNative native,
        Func<ProcessStartInfo, bool>
            start)
    {
        _native =
            native
            ?? throw new
                ArgumentNullException(
                    nameof(native));
        _start =
            start
            ?? throw new
                ArgumentNullException(
                    nameof(start));
    }

    public Task<IReadOnlyList<
        AppJumpListItem>>
        GetRecentAsync(
            string applicationUserModelId,
            int limit,
            CancellationToken
                cancellationToken =
                    default)
    {
        string appId =
            applicationUserModelId
                ?.Trim()
            ?? string.Empty;
        if (_disposed
            || appId.Length == 0
            || limit <= 0)
        {
            return Task.FromResult<
                IReadOnlyList<
                    AppJumpListItem>>(
                Array.Empty<
                    AppJumpListItem>());
        }

        if (cancellationToken
                .IsCancellationRequested)
        {
            return Task.FromCanceled<
                IReadOnlyList<
                    AppJumpListItem>>(
                cancellationToken);
        }

        var completion =
            new TaskCompletionSource<
                IReadOnlyList<
                    AppJumpListItem>>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        var thread =
            new Thread(() =>
            {
                if (_disposed
                    || cancellationToken
                        .IsCancellationRequested)
                {
                    completion
                        .TrySetCanceled(
                            cancellationToken);
                    return;
                }

                try
                {
                    IReadOnlyList<
                        AppJumpListItem>
                        items =
                            AppJumpListPolicy
                                .Normalize(
                                    _native
                                        .ReadRecent(
                                            appId,
                                            Math.Min(
                                                limit,
                                                AppJumpListPolicy
                                                    .MaximumItemCount)),
                                    limit);
                    if (_disposed
                        || cancellationToken
                            .IsCancellationRequested)
                    {
                        completion
                            .TrySetCanceled(
                                cancellationToken);
                    }
                    else
                    {
                        completion
                            .TrySetResult(
                                items);
                    }
                }
                catch
                {
                    completion
                        .TrySetResult(
                            Array.Empty<
                                AppJumpListItem>());
                }
            })
            {
                IsBackground = true,
                Name =
                    "FocusPanel.JumpList"
            };
        thread.SetApartmentState(
            ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public Task<bool> OpenAsync(
        AppJumpListItem item,
        AppJumpListApplicationLaunch?
            application)
    {
        if (_disposed
            || !AppJumpListOpenRequestPolicy
                .TryBuild(
                    item,
                    application,
                    out ProcessStartInfo?
                        request)
            || request == null)
        {
            return Task.FromResult(false);
        }

        return Task.Run(() =>
        {
            try
            {
                return !_disposed
                       && _start(request);
            }
            catch
            {
                return false;
            }
        });
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal sealed class
    WindowsAppJumpListNative :
        IAppJumpListNative
{
    private const uint SigDnNormalDisplay =
        0x00000000;
    private const uint
        SigDnFileSystemPath =
            0x80058000;
    private const uint
        SigDnDesktopAbsoluteParsing =
            0x80028000;
    private const uint SlgpRawPath =
        0x00000004;
    private const ushort VtLpwstr = 31;

    private static readonly Guid
        IidIUnknown =
            new(
                "00000000-0000-0000-C000-000000000046");
    private static readonly Guid
        IidIObjectArray =
            new(
                "92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");
    private static readonly PropertyKey
        TitlePropertyKey =
            new(
                new Guid(
                    "F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
                2);

    public IReadOnlyList<AppJumpListItem>
        ReadRecent(
            string applicationUserModelId,
            int limit)
    {
        object? documentListsObject =
            null;
        object? arrayObject = null;
        try
        {
            documentListsObject =
                new
                    ApplicationDocumentListsComObject();
            var documentLists =
                (IApplicationDocumentLists)
                    documentListsObject;
            if (documentLists.SetAppId(
                    applicationUserModelId)
                < 0)
            {
                return Array.Empty<
                    AppJumpListItem>();
            }

            Guid arrayId =
                IidIObjectArray;
            if (documentLists.GetList(
                    AppDocumentListType
                        .Recent,
                    (uint)Math.Max(
                        1,
                        limit),
                    ref arrayId,
                    out arrayObject)
                    < 0
                || arrayObject
                    is not IObjectArray
                        array)
            {
                return Array.Empty<
                    AppJumpListItem>();
            }

            if (array.GetCount(
                    out uint count)
                < 0)
            {
                return Array.Empty<
                    AppJumpListItem>();
            }

            var result =
                new List<
                    AppJumpListItem>(
                    (int)Math.Min(
                        count,
                        (uint)limit));
            uint bounded =
                Math.Min(
                    count,
                    (uint)Math.Max(
                        1,
                        limit));
            for (uint index = 0;
                 index < bounded;
                 index++)
            {
                object? itemObject =
                    null;
                try
                {
                    Guid unknownId =
                        IidIUnknown;
                    if (array.GetAt(
                            index,
                            ref unknownId,
                            out itemObject)
                            < 0
                        || itemObject
                            == null)
                    {
                        continue;
                    }

                    AppJumpListItem? item =
                        itemObject
                            is IShellLinkW
                                shellLink
                            ? ReadShellLink(
                                shellLink,
                                itemObject)
                            : itemObject
                                is IShellItem
                                    shellItem
                                ? ReadShellItem(
                                    shellItem)
                                : null;
                    if (item != null)
                        result.Add(item);
                }
                catch
                {
                    // One malformed destination must not
                    // hide the remaining recent items.
                }
                finally
                {
                    ReleaseComObject(
                        itemObject);
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<
                AppJumpListItem>();
        }
        finally
        {
            ReleaseComObject(
                arrayObject);
            ReleaseComObject(
                documentListsObject);
        }
    }

    private static AppJumpListItem?
        ReadShellItem(
            IShellItem shellItem)
    {
        string? title =
            GetDisplayName(
                shellItem,
                SigDnNormalDisplay);
        string? target =
            GetDisplayName(
                shellItem,
                SigDnFileSystemPath)
            ?? GetDisplayName(
                shellItem,
                SigDnDesktopAbsoluteParsing);
        return string
            .IsNullOrWhiteSpace(target)
            ? null
            : new AppJumpListItem(
                title
                ?? string.Empty,
                target,
                null);
    }

    private static AppJumpListItem?
        ReadShellLink(
            IShellLinkW shellLink,
            object shellLinkObject)
    {
        var target =
            new StringBuilder(2048);
        var arguments =
            new StringBuilder(4096);
        var description =
            new StringBuilder(512);
        shellLink.GetPath(
            target,
            target.Capacity,
            IntPtr.Zero,
            SlgpRawPath);
        shellLink.GetArguments(
            arguments,
            arguments.Capacity);
        shellLink.GetDescription(
            description,
            description.Capacity);
        if (target.Length == 0)
            return null;

        string? title =
            shellLinkObject
                is IPropertyStore
                    propertyStore
                ? ReadStringProperty(
                    propertyStore,
                    TitlePropertyKey)
                : null;
        if (string.IsNullOrWhiteSpace(
                title))
        {
            title =
                description.Length > 0
                    ? description
                        .ToString()
                    : null;
        }

        return new AppJumpListItem(
            title
            ?? string.Empty,
            target.ToString(),
            arguments.Length == 0
                ? null
                : arguments
                    .ToString())
        {
            Source =
                AppJumpListItemSource
                    .ShellLink
        };
    }

    private static string? GetDisplayName(
        IShellItem item,
        uint mode)
    {
        IntPtr value = IntPtr.Zero;
        try
        {
            return item.GetDisplayName(
                       mode,
                       out value)
                       >= 0
                   && value
                       != IntPtr.Zero
                ? Marshal.PtrToStringUni(
                    value)
                : null;
        }
        finally
        {
            if (value != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(
                    value);
            }
        }
    }

    private static string?
        ReadStringProperty(
            IPropertyStore store,
            PropertyKey key)
    {
        PropVariant value = default;
        try
        {
            return store.GetValue(
                       ref key,
                       out value)
                       >= 0
                   && value.Type
                       == VtLpwstr
                   && value.Pointer
                       != IntPtr.Zero
                ? Marshal.PtrToStringUni(
                    value.Pointer)
                : null;
        }
        finally
        {
            NativeMethods.PropVariantClear(
                ref value);
        }
    }

    private static void ReleaseComObject(
        object? value)
    {
        if (value != null
            && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(
                value);
        }
    }

    private enum AppDocumentListType
    {
        Recent = 0,
        Frequent = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        internal Guid FormatId;
        internal uint PropertyId;

        internal PropertyKey(
            Guid formatId,
            uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(
        LayoutKind.Explicit,
        Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        internal ushort Type;
        [FieldOffset(8)]
        internal IntPtr Pointer;
    }

    [ComImport]
    [Guid(
        "86BEC222-30F2-47E0-9F25-60D11CD75C28")]
    private class
        ApplicationDocumentListsComObject
    {
    }

    [ComImport]
    [Guid(
        "3C594F9F-9F30-47A1-979A-C9E83D3D0A06")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface
        IApplicationDocumentLists
    {
        [PreserveSig]
        int SetAppId(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string applicationUserModelId);

        [PreserveSig]
        int GetList(
            AppDocumentListType listType,
            uint desiredCount,
            ref Guid interfaceId,
            [MarshalAs(
                UnmanagedType.Interface)]
            out object? result);
    }

    [ComImport]
    [Guid(
        "92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        [PreserveSig]
        int GetCount(
            out uint objectCount);

        [PreserveSig]
        int GetAt(
            uint index,
            ref Guid interfaceId,
            [MarshalAs(
                UnmanagedType.Interface)]
            out object? value);
    }

    [ComImport]
    [Guid(
        "43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(
            IntPtr bindContext,
            ref Guid handlerId,
            ref Guid interfaceId,
            out IntPtr result);

        [PreserveSig]
        int GetParent(
            out IShellItem? parent);

        [PreserveSig]
        int GetDisplayName(
            uint displayNameType,
            out IntPtr name);

        [PreserveSig]
        int GetAttributes(
            uint attributeMask,
            out uint attributes);

        [PreserveSig]
        int Compare(
            IShellItem other,
            uint hint,
            out int order);
    }

    [ComImport]
    [Guid(
        "000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out,
             MarshalAs(
                 UnmanagedType.LPWStr)]
            StringBuilder path,
            int pathMax,
            IntPtr findData,
            uint flags);
        void GetIDList(
            out IntPtr itemIdList);
        void SetIDList(
            IntPtr itemIdList);
        void GetDescription(
            [Out,
             MarshalAs(
                 UnmanagedType.LPWStr)]
            StringBuilder name,
            int nameMax);
        void SetDescription(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string name);
        void GetWorkingDirectory(
            [Out,
             MarshalAs(
                 UnmanagedType.LPWStr)]
            StringBuilder directory,
            int directoryMax);
        void SetWorkingDirectory(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string directory);
        void GetArguments(
            [Out,
             MarshalAs(
                 UnmanagedType.LPWStr)]
            StringBuilder arguments,
            int argumentsMax);
        void SetArguments(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string arguments);
        void GetHotkey(
            out short hotkey);
        void SetHotkey(
            short hotkey);
        void GetShowCmd(
            out int showCommand);
        void SetShowCmd(
            int showCommand);
        void GetIconLocation(
            [Out,
             MarshalAs(
                 UnmanagedType.LPWStr)]
            StringBuilder iconPath,
            int iconPathMax,
            out int iconIndex);
        void SetIconLocation(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string iconPath,
            int iconIndex);
        void SetRelativePath(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string path,
            uint reserved);
        void Resolve(
            IntPtr window,
            uint flags);
        void SetPath(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string path);
    }

    [ComImport]
    [Guid(
        "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(
            out uint propertyCount);
        [PreserveSig]
        int GetAt(
            uint propertyIndex,
            out PropertyKey key);
        [PreserveSig]
        int GetValue(
            ref PropertyKey key,
            out PropVariant value);
        [PreserveSig]
        int SetValue(
            ref PropertyKey key,
            ref PropVariant value);
        [PreserveSig]
        int Commit();
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int
            PropVariantClear(
                ref PropVariant value);
    }
}
