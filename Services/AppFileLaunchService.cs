using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal sealed record AppFileLaunchResult(
    int RequestedCount,
    int EligibleCount,
    bool LaunchSucceeded,
    int IgnoredCount,
    string? FailureReason)
{
    internal int OpenedCount =>
        LaunchSucceeded
            ? EligibleCount
            : 0;

    internal bool IsCompleteSuccess =>
        LaunchSucceeded
        && EligibleCount > 0
        && IgnoredCount == 0;

    internal static AppFileLaunchResult Rejected(
        string reason,
        int requestedCount = 0) =>
        new(
            requestedCount,
            0,
            false,
            requestedCount,
            reason);
}

internal interface IAppFileLaunchNative
{
    bool PathExists(string path);
    bool TryStart(ProcessStartInfo request);
    bool TryActivatePackaged(
        string applicationUserModelId,
        IReadOnlyList<string> paths);
}

internal interface IAppFileLaunchService
{
    Task<AppFileLaunchResult> OpenAsync(
        AppLaunchItem app,
        IEnumerable<string> paths);

    Task CompleteAsync();
}

internal readonly record struct
    AppFileDropPathSelection(
        int RequestedCount,
        IReadOnlyList<string> Paths,
        int IgnoredCount);

internal static class AppFileDropPolicy
{
    internal const int MaximumPathCount = 32;

    internal static AppFileDropPathSelection
        SelectPaths(
            IEnumerable<string>? source,
            int limit = MaximumPathCount)
    {
        if (source == null)
        {
            return new AppFileDropPathSelection(
                0,
                Array.Empty<string>(),
                0);
        }

        int boundedLimit = Math.Clamp(
            limit,
            0,
            MaximumPathCount);
        var paths = new List<string>(
            boundedLimit);
        var identities = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int requested = 0;
        int ignored = 0;
        foreach (string? sourcePath in source)
        {
            requested++;
            string path =
                sourcePath?.Trim()
                ?? string.Empty;
            if (path.Length == 0
                || !Path.IsPathFullyQualified(path))
            {
                ignored++;
                continue;
            }

            if (!identities.Add(path))
                continue;
            if (paths.Count >= boundedLimit)
            {
                ignored++;
                continue;
            }

            paths.Add(path);
        }

        return new AppFileDropPathSelection(
            requested,
            paths,
            ignored);
    }

    internal static bool TryBuildDesktopRequest(
        AppLaunchItem app,
        IReadOnlyList<string> paths,
        out ProcessStartInfo? request)
    {
        ArgumentNullException.ThrowIfNull(app);
        request = null;
        string target =
            app.LaunchTarget?.Trim()
            ?? string.Empty;
        if (target.Length == 0
            || paths.Count == 0)
        {
            return false;
        }

        string pathArguments =
            string.Join(
                " ",
                paths.Select(
                    QuoteWindowsArgument));
        string arguments =
            string.IsNullOrWhiteSpace(
                app.Arguments)
                ? pathArguments
                : app.Arguments.Trim()
                  + " "
                  + pathArguments;
        request = new ProcessStartInfo
        {
            FileName = target,
            Arguments = arguments,
            UseShellExecute = true
        };
        return true;
    }

