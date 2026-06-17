using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
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

    public static ImageSource GetIcon(string path, bool large = true)
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

        // Try to resolve shortcut target to get clean icon without overlay
        if (System.IO.Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string target = ResolveShortcut(path);
            if (!string.IsNullOrEmpty(target) && (System.IO.File.Exists(target) || System.IO.Directory.Exists(target)))
            {
                path = target;
            }
        }

        if (large)
        {
            var highResIcon = TryGetShellImageListIcon(path);
            if (highResIcon != null)
            {
                lock (_cacheLock)
                {
                    if (_iconCache.Count >= MaxCacheSize)
                    {
                        var keysToRemove = new List<string>();
                        int count = 0;
                        foreach (var key in _iconCache.Keys)
                        {
                            if (count++ > MaxCacheSize / 2) break;
                            keysToRemove.Add(key);
                        }
                        foreach (var key in keysToRemove) _iconCache.Remove(key);
                    }
                    _iconCache[cacheKey] = highResIcon;
                }
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
        lock (_cacheLock)
        {
            // 防止缓存无限增长
            if (_iconCache.Count >= MaxCacheSize)
            {
                // 清除一半缓存
                var keysToRemove = new List<string>();
                int count = 0;
                foreach (var key in _iconCache.Keys)
                {
                    if (count++ > MaxCacheSize / 2)
                        break;
                    keysToRemove.Add(key);
                }
                foreach (var key in keysToRemove)
                {
                    _iconCache.Remove(key);
                }
            }
            _iconCache[cacheKey] = icon;
        }

        // Cleanup
        DestroyIcon(shinfo.hIcon);

        return icon;
    }

    private static ImageSource TryGetShellImageListIcon(string path)
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

    private static string ResolveShortcut(string shortcutPath)
    {
        // Simple WScript resolution using dynamic to avoid reference
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
