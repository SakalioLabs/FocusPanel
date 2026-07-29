using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FocusPanel.Models;

namespace FocusPanel.Services;

internal readonly record struct ResolvedAppIdentity(
    string Key,
    string? ApplicationUserModelId,
    string? ExecutablePath);

internal interface IAppIdentityResolver
{
    ResolvedAppIdentity ResolveLaunch(AppLaunchItem app);
    ResolvedAppIdentity ResolveWindow(IntPtr window, uint processId, string? executablePath);
}

internal interface IAppIdentityNative
{
    string? GetWindowApplicationUserModelId(IntPtr window);
    string? GetProcessApplicationUserModelId(uint processId);
    ShortcutIdentity ResolveShortcut(string shortcutPath);
}

internal readonly record struct ShortcutIdentity(
    string? ApplicationUserModelId,
    string? ExecutablePath);

internal sealed class AppIdentityResolver : IAppIdentityResolver
{
    private readonly IAppIdentityNative _native;

    internal AppIdentityResolver() : this(new WindowsAppIdentityNative())
    {
    }

    internal AppIdentityResolver(IAppIdentityNative native)
    {
        _native = native;
    }

    public ResolvedAppIdentity ResolveLaunch(AppLaunchItem app)
    {
        string? aumid = null;
        string? executablePath = null;
        if (app.LaunchKind == AppLaunchKind.ShellApp)
        {
            aumid = app.LaunchTarget;
        }
        else if (app.LaunchKind == AppLaunchKind.Shortcut)
        {
            ShortcutIdentity shortcut = _native.ResolveShortcut(app.LaunchTarget);
            aumid = shortcut.ApplicationUserModelId;
            executablePath = shortcut.ExecutablePath;
        }
        else if (app.LaunchKind == AppLaunchKind.Executable)
        {
            executablePath = app.LaunchTarget;
        }

        string key = BuildKey(aumid, executablePath)
            ?? $"launch:{(int)app.LaunchKind}:{NormalizeText(app.LaunchTarget)}:{NormalizeText(app.Arguments)}";
        return new ResolvedAppIdentity(key, NormalizeOptional(aumid), NormalizePath(executablePath));
    }

    public ResolvedAppIdentity ResolveWindow(
        IntPtr window,
        uint processId,
        string? executablePath)
    {
        string? aumid = _native.GetWindowApplicationUserModelId(window);
        if (string.IsNullOrWhiteSpace(aumid))
            aumid = _native.GetProcessApplicationUserModelId(processId);

        string key = BuildKey(aumid, executablePath)
            ?? BuildTemporaryWindowKey(
                window,
                processId);
        return new ResolvedAppIdentity(key, NormalizeOptional(aumid), NormalizePath(executablePath));
    }

    internal static string BuildTemporaryWindowKey(
        IntPtr window,
        uint processId) =>
        processId != 0
            ? $"window:{processId}"
            : $"window-handle:{window.ToInt64():X}";

    internal static string? BuildKey(string? aumid, string? executablePath)
    {
        string? normalizedAumid = NormalizeOptional(aumid);
        if (normalizedAumid != null)
            return $"aumid:{normalizedAumid.ToLowerInvariant()}";
        string? normalizedPath = NormalizePath(executablePath);
        return normalizedPath == null ? null : $"exe:{normalizedPath.ToLowerInvariant()}";
    }

    internal static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return expanded;
        }
    }

    private static string NormalizeText(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class WindowsAppIdentityNative : IAppIdentityNative
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;
    private const ushort VtLpwstr = 31;
    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    public string? GetWindowApplicationUserModelId(IntPtr window)
    {
        IPropertyStore? store = null;
        try
        {
            Guid iid = typeof(IPropertyStore).GUID;
            if (NativeMethods.SHGetPropertyStoreForWindow(window, ref iid, out store) != 0
                || store == null)
            {
                return null;
            }
            return ReadStringProperty(store, AppUserModelIdKey);
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseComObject(store);
        }
    }

    public string? GetProcessApplicationUserModelId(uint processId)
    {
        IntPtr process = NativeMethods.OpenProcess(
            ProcessQueryLimitedInformation,
            false,
            processId);
        if (process == IntPtr.Zero)
            return null;
        try
        {
            uint length = 0;
            int result = NativeMethods.GetApplicationUserModelId(
                process,
                ref length,
                null);
            if (result != ErrorInsufficientBuffer || length == 0)
                return null;
            var value = new StringBuilder((int)length);
            result = NativeMethods.GetApplicationUserModelId(process, ref length, value);
            return result == 0 ? value.ToString() : null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    public ShortcutIdentity ResolveShortcut(string shortcutPath)
    {
        object? shellLinkObject = null;
        try
        {
            shellLinkObject = new ShellLinkComObject();
            var persistFile = (IPersistFile)shellLinkObject;
            persistFile.Load(shortcutPath, 0);

            string? aumid = ReadStringProperty((IPropertyStore)shellLinkObject, AppUserModelIdKey);
            var target = new StringBuilder(1024);
            ((IShellLinkW)shellLinkObject).GetPath(target, target.Capacity, IntPtr.Zero, 0x0004);
            return new ShortcutIdentity(aumid, target.Length == 0 ? null : target.ToString());
        }
        catch
        {
            return default;
        }
        finally
        {
            ReleaseComObject(shellLinkObject);
        }
    }

    private static string? ReadStringProperty(IPropertyStore store, PropertyKey key)
    {
        PropVariant value = default;
        try
        {
            store.GetValue(ref key, out value);
            return value.Type == VtLpwstr && value.Pointer != IntPtr.Zero
                ? Marshal.PtrToStringUni(value.Pointer)
                : null;
        }
        finally
        {
            NativeMethods.PropVariantClear(ref value);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        internal Guid FormatId;
        internal uint PropertyId;
        internal PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] internal ushort Type;
        [FieldOffset(8)] internal IntPtr Pointer;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkComObject
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int pathMax, IntPtr findData, uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int nameMax);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int directoryMax);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int argumentsMax);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathMax, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        internal static extern int SHGetPropertyStoreForWindow(
            IntPtr window,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetApplicationUserModelId(
            IntPtr process,
            ref uint applicationUserModelIdLength,
            StringBuilder? applicationUserModelId);

        [DllImport("ole32.dll")]
        internal static extern int PropVariantClear(ref PropVariant value);
    }
}
