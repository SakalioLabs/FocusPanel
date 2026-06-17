using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusPanel.Helpers;

public static class DesktopHelper
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const int LVM_DELETEITEM = LVM_FIRST + 8;
    private const int LVM_SETITEMPOSITION = LVM_FIRST + 15;
    private const int LVM_GETITEMTEXTW = LVM_FIRST + 115;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;
    private const int LVIF_TEXT = 0x0001;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void ToggleDesktopIcons(bool show)
    {
        IntPtr hWnd = GetDesktopListViewHandle();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, show ? SW_SHOW : SW_HIDE);
        }
    }

    public static bool IsDesktopIconsVisible()
    {
        IntPtr hWnd = GetDesktopListViewHandle();
        if (hWnd != IntPtr.Zero)
        {
            return IsWindowVisible(hWnd);
        }
        return true;
    }

    public static bool HideDesktopItem(string fileName)
    {
        IntPtr hWnd = GetDesktopListViewHandle();
        if (hWnd == IntPtr.Zero || string.IsNullOrWhiteSpace(fileName))
            return false;

        var names = new[]
        {
            fileName,
            string.IsNullOrEmpty(Path.GetExtension(fileName))
                ? fileName
                : Path.GetFileNameWithoutExtension(fileName)
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            int index = FindDesktopItemIndex(hWnd, name);
            if (index >= 0)
            {
                SendMessage(hWnd, LVM_DELETEITEM, new IntPtr(index), IntPtr.Zero);
                return true;
            }
        }

        return false;
    }

    public static bool ShowDesktopItem(string fileName)
    {
        RefreshDesktop();

        IntPtr hWnd = GetDesktopListViewHandle();
        if (hWnd == IntPtr.Zero || string.IsNullOrWhiteSpace(fileName))
            return false;

        var names = new[]
        {
            fileName,
            string.IsNullOrEmpty(Path.GetExtension(fileName))
                ? fileName
                : Path.GetFileNameWithoutExtension(fileName)
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            int index = FindDesktopItemIndex(hWnd, name);
            if (index >= 0)
            {
                SendMessage(hWnd, LVM_SETITEMPOSITION, new IntPtr(index), MakeLParam(40, 40));
                return true;
            }
        }

        return false;
    }

    public static void RefreshDesktop()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static IntPtr GetDesktopListViewHandle()
    {
        IntPtr progman = FindWindow("Progman", "Program Manager");
        IntPtr shellDllDefView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (shellDllDefView == IntPtr.Zero)
        {
            IntPtr workerW = IntPtr.Zero;
            int retryCount = 0;
            const int MAX_RETRIES = 20;

            while (retryCount < MAX_RETRIES)
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                if (workerW == IntPtr.Zero) break;

                shellDllDefView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDllDefView != IntPtr.Zero) break;

                retryCount++;
            }
        }

        if (shellDllDefView != IntPtr.Zero)
        {
            return FindWindowEx(shellDllDefView, IntPtr.Zero, "SysListView32", "FolderView");
        }

        return IntPtr.Zero;
    }

    private static int FindDesktopItemIndex(IntPtr listViewHandle, string targetText)
    {
        int count = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (count <= 0) return -1;

        GetWindowThreadProcessId(listViewHandle, out uint processId);
        if (processId == 0) return -1;

        IntPtr process = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION, false, processId);
        if (process == IntPtr.Zero) return -1;

        const int textBytes = 520;
        int lvItemSize = Marshal.SizeOf<LVITEM64>();
        IntPtr remoteText = IntPtr.Zero;
        IntPtr remoteItem = IntPtr.Zero;

        try
        {
            remoteText = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)textBytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            remoteItem = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)lvItemSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteText == IntPtr.Zero || remoteItem == IntPtr.Zero) return -1;

            for (int i = 0; i < count; i++)
            {
                var item = new LVITEM64
                {
                    mask = LVIF_TEXT,
                    iItem = i,
                    iSubItem = 0,
                    pszText = remoteText,
                    cchTextMax = textBytes / 2
                };

                byte[] itemBytes = StructureToBytes(item);
                if (!WriteProcessMemory(process, remoteItem, itemBytes, itemBytes.Length, out _))
                    continue;

                SendMessage(listViewHandle, LVM_GETITEMTEXTW, new IntPtr(i), remoteItem);

                byte[] textBuffer = new byte[textBytes];
                if (!ReadProcessMemory(process, remoteText, textBuffer, textBuffer.Length, out _))
                    continue;

                string text = Encoding.Unicode.GetString(textBuffer).TrimEnd('\0');
                if (string.Equals(text, targetText, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }
        finally
        {
            if (remoteText != IntPtr.Zero) VirtualFreeEx(process, remoteText, UIntPtr.Zero, MEM_RELEASE);
            if (remoteItem != IntPtr.Zero) VirtualFreeEx(process, remoteItem, UIntPtr.Zero, MEM_RELEASE);
            CloseHandle(process);
        }

        return -1;
    }

    private static byte[] StructureToBytes<T>(T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] bytes = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static IntPtr MakeLParam(int low, int high)
    {
        return new IntPtr((high << 16) | (low & 0xffff));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LVITEM64
    {
        public int mask;
        public int iItem;
        public int iSubItem;
        public int state;
        public int stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public int cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }
}