    internal static string QuoteWindowsArgument(
        string value)
    {
        value ??= string.Empty;
        var result =
            new System.Text.StringBuilder(
                value.Length + 2);
        result.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append(
                    '\\',
                    backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append(
                '\\',
                backslashes);
            backslashes = 0;
            result.Append(character);
        }

        result.Append(
            '\\',
            backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}

internal sealed class AppFileLaunchService :
    IAppFileLaunchService
{
    private readonly IAppFileLaunchNative
        _native;
    private readonly InFlightTaskTracker
        _tasks = new();

    internal AppFileLaunchService()
        : this(
            new WindowsAppFileLaunchNative())
    {
    }

    internal AppFileLaunchService(
        IAppFileLaunchNative native)
    {
        _native =
            native
            ?? throw new ArgumentNullException(
                nameof(native));
    }

    public Task<AppFileLaunchResult> OpenAsync(
        AppLaunchItem app,
        IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(app);
        AppLaunchItem detached =
            CaptureLaunch(app);
        AppFileDropPathSelection selection =
            AppFileDropPolicy.SelectPaths(
                paths);
        Task<AppFileLaunchResult>? task =
            _tasks.TryStart(
                () =>
                    Task.Run(
                        () =>
                            OpenCore(
                                detached,
                                selection)));
        return task
            ?? Task.FromResult(
                AppFileLaunchResult.Rejected(
                    "FocusPanel 正在退出，未再接收文件。",
                    selection.RequestedCount));
    }

    public Task CompleteAsync() =>
        _tasks.CompleteAsync();

    private AppFileLaunchResult OpenCore(
        AppLaunchItem app,
        AppFileDropPathSelection selection)
    {
        if (selection.RequestedCount == 0)
        {
            return AppFileLaunchResult.Rejected(
                "没有收到可打开的文件。");
        }

        var eligible =
            new List<string>(
                selection.Paths.Count);
        int ignored =
            selection.IgnoredCount;
        foreach (string path
                 in selection.Paths)
        {
            bool exists;
            try
            {
                exists =
                    _native.PathExists(path);
            }
            catch
            {
                exists = false;
            }

            if (exists)
                eligible.Add(path);
            else
                ignored++;
        }

        if (eligible.Count == 0)
        {
            return new AppFileLaunchResult(
                selection.RequestedCount,
                0,
                false,
                ignored,
                "文件可能已移动、删除或当前无法访问。");
        }

        bool launched;
        try
        {
            launched =
                app.LaunchKind
                    == AppLaunchKind.ShellApp
                && AppLaunchRequestBuilder
                    .IsApplicationUserModelId(
                        app.LaunchTarget)
                    ? _native
                        .TryActivatePackaged(
                            app.LaunchTarget
                                .Trim(),
                            eligible)
                    : AppFileDropPolicy
                        .TryBuildDesktopRequest(
                            app,
                            eligible,
                            out ProcessStartInfo?
                                request)
                      && request != null
                      && _native.TryStart(
                          request);
        }
        catch
        {
            launched = false;
        }

        return new AppFileLaunchResult(
            selection.RequestedCount,
            eligible.Count,
            launched,
            ignored,
            launched
                ? null
                : "目标应用拒绝了文件，或它已不再可用。");
    }

    private static AppLaunchItem CaptureLaunch(
        AppLaunchItem source) =>
        new()
        {
            DisplayName =
                source.DisplayName,
            LaunchKind =
                source.LaunchKind,
            LaunchTarget =
                source.LaunchTarget,
            Arguments =
                source.Arguments,
            IconKey =
                source.IconKey,
            IdentityKey =
                source.IdentityKey,
            ApplicationUserModelId =
                source.ApplicationUserModelId
        };
}

internal sealed class
    WindowsAppFileLaunchNative :
        IAppFileLaunchNative
{
    private static readonly Guid
        IidShellItemArray =
            new(
                "B63EA76D-1F85-456F-A19C-48159EFA858B");

    public bool PathExists(
        string path) =>
        File.Exists(path)
        || Directory.Exists(path);

    public bool TryStart(
        ProcessStartInfo request) =>
        AppLaunchExecution.TryStart(
            request);

    public bool TryActivatePackaged(
        string applicationUserModelId,
        IReadOnlyList<string> paths)
    {
        if (string.IsNullOrWhiteSpace(
                applicationUserModelId)
            || paths.Count == 0)
        {
            return false;
        }

        var itemIdLists =
            new List<IntPtr>(
                paths.Count);
        IntPtr pointerArray =
            IntPtr.Zero;
        IShellItemArray? shellItems =
            null;
        object? managerObject = null;
        try
        {
            foreach (string path
                     in paths)
            {
                if (NativeMethods
                        .SHParseDisplayName(
                            path,
                            IntPtr.Zero,
                            out IntPtr itemIdList,
                            0,
                            out _)
                    < 0
                    || itemIdList
                        == IntPtr.Zero)
                {
                    return false;
                }
                itemIdLists.Add(
                    itemIdList);
            }

            pointerArray =
                Marshal.AllocHGlobal(
                    checked(
                        IntPtr.Size
                        * itemIdLists.Count));
            Marshal.Copy(
                itemIdLists.ToArray(),
                0,
                pointerArray,
                itemIdLists.Count);
            if (NativeMethods
                    .SHCreateShellItemArrayFromIDLists(
                        (uint)itemIdLists.Count,
                        pointerArray,
                        out shellItems)
                < 0
                || shellItems == null)
            {
                return false;
            }

            managerObject =
                new
                    ApplicationActivationManagerComObject();
            var manager =
                (IApplicationActivationManager)
                    managerObject;
            _ = NativeMethods
                .CoAllowSetForegroundWindow(
                    managerObject,
                    IntPtr.Zero);
            return manager.ActivateForFile(
                       applicationUserModelId,
                       shellItems,
                       string.Empty,
                       out _)
                   >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shellItems != null
                && Marshal.IsComObject(
                    shellItems))
            {
                Marshal.FinalReleaseComObject(
                    shellItems);
            }
            if (managerObject != null
                && Marshal.IsComObject(
                    managerObject))
            {
                Marshal.FinalReleaseComObject(
                    managerObject);
            }
            if (pointerArray != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    pointerArray);
            }
            foreach (IntPtr itemIdList
                     in itemIdLists)
            {
                Marshal.FreeCoTaskMem(
                    itemIdList);
            }
        }
    }

    [ComImport]
    [Guid(
        "45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class
        ApplicationActivationManagerComObject
    {
    }

    [ComImport]
    [Guid(
        "2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface
        IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string applicationUserModelId,
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string? arguments,
            uint options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string applicationUserModelId,
            [MarshalAs(
                UnmanagedType.Interface)]
            IShellItemArray itemArray,
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string? verb,
            out uint processId);

        [PreserveSig]
        int ActivateForProtocol(
            [MarshalAs(
                UnmanagedType.LPWStr)]
            string applicationUserModelId,
            [MarshalAs(
                UnmanagedType.Interface)]
            IShellItemArray itemArray,
            out uint processId);
    }

    [ComImport]
    [Guid(
        "B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(
        ComInterfaceType
            .InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
    }

    private static class NativeMethods
    {
        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode,
            PreserveSig = true)]
        internal static extern int
            SHParseDisplayName(
                string name,
                IntPtr bindContext,
                out IntPtr itemIdList,
                uint attributesIn,
                out uint attributes);

        [DllImport(
            "shell32.dll",
            PreserveSig = true)]
        internal static extern int
            SHCreateShellItemArrayFromIDLists(
                uint count,
                IntPtr itemIdLists,
                [MarshalAs(
                    UnmanagedType.Interface)]
                out IShellItemArray?
                    shellItemArray);

        [DllImport(
            "ole32.dll",
            PreserveSig = true)]
        internal static extern int
            CoAllowSetForegroundWindow(
                [MarshalAs(
                    UnmanagedType.IUnknown)]
                object unknown,
                IntPtr reserved);
    }
}
