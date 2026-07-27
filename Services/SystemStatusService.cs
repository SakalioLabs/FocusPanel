using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly IAudioEndpointVolume? _audioEndpoint;

    public SystemStatusService()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out IMMDevice device);
            Guid endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref endpointVolumeId, Clsctx.All, IntPtr.Zero, out object endpoint);
            _audioEndpoint = (IAudioEndpointVolume)endpoint;
        }
        catch
        {
            _audioEndpoint = null;
        }
    }

    public float MasterVolume
    {
        get
        {
            if (_audioEndpoint == null)
                return 0;
            try
            {
                _audioEndpoint.GetMasterVolumeLevelScalar(out float volume);
                return volume;
            }
            catch
            {
                return 0;
            }
        }
        set
        {
            if (_audioEndpoint == null)
                return;
            try
            {
                Guid context = Guid.Empty;
                _audioEndpoint.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref context);
            }
            catch
            {
                // The default endpoint can disappear during device switching.
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            if (_audioEndpoint == null)
                return false;
            _audioEndpoint.GetMute(out bool muted);
            return muted;
        }
        set
        {
            if (_audioEndpoint == null)
                return;
            Guid context = Guid.Empty;
            _audioEndpoint.SetMute(value, ref context);
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

    public bool HasBattery => SystemInformation.PowerStatus.BatteryChargeStatus
        != BatteryChargeStatus.NoSystemBattery;

    public int BatteryPercent => HasBattery
        ? (int)Math.Round(SystemInformation.PowerStatus.BatteryLifePercent * 100)
        : 0;

    public void OpenQuickSettings()
    {
        if (!TrySendWindowsShortcut(0x41))
            OpenSystemUri("ms-settings:network-status");
    }

    public void OpenNotifications()
    {
        if (!TrySendWindowsShortcut(0x4E))
            OpenSystemUri("ms-settings:notifications");
    }

    public void OpenInputSwitcher()
    {
        if (!TrySendWindowsShortcut(0x20))
            OpenSystemUri("ms-settings:typing");
    }

    public void OpenPowerSettings() => OpenSystemUri("ms-settings:powersleep");

    public void ShowDesktop()
    {
        if (!TrySendWindowsShortcut(0x44))
            NativeMethods.ShowDesktopFallback();
    }
    public void Lock() => NativeMethods.LockWorkStation();
    public void Sleep() => NativeMethods.SetSuspendState(false, false, false);
    public void Restart() => StartShutdown("/r /t 0");
    public void Shutdown() => StartShutdown("/s /t 0");

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

    private static bool TrySendWindowsShortcut(ushort key)
    {
        var inputs = new[]
        {
            KeyboardInput.KeyDown(0x5B),
            KeyboardInput.KeyDown(key),
            KeyboardInput.KeyUp(key),
            KeyboardInput.KeyUp(0x5B)
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
        if (_audioEndpoint != null && Marshal.IsComObject(_audioEndpoint))
            Marshal.FinalReleaseComObject(_audioEndpoint);
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
        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

        internal static void ShowDesktopFallback()
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                return;
            object? shell = Activator.CreateInstance(shellType);
            shellType.InvokeMember(
                "ToggleDesktop",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                null);
            if (shell != null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    }
}
