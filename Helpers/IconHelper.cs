using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FocusPanel.Helpers;

public static class IconHelper
{
    // 图标缓存 - 避免重复解析
    private static readonly Dictionary<string, ImageSource> _iconCache = new();
    private static readonly object _cacheLock = new();
    private const int MaxCacheSize = 500; // 限制缓存大小

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    };

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0; // 32x32
    private const uint SHGFI_SMALLICON = 0x1; // 16x16
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint SHGFI_LINKOVERLAY = 0x000008000; // Add link overlay
    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int SHIL_JUMBO = 0x4;
    private const int ILD_TRANSPARENT = 0x1;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        uint nIconSize);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
            int capacity,
            IntPtr findData,
            uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name,
            int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,
            int capacity);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
            int capacity);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath,
            int capacity,
            out int iconIndex);
        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)] string iconPath,
            int iconIndex);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig]
        int IsDirty();
        void Load(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            uint mode);
        void Save(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile(
            [MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }

    public static ImageSource? GetIcon(string path, bool large = true)
    {
        // 生成缓存键
        string cacheKey = $"{path}_{large}";

        // 检查缓存
        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            {
                return cachedIcon;
            }
        }

        ImageSource? explicitIcon =
            TryGetExplicitCustomIcon(path, large);
        if (explicitIcon != null)
        {
            CacheIcon(cacheKey, explicitIcon);
            return explicitIcon;
        }

        if (large)
        {
            var highResIcon = TryGetShellImageListIcon(path);
            if (highResIcon != null)
            {
                CacheIcon(cacheKey, highResIcon);
                return highResIcon;
            }
        }

        var shinfo = new SHFILEINFO();
        uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);

        // Use file attributes if file doesn't exist (for extension lookup)
        // Or if we resolved a target but it's not accessible? No, SHGetFileInfo handles paths fine.
        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

        if (shinfo.hIcon == IntPtr.Zero) return null;

        var icon = Imaging.CreateBitmapSourceFromHIcon(
            shinfo.hIcon,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        icon.Freeze(); // Make it cross-thread accessible

        // 缓存图标
        CacheIcon(cacheKey, icon);

        // Cleanup
        DestroyIcon(shinfo.hIcon);

        return icon;
    }

    private static ImageSource? TryGetExplicitCustomIcon(
        string itemPath,
        bool large)
    {
        if (!TryResolveCustomIconLocation(
                itemPath,
                out string iconPath,
                out int iconIndex))
        {
            return null;
        }

        IntPtr largeIcon = IntPtr.Zero;
        IntPtr smallIcon = IntPtr.Zero;
        try
        {
            uint size = large
                ? 64u | (16u << 16)
                : 16u | (16u << 16);
            int result = SHDefExtractIcon(
                iconPath,
                iconIndex,
                0,
                out largeIcon,
                out smallIcon,
                size);
            IntPtr handle = large
                ? largeIcon
                : smallIcon;
            if (result != 0 || handle == IntPtr.Zero)
                return null;

            BitmapSource source =
                Imaging.CreateBitmapSourceFromHIcon(
                    handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (largeIcon != IntPtr.Zero)
                DestroyIcon(largeIcon);
            if (smallIcon != IntPtr.Zero
                && smallIcon != largeIcon)
            {
                DestroyIcon(smallIcon);
            }
        }
    }

    internal static bool TryResolveCustomIconLocation(
        string itemPath,
        out string iconPath,
        out int iconIndex)
    {
        iconPath = string.Empty;
        iconIndex = 0;
        try
        {
            string extension = Path.GetExtension(itemPath);
            if (extension.Equals(
                    ".lnk",
                    StringComparison.OrdinalIgnoreCase))
            {
                return TryResolveShortcutIcon(
                    itemPath,
                    out iconPath,
                    out iconIndex);
            }
            if (extension.Equals(
                    ".url",
                    StringComparison.OrdinalIgnoreCase))
            {
                return TryResolveIniIcon(
                    itemPath,
                    itemPath,
                    out iconPath,
                    out iconIndex);
            }
            if (Directory.Exists(itemPath))
            {
                return TryResolveIniIcon(
                    Path.Combine(itemPath, "desktop.ini"),
                    itemPath,
                    out iconPath,
                    out iconIndex);
            }
        }
        catch
        {
            // A malformed shortcut must fall back to the normal Shell icon.
        }
        return false;
    }

    private static bool TryResolveShortcutIcon(
        string shortcutPath,
        out string iconPath,
        out int iconIndex)
    {
        iconPath = string.Empty;
        iconIndex = 0;
        object? shellLink = null;
        try
        {
            shellLink = new ShellLink();
            ((IPersistFile)shellLink).Load(
                shortcutPath,
                0);
            var buffer = new StringBuilder(32768);
            ((IShellLinkW)shellLink).GetIconLocation(
                buffer,
                buffer.Capacity,
                out iconIndex);
            iconPath = NormalizeIconPath(
                buffer.ToString(),
                shortcutPath);
            return !string.IsNullOrWhiteSpace(iconPath);
        }
        finally
        {
            if (shellLink != null
                && Marshal.IsComObject(shellLink))
            {
                Marshal.FinalReleaseComObject(shellLink);
            }
        }
    }

    private static bool TryResolveIniIcon(
        string iniPath,
        string itemPath,
        out string iconPath,
        out int iconIndex)
    {
        iconPath = string.Empty;
        iconIndex = 0;
        if (!File.Exists(iniPath))
            return false;

        string? iconFile = null;
        foreach (string rawLine in File.ReadLines(iniPath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith(
                    "IconResource=",
                    StringComparison.OrdinalIgnoreCase))
            {
                string value = line["IconResource=".Length..];
                int separator = value.LastIndexOf(',');
                if (separator > 0
                    && int.TryParse(
                        value[(separator + 1)..].Trim(),
                        out int parsedIndex))
                {
                    iconIndex = parsedIndex;
                    value = value[..separator];
                }
                iconFile = value;
                break;
            }
            if (line.StartsWith(
                    "IconFile=",
                    StringComparison.OrdinalIgnoreCase))
            {
                iconFile = line["IconFile=".Length..];
            }
            else if (line.StartsWith(
                         "IconIndex=",
                         StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(
                    line["IconIndex=".Length..].Trim(),
                    out iconIndex);
            }
        }

        iconPath = NormalizeIconPath(
            iconFile,
            itemPath);
        return !string.IsNullOrWhiteSpace(iconPath);
    }

    internal static string NormalizeIconPath(
        string? iconPath,
        string itemPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return string.Empty;

        string normalized = Environment
            .ExpandEnvironmentVariables(iconPath.Trim().Trim('"'));
        if (!Path.IsPathRooted(normalized))
        {
            string? baseDirectory = Directory.Exists(itemPath)
                ? itemPath
                : Path.GetDirectoryName(itemPath);
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                normalized = Path.Combine(
                    baseDirectory,
                    normalized);
            }
        }
        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }
    }

    private static void CacheIcon(
        string cacheKey,
        ImageSource icon)
    {
        lock (_cacheLock)
        {
            if (_iconCache.Count >= MaxCacheSize)
            {
                var keysToRemove = new List<string>();
                int count = 0;
                foreach (string key in _iconCache.Keys)
                {
                    if (count++ > MaxCacheSize / 2)
                        break;
                    keysToRemove.Add(key);
                }
                foreach (string key in keysToRemove)
                    _iconCache.Remove(key);
            }
            _iconCache[cacheKey] = icon;
        }
    }

    private static ImageSource? TryGetShellImageListIcon(string path)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;
            if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
                flags |= SHGFI_USEFILEATTRIBUTES;

            SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (shinfo.iIcon < 0) return null;

            Guid imageListGuid = new("46EB5926-582E-4017-9FDF-E8998DAA0950");
            if (SHGetImageList(SHIL_JUMBO, ref imageListGuid, out var imageList) != 0 || imageList == null)
            {
                if (SHGetImageList(SHIL_EXTRALARGE, ref imageListGuid, out imageList) != 0 || imageList == null)
                    return null;
            }

            IntPtr hIcon = IntPtr.Zero;
            if (imageList.GetIcon(shinfo.iIcon, ILD_TRANSPARENT, ref hIcon) != 0 || hIcon == IntPtr.Zero)
                return null;

            try
            {
                var icon = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                icon.Freeze();
                return icon;
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    // 清除指定路径的缓存（用于文件变更时）
    public static void ClearCache(string path)
    {
        lock (_cacheLock)
        {
            var keysToRemove = new List<string>();
            foreach (var key in _iconCache.Keys)
            {
                if (key.StartsWith(path))
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                _iconCache.Remove(key);
            }
        }
    }

    // 清除所有缓存
    public static void ClearAllCache()
    {
        lock (_cacheLock)
        {
            _iconCache.Clear();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
