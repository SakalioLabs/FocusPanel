using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace FocusPanel.Services;

public interface IDesktopItemVisibilityService
{
    bool Exists(string path);
    FileAttributes GetAttributes(string path);
    void SetAttributes(string path, FileAttributes attributes);
    string? TryGetIdentity(string path);
    void NotifyAttributesChanged(string path);
    bool ShowsProtectedSystemFiles { get; }
}

public static class DesktopItemAttributePolicy
{
    public static FileAttributes Collect(FileAttributes original)
        => (original & ~FileAttributes.Normal)
            | FileAttributes.Hidden
            | FileAttributes.System;

    public static FileAttributes Restore(long original)
        => (FileAttributes)original;
}

public sealed class WindowsDesktopItemVisibilityService : IDesktopItemVisibilityService
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint ShcneAttributes = 0x00000800;
    private const uint ShcneUpdateDir = 0x00001000;
    private const uint ShcneUpdateItem = 0x00002000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlushNoWait = 0x00002000;

    public bool ShowsProtectedSystemFiles
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                return Convert.ToInt32(key?.GetValue("ShowSuperHidden", 0)) == 1;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public void SetAttributes(string path, FileAttributes attributes)
        => File.SetAttributes(path, attributes);

    public string? TryGetIdentity(string path)
    {
        if (!Exists(path))
            return null;

        using SafeFileHandle handle = CreateFile(
            path,
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out ByHandleFileInformation info))
            return null;

        ulong index = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
        return $"{info.VolumeSerialNumber:X8}:{index:X16}";
    }

    public void NotifyAttributesChanged(string path)
    {
        uint flags = ShcnfPathW | ShcnfFlushNoWait;
        SHChangeNotify(ShcneAttributes, flags, path, null);
        SHChangeNotify(ShcneUpdateItem, flags, path, null);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            SHChangeNotify(ShcneUpdateDir, flags, directory, null);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string? item1, string? item2);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
