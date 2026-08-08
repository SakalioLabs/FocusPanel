using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly Func<
        WindowsShellShortcut,
        bool> _shortcutSender;
    private readonly IInputMethodNative
        _inputMethods;
    private readonly IDesktopToggleNative
        _desktopToggle;

    public SystemStatusService()
        : this(
            SendWindowsShortcut,
            new WindowsInputMethodNative(),
            new ShellDesktopToggleNative())
    {
    }

    internal SystemStatusService(
        Func<
            WindowsShellShortcut,
            bool> shortcutSender,
        IInputMethodNative? inputMethods = null,
        IDesktopToggleNative? desktopToggle = null)
    {
        _shortcutSender =
            shortcutSender
            ?? throw new ArgumentNullException(
                nameof(shortcutSender));
        _inputMethods =
            inputMethods
            ?? new WindowsInputMethodNative();
        _desktopToggle =
            desktopToggle
            ?? new ShellDesktopToggleNative();
    }

    public SystemStatusSnapshot GetStatusSnapshot()
    {
        int comInitializationResult =
            NativeMethods.CoInitializeEx(
                IntPtr.Zero,
                CoInit.Multithreaded);
        IMMDeviceEnumerator? deviceEnumerator =
            TryCreateDeviceEnumerator();
        try
        {
            return new SystemStatusSnapshot(
                GetAudioStatus(deviceEnumerator),
                GetNetworkStatus(),
                GetInputMethodStatus(),
                GetBatteryStatus());
        }
        finally
        {
            ReleaseComObject(deviceEnumerator);
            if (HResultSucceeded(
                    comInitializationResult))
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    public AudioStatusSnapshot GetAudioStatus()
    {
        int comInitializationResult =
            NativeMethods.CoInitializeEx(
                IntPtr.Zero,
                CoInit.Multithreaded);
        IMMDeviceEnumerator? deviceEnumerator =
            TryCreateDeviceEnumerator();
        try
        {
            return GetAudioStatus(
                deviceEnumerator);
        }
        finally
        {
            ReleaseComObject(deviceEnumerator);
            if (HResultSucceeded(
                    comInitializationResult))
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    private static AudioStatusSnapshot GetAudioStatus(
        IMMDeviceEnumerator? deviceEnumerator)
    {
        IAudioEndpointVolume? endpoint = null;
        try
        {
            endpoint = GetDefaultAudioEndpoint(
                deviceEnumerator);
            if (endpoint == null)
                return AudioStatusSnapshot.Unavailable;

            int volumeResult =
                endpoint.GetMasterVolumeLevelScalar(
                    out float volume);
            int muteResult = endpoint.GetMute(out bool muted);
            if (!HResultSucceeded(volumeResult)
                || !HResultSucceeded(muteResult))
            {
                return AudioStatusSnapshot.Unavailable;
            }

            return new AudioStatusSnapshot(
                true,
                Math.Clamp(volume, 0f, 1f),
                muted);
        }
        catch
        {
            return AudioStatusSnapshot.Unavailable;
        }
        finally
        {
            ReleaseComObject(endpoint);
        }
    }

    public bool TrySetMasterVolume(float value)
    {
        int comInitializationResult =
            NativeMethods.CoInitializeEx(
                IntPtr.Zero,
                CoInit.Multithreaded);
        IMMDeviceEnumerator? deviceEnumerator =
            TryCreateDeviceEnumerator();
        IAudioEndpointVolume? endpoint = null;
        try
        {
            endpoint = GetDefaultAudioEndpoint(
                deviceEnumerator);
            if (endpoint == null)
                return false;

            Guid context = Guid.Empty;
            int result =
                endpoint.SetMasterVolumeLevelScalar(
                    Math.Clamp(value, 0f, 1f),
                    ref context);
            return HResultSucceeded(result);
        }
        catch
        {
            // The default endpoint can disappear during device switching.
            return false;
        }
        finally
        {
            ReleaseComObject(endpoint);
            ReleaseComObject(deviceEnumerator);
            if (HResultSucceeded(
                    comInitializationResult))
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    public bool TrySetMuted(bool value)
    {
        int comInitializationResult =
            NativeMethods.CoInitializeEx(
                IntPtr.Zero,
                CoInit.Multithreaded);
        IMMDeviceEnumerator? deviceEnumerator =
            TryCreateDeviceEnumerator();
        IAudioEndpointVolume? endpoint = null;
        try
        {
            endpoint = GetDefaultAudioEndpoint(
                deviceEnumerator);
            if (endpoint == null)
                return false;

            Guid context = Guid.Empty;
            return HResultSucceeded(
                endpoint.SetMute(value, ref context));
        }
        catch
        {
            // The default endpoint can disappear during device switching.
            return false;
        }
        finally
        {
            ReleaseComObject(endpoint);
            ReleaseComObject(deviceEnumerator);
            if (HResultSucceeded(
                    comInitializationResult))
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    public bool SendMediaCommand(
        MediaTransportAction action)
    {
        try
        {
            return TrySendWindowsShortcut(
                MediaTransportShortcutMap.Get(
                    action));
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public NetworkStatusSnapshot GetNetworkStatus()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return NetworkStatusSnapshot.Unavailable;

            NetworkInterface? active =
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(item =>
                        item.OperationalStatus
                            == OperationalStatus.Up
                        && item.NetworkInterfaceType
                            != NetworkInterfaceType.Loopback
                        && item.NetworkInterfaceType
                            != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(item =>
                        item.NetworkInterfaceType
                            == NetworkInterfaceType.Wireless80211)
                    .FirstOrDefault();
            if (active == null)
            {
                return NetworkStatusSnapshot.FromObservation(
                    true,
                    null,
                    NetworkConnectionKind.Unknown,
                    null,
                    null);
            }

            string? ipv4 = active.GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(address =>
                    address.Address.AddressFamily
                        == AddressFamily.InterNetwork)
                ?.Address.ToString();
            NetworkConnectionKind connectionKind;
            string kindLabel;
            if (active.NetworkInterfaceType
                == NetworkInterfaceType.Wireless80211)
            {
                connectionKind =
                    NetworkConnectionKind.WiFi;
                kindLabel = "Wi‑Fi";
            }
            else if (active.NetworkInterfaceType
                == NetworkInterfaceType.Ethernet)
            {
                connectionKind =
                    NetworkConnectionKind.Ethernet;
                kindLabel = "以太网";
            }
            else
            {
                connectionKind =
                    NetworkConnectionKind.Other;
                kindLabel =
                    active.NetworkInterfaceType.ToString();
            }
            return NetworkStatusSnapshot.FromObservation(
                true,
                active.Name,
                connectionKind,
                kindLabel,
                ipv4);
        }
        catch
        {
            return NetworkStatusSnapshot.Unavailable;
        }
    }

    public InputMethodStatusSnapshot GetInputMethodStatus()
    {
        try
        {
            IntPtr foreground =
                _inputMethods
                    .GetForegroundWindow();
            IntPtr layout = _inputMethods
                .GetKeyboardLayoutForWindow(
                    foreground);
            CultureInfo? culture = GetInputCulture(layout);
            string description =
                _inputMethods.GetDescription(
                    layout);
            return InputMethodStatusSnapshot.FromObservation(
                culture?.TwoLetterISOLanguageName,
                description);
        }
        catch
        {
            return InputMethodStatusSnapshot.Unavailable;
        }
    }

    public IReadOnlyList<InputMethodOption>
        GetInputMethods()
    {
        try
        {
            IntPtr foreground =
                _inputMethods
                    .GetForegroundWindow();
            IntPtr activeLayout =
                _inputMethods
                    .GetKeyboardLayoutForWindow(
                        foreground);
            InputMethodObservation[]
                observations = _inputMethods
                    .GetKeyboardLayouts()
                    .Select(layout =>
                    {
                        CultureInfo? culture =
                            GetInputCulture(layout);
                        return new
                            InputMethodObservation(
                                layout.ToInt64(),
                                culture
                                    ?.TwoLetterISOLanguageName,
                                culture?.NativeName,
                                _inputMethods
                                    .GetDescription(
                                        layout));
                    })
                    .ToArray();
            return InputMethodOptionComposer
                .Compose(
                    observations,
                    activeLayout.ToInt64());
        }
        catch
        {
            return Array.Empty<
                InputMethodOption>();
        }
    }

    public bool TryActivateInputMethod(
        InputMethodOption inputMethod,
        IntPtr preferredTargetWindow)
    {
        try
        {
            IntPtr layout = new(
                inputMethod.LayoutHandle);
            bool isInstalled = _inputMethods
                .GetKeyboardLayouts()
                .Any(installed =>
                    installed == layout);
            if (!isInstalled)
                return false;

            IntPtr foreground =
                _inputMethods
                    .GetForegroundWindow();
            IntPtr target =
                preferredTargetWindow
                    != IntPtr.Zero
                    ? preferredTargetWindow
                    : foreground;
            if (target == IntPtr.Zero)
                return false;

            if (_inputMethods
                    .GetKeyboardLayoutForWindow(
                        target)
                == layout)
            {
                if (foreground != IntPtr.Zero
                    && foreground != target)
                {
                    _ = _inputMethods
                        .TryRequestInputLanguage(
                            foreground,
                            layout);
                }
                return true;
            }

            bool requestPosted =
                _inputMethods
                    .TryRequestInputLanguage(
                        target,
                        layout);
            if (!requestPosted)
                return false;
            if (foreground != IntPtr.Zero
                && foreground != target)
            {
                _ = _inputMethods
                    .TryRequestInputLanguage(
                        foreground,
                        layout);
            }

            for (int attempt = 0;
                 attempt < 5;
                 attempt++)
            {
                if (_inputMethods
                        .GetKeyboardLayoutForWindow(
                            target)
                    == layout)
                {
                    return true;
                }

                Thread.Sleep(30);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public BatteryStatusSnapshot GetBatteryStatus()
    {
        try
        {
            PowerStatus power = SystemInformation.PowerStatus;
            BatteryChargeStatus chargeStatus =
                power.BatteryChargeStatus;
            bool hasBattery =
                chargeStatus !=
                BatteryChargeStatus.NoSystemBattery;
            bool isCharging = hasBattery
                && chargeStatus
                    != BatteryChargeStatus.Unknown
                && (chargeStatus
                    & BatteryChargeStatus.Charging) != 0;
            return BatteryStatusSnapshot.FromFraction(
                hasBattery,
                power.BatteryLifePercent,
                isCharging);
        }
        catch
        {
            return BatteryStatusSnapshot.Unavailable;
        }
    }

    public bool SwitchVirtualDesktop(
        VirtualDesktopDirection direction) =>
        TrySendWindowsShortcut(
            direction
            == VirtualDesktopDirection.Previous
                ? WindowsShellAction
                    .VirtualDesktopPrevious
                : WindowsShellAction
                    .VirtualDesktopNext);

    public bool CreateVirtualDesktop() =>
        TrySendWindowsShortcut(
            WindowsShellAction
                .VirtualDesktopCreate);

    public bool CloseCurrentVirtualDesktop() =>
        TrySendWindowsShortcut(
            WindowsShellAction
                .VirtualDesktopClose);

    public bool OpenSoundOutput() =>
        TrySendWindowsShortcut(
            WindowsShellAction.SoundOutput);

    public bool OpenScreenSnipping() =>
        TrySendWindowsShortcut(
            WindowsShellAction.ScreenSnipping);

    public bool OpenProjectDisplay() =>
        TrySendWindowsShortcut(
            WindowsShellAction.ProjectDisplay);

    public bool OpenCastDevices() =>
        TrySendWindowsShortcut(
            WindowsShellAction.CastDevices);

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

    public bool OpenLocationPrivacySettings() =>
        SystemActionExecution.TryStart(
            () => OpenSystemUri(
                "ms-settings:privacy-location"));

    public bool ShowDesktop() =>
        SystemActionExecution.Try(
            _desktopToggle.ToggleDesktop);

    public bool Lock() =>
        SystemActionExecution.Try(NativeMethods.LockWorkStation);

    public bool Sleep() =>
        SystemActionExecution.Try(
            () => NativeMethods.SetSuspendState(false, false, false));

    public bool Restart() =>
        SystemActionExecution.TryStart(() => StartShutdown("/r /t 0"));

    public bool Shutdown() =>
        SystemActionExecution.TryStart(() => StartShutdown("/s /t 0"));

    private static IAudioEndpointVolume? GetDefaultAudioEndpoint(
        IMMDeviceEnumerator? deviceEnumerator)
    {
        if (deviceEnumerator == null)
            return null;

        int deviceResult =
            deviceEnumerator.GetDefaultAudioEndpoint(
            EDataFlow.Render,
            ERole.Multimedia,
            out IMMDevice device);
        if (!HResultSucceeded(deviceResult)
            || device == null)
        {
            return null;
        }

        try
        {
            Guid endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
            int activationResult =
                device.Activate(
                    ref endpointVolumeId,
                    Clsctx.All,
                    IntPtr.Zero,
                    out object endpoint);
            if (HResultSucceeded(activationResult)
                && endpoint is IAudioEndpointVolume volume)
            {
                return volume;
            }

            ReleaseComObject(endpoint);
            return null;
        }
        finally
        {
            ReleaseComObject(device);
        }
    }

    internal static bool HResultSucceeded(int result) =>
        result >= 0;

    private static IMMDeviceEnumerator?
        TryCreateDeviceEnumerator()
    {
        try
        {
            return (IMMDeviceEnumerator)
                new MMDeviceEnumeratorComObject();
        }
        catch
        {
            return null;
        }
    }

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

    private bool TrySendWindowsShortcut(
        WindowsShellAction action) =>
        _shortcutSender(
            WindowsShellShortcutMap.Get(
                action));

    private static bool SendWindowsShortcut(
        WindowsShellShortcut shortcut)
    {
        IReadOnlyList<
            WindowsShortcutKeyTransition>
            sequence =
                WindowsShortcutSequence.Build(
                    shortcut);
        Input[] inputs =
            sequence.Select(
                    transition =>
                        transition.IsDown
                            ? KeyboardInput
                                .KeyDown(
                                    transition.Key)
                            : KeyboardInput
                                .KeyUp(
                                    transition.Key))
                .ToArray();
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
        // Audio COM objects are scoped to each calling thread and
        // released at the end of every read or write operation.
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

    private enum CoInit : uint
    {
        Multithreaded = 0
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
        [DllImport("ole32.dll")]
        internal static extern int CoInitializeEx(
            IntPtr reserved,
            CoInit concurrencyModel);

        [DllImport("ole32.dll")]
        internal static extern void CoUninitialize();

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    }
}
