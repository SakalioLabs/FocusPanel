using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly IMMDeviceEnumerator? _deviceEnumerator;

    public SystemStatusService()
    {
        try
        {
            _deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        }
        catch
        {
            _deviceEnumerator = null;
        }
    }

    public float MasterVolume
    {
        get
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = GetDefaultAudioEndpoint();
                if (endpoint == null)
                    return 0;
                endpoint.GetMasterVolumeLevelScalar(out float volume);
                return volume;
            }
            catch
            {
                return 0;
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        }
        set
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = GetDefaultAudioEndpoint();
                if (endpoint == null)
                    return;
                Guid context = Guid.Empty;
                endpoint.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref context);
            }
            catch
            {
                // The default endpoint can disappear during device switching.
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = GetDefaultAudioEndpoint();
                if (endpoint == null)
                    return false;
                endpoint.GetMute(out bool muted);
                return muted;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        }
        set
        {
            IAudioEndpointVolume? endpoint = null;
            try
            {
                endpoint = GetDefaultAudioEndpoint();
                if (endpoint == null)
                    return;
                Guid context = Guid.Empty;
                endpoint.SetMute(value, ref context);
            }
            catch
            {
                // The default endpoint can disappear during device switching.
            }
            finally
            {
                ReleaseComObject(endpoint);
            }
        }
    }

    public bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();

    public string NetworkDisplayName
    {
        get
        {
            try
            {
                NetworkInterface? active = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(item =>
                        item.OperationalStatus == OperationalStatus.Up
                        && item.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && item.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(item =>
                        item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .FirstOrDefault();
                return active?.Name ?? "未连接";
            }
            catch
            {
                return IsNetworkAvailable ? "网络已连接" : "未连接";
            }
        }
    }

    public string NetworkDetail
    {
        get
        {
            try
            {
                NetworkInterface? active = GetActiveNetworkInterface();
                if (active == null)
                    return "当前没有可用连接";

                string? ipv4 = active.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString();
                string kind = active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    ? "Wi‑Fi"
                    : active.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                        ? "以太网"
                        : active.NetworkInterfaceType.ToString();
                return string.IsNullOrWhiteSpace(ipv4) ? kind : $"{kind} · {ipv4}";
            }
            catch
            {
                return IsNetworkAvailable ? "网络已连接" : "当前没有可用连接";
            }
        }
    }

    public string InputLanguageDisplay
    {
        get
        {
            CultureInfo? culture = GetForegroundInputCulture();
            return culture == null ? "—" : GetShortLanguageName(culture);
        }
    }

    public string InputMethodDisplay
    {
        get
        {
            IntPtr layout = GetForegroundKeyboardLayout();
            CultureInfo? culture = GetInputCulture(layout);
            if (culture?.TwoLetterISOLanguageName != "zh")
                return culture == null ? "—" : GetShortLanguageName(culture);

            var description = new StringBuilder(128);
            _ = NativeMethods.ImmGetDescription(layout, description, (uint)description.Capacity);
            string name = description.ToString();
            if (name.Contains("拼音", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Pinyin", StringComparison.OrdinalIgnoreCase))
            {
                return "拼";
            }
            if (name.Contains("五笔", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wubi", StringComparison.OrdinalIgnoreCase))
            {
                return "五";
            }
            if (name.Contains("注音", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Bopomofo", StringComparison.OrdinalIgnoreCase))
            {
                return "注";
            }
            return "中";
        }
    }

    public bool HasBattery => SystemInformation.PowerStatus.BatteryChargeStatus
        != BatteryChargeStatus.NoSystemBattery;

    public int BatteryPercent => HasBattery
        ? (int)Math.Round(SystemInformation.PowerStatus.BatteryLifePercent * 100)
        : 0;

    public bool IsCharging => HasBattery
        && (SystemInformation.PowerStatus.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;

    public bool OpenQuickSettings() => TrySendWindowsShortcut(WindowsShellAction.QuickSettings);

    public bool OpenNotifications() => TrySendWindowsShortcut(WindowsShellAction.Notifications);

    public bool OpenInputSwitcher() => TrySendWindowsShortcut(WindowsShellAction.InputSwitcher);

    public bool OpenStartMenu() => TrySendWindowsShortcut(WindowsShellAction.StartMenu);

    public bool OpenTaskView() => TrySendWindowsShortcut(WindowsShellAction.TaskView);

    public bool OpenWindowsSearch() => TrySendWindowsShortcut(WindowsShellAction.Search);

    public bool OpenWidgets() => TrySendWindowsShortcut(WindowsShellAction.Widgets);

    public bool OpenRunDialog() => TrySendWindowsShortcut(WindowsShellAction.RunDialog);

    public bool OpenManagementTool(SystemManagementTool tool)
    {
        SystemLaunchRequest request = SystemManagementToolCatalog.Get(tool);
        try
        {
            return StartManagementRequest(request.FileName, request) != null;
        }
        catch (Win32Exception ex) when (
            ex.NativeErrorCode is 2 or 3
            && !string.IsNullOrWhiteSpace(request.FallbackFileName))
        {
            try
            {
                return StartManagementRequest(request.FallbackFileName, request) != null;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static Process? StartManagementRequest(
        string fileName,
        SystemLaunchRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(request.Arguments))
            startInfo.Arguments = request.Arguments;
        if (!string.IsNullOrWhiteSpace(request.Verb))
            startInfo.Verb = request.Verb;
        return Process.Start(startInfo);
    }

    public bool OpenPowerSettings() =>
        SystemActionExecution.TryStart(
            () => OpenSystemUri("ms-settings:powersleep"));

    public bool ShowDesktop() =>
        SystemActionExecution.TryWithFallback(
            () => TrySendWindowsShortcut(WindowsShellAction.ShowDesktop),
            NativeMethods.ShowDesktopFallback);

    public bool Lock() =>
        SystemActionExecution.Try(NativeMethods.LockWorkStation);

    public bool Sleep() =>
        SystemActionExecution.Try(
            () => NativeMethods.SetSuspendState(false, false, false));

    public bool Restart() =>
        SystemActionExecution.TryStart(() => StartShutdown("/r /t 0"));

    public bool Shutdown() =>
        SystemActionExecution.TryStart(() => StartShutdown("/s /t 0"));

    private IAudioEndpointVolume? GetDefaultAudioEndpoint()
    {
        if (_deviceEnumerator == null)
            return null;

        _deviceEnumerator.GetDefaultAudioEndpoint(
            EDataFlow.Render,
            ERole.Multimedia,
            out IMMDevice device);
        try
        {
            Guid endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref endpointVolumeId, Clsctx.All, IntPtr.Zero, out object endpoint);
            return (IAudioEndpointVolume)endpoint;
        }
        finally
        {
            ReleaseComObject(device);
        }
    }

    private static NetworkInterface? GetActiveNetworkInterface()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(item =>
                item.OperationalStatus == OperationalStatus.Up
                && item.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && item.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(item =>
                item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .FirstOrDefault();

    private static IntPtr GetForegroundKeyboardLayout()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint threadId = foreground == IntPtr.Zero
            ? 0
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    private static CultureInfo? GetForegroundInputCulture() =>
        GetInputCulture(GetForegroundKeyboardLayout());

    private static CultureInfo? GetInputCulture(IntPtr layout)
    {
        try
        {
            int languageId = unchecked((ushort)(long)layout);
            return languageId == 0 ? null : CultureInfo.GetCultureInfo(languageId);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string GetShortLanguageName(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "zh" => "中",
            "ja" => "日",
            "ko" => "한",
            "en" => "EN",
            string language => language.ToUpperInvariant()
        };

    private static void StartShutdown(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static bool TrySendWindowsShortcut(WindowsShellAction action)
    {
        WindowsShellShortcut shortcut = WindowsShellShortcutMap.Get(action);
        if (!shortcut.UsesWindowsKey)
            return TrySendKey(shortcut.Key);

        var inputs = new[]
        {
            KeyboardInput.KeyDown(0x5B),
            KeyboardInput.KeyDown(shortcut.Key),
            KeyboardInput.KeyUp(shortcut.Key),
            KeyboardInput.KeyUp(0x5B)
        };
        return NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>()) == (uint)inputs.Length;
    }

    private static bool TrySendKey(ushort key)
    {
        var inputs = new[]
        {
            KeyboardInput.KeyDown(key),
            KeyboardInput.KeyUp(key)
        };
        return NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>()) == (uint)inputs.Length;
    }

    private static void OpenSystemUri(string uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        ReleaseComObject(_deviceEnumerator);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    [Flags]
    private enum Clsctx : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All = InprocServer | InprocHandler | LocalServer | RemoteServer
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out object devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid interfaceId, Clsctx clsctx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float levelDb, ref Guid context);
        int SetMasterVolumeLevelScalar(float level, ref Guid context);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid context);
        int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid context);
        int GetChannelVolumeLevel(uint channelNumber, out float levelDb);
        int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;

        public static Input KeyDown(ushort key) => new()
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key } }
        };

        public static Input KeyUp(ushort key) => new()
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = 0x0002 } }
        };
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string className, string? windowName);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint ImmGetDescription(
            IntPtr keyboardLayout,
            StringBuilder description,
            uint bufferLength);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

        internal static bool ShowDesktopFallback()
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                return false;
            object? shell = Activator.CreateInstance(shellType);
            try
            {
                shellType.InvokeMember(
                    "ToggleDesktop",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    null);
                return true;
            }
            finally
            {
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    }
}
