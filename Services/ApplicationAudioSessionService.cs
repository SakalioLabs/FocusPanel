using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace FocusPanel.Services;

public readonly record struct
    ApplicationAudioSessionSnapshot(
        string SessionId,
        string DisplayName,
        int ProcessId,
        float Volume,
        bool IsMuted,
        bool IsActive,
        bool IsSystemSounds);

public interface IApplicationAudioSessionService :
    IDisposable
{
    IReadOnlyList<ApplicationAudioSessionSnapshot>
        GetSessions();

    bool TrySetVolume(
        string sessionId,
        float volume);

    bool TrySetMuted(
        string sessionId,
        bool muted);
}

internal interface IAudioSessionNativeApi
{
    IReadOnlyList<ApplicationAudioSessionSnapshot>
        GetSessions();

    bool TrySetVolume(
        string sessionId,
        float volume);

    bool TrySetMuted(
        string sessionId,
        bool muted);
}

public sealed class ApplicationAudioSessionService :
    IApplicationAudioSessionService
{
    private const int MaximumSessions = 12;
    private readonly IAudioSessionNativeApi _nativeApi;

    public ApplicationAudioSessionService()
        : this(new CoreAudioSessionNativeApi())
    {
    }

    internal ApplicationAudioSessionService(
        IAudioSessionNativeApi nativeApi)
    {
        _nativeApi =
            nativeApi
            ?? throw new ArgumentNullException(
                nameof(nativeApi));
    }

    public IReadOnlyList<ApplicationAudioSessionSnapshot>
        GetSessions()
    {
        try
        {
            return _nativeApi.GetSessions()
                .Where(session =>
                    !string.IsNullOrWhiteSpace(
                        session.SessionId)
                    && !string.IsNullOrWhiteSpace(
                        session.DisplayName))
                .GroupBy(
                    session => session.SessionId,
                    StringComparer.Ordinal)
                .Select(group =>
                    group.First())
                .OrderByDescending(session =>
                    session.IsActive)
                .ThenByDescending(session =>
                    session.IsSystemSounds)
                .ThenBy(
                    session => session.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    session => session.SessionId,
                    StringComparer.Ordinal)
                .Take(MaximumSessions)
                .Select(session =>
                    session with
                    {
                        Volume =
                            Math.Clamp(
                                session.Volume,
                                0f,
                                1f)
                    })
                .ToArray();
        }
        catch
        {
            return Array.Empty<
                ApplicationAudioSessionSnapshot>();
        }
    }

    public bool TrySetVolume(
        string sessionId,
        float volume)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        try
        {
            return _nativeApi.TrySetVolume(
                sessionId,
                Math.Clamp(volume, 0f, 1f));
        }
        catch
        {
            return false;
        }
    }

    public bool TrySetMuted(
        string sessionId,
        bool muted)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        try
        {
            return _nativeApi.TrySetMuted(
                sessionId,
                muted);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}

internal sealed class CoreAudioSessionNativeApi :
    IAudioSessionNativeApi
{
    public IReadOnlyList<ApplicationAudioSessionSnapshot>
        GetSessions() =>
        WithSessions(
            sessions => sessions
                .Select(TryReadSession)
                .Where(snapshot =>
                    snapshot.HasValue)
                .Select(snapshot =>
                    snapshot!.Value)
                .ToArray(),
            Array.Empty<
                ApplicationAudioSessionSnapshot>());

    public bool TrySetVolume(
        string sessionId,
        float volume) =>
        TryMutate(
            sessionId,
            audioVolume =>
            {
                Guid context = Guid.Empty;
                return audioVolume.SetMasterVolume(
                    Math.Clamp(volume, 0f, 1f),
                    ref context) >= 0;
            });

    public bool TrySetMuted(
        string sessionId,
        bool muted) =>
        TryMutate(
            sessionId,
            audioVolume =>
            {
                Guid context = Guid.Empty;
                return audioVolume.SetMute(
                    muted,
                    ref context) >= 0;
            });

    private static bool TryMutate(
        string sessionId,
        Func<ISimpleAudioVolume, bool> mutation) =>
        WithSessions(
            sessions =>
            {
                foreach (IAudioSessionControl
                         control in sessions)
                {
                    if (control
                        is not IAudioSessionControl2
                            control2
                        || !TryGetSessionId(
                            control2,
                            out string currentId)
                        || !string.Equals(
                            currentId,
                            sessionId,
                            StringComparison.Ordinal)
                        || control
                            is not ISimpleAudioVolume
                                audioVolume)
                    {
                        continue;
                    }

                    return mutation(audioVolume);
                }

                return false;
            },
            false);

    private static T WithSessions<T>(
        Func<
            IReadOnlyList<IAudioSessionControl>,
            T> operation,
        T fallback)
    {
        int initialization =
            NativeMethods.CoInitializeEx(
                IntPtr.Zero,
                CoInit.Multithreaded);
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? enumerator = null;
        var controls =
            new List<IAudioSessionControl>();
        try
        {
            deviceEnumerator =
                (IMMDeviceEnumerator)
                new MMDeviceEnumeratorComObject();
            if (deviceEnumerator
                    .GetDefaultAudioEndpoint(
                        EDataFlow.Render,
                        ERole.Multimedia,
                        out device) < 0
                || device == null)
            {
                return fallback;
            }

            Guid managerId =
                typeof(IAudioSessionManager2).GUID;
            if (device.Activate(
                    ref managerId,
                    Clsctx.All,
                    IntPtr.Zero,
                    out object managerObject) < 0
                || managerObject
                    is not IAudioSessionManager2
                        sessionManager)
            {
                ReleaseComObject(managerObject);
                return fallback;
            }

            manager = sessionManager;
            if (manager.GetSessionEnumerator(
                    out enumerator) < 0
                || enumerator == null
                || enumerator.GetCount(
                    out int count) < 0)
            {
                return fallback;
            }

            for (int index = 0;
                 index < count;
                 index++)
            {
                if (enumerator.GetSession(
                        index,
                        out IAudioSessionControl
                            control) >= 0
                    && control != null)
                {
                    controls.Add(control);
                }
            }

            return operation(controls);
        }
        catch
        {
            return fallback;
        }
        finally
        {
            foreach (IAudioSessionControl control
                     in controls)
            {
                ReleaseComObject(control);
            }
            ReleaseComObject(enumerator);
            ReleaseComObject(manager);
            ReleaseComObject(device);
            ReleaseComObject(deviceEnumerator);
            if (initialization >= 0)
                NativeMethods.CoUninitialize();
        }
    }

    private static
        ApplicationAudioSessionSnapshot?
        TryReadSession(
            IAudioSessionControl control)
    {
        if (control
            is not IAudioSessionControl2 control2
            || control
                is not ISimpleAudioVolume audioVolume
            || control.GetState(
                out AudioSessionState state) < 0
            || state == AudioSessionState.Expired
            || !TryGetSessionId(
                control2,
                out string sessionId)
            || audioVolume.GetMasterVolume(
                out float volume) < 0
            || audioVolume.GetMute(
                out bool muted) < 0)
        {
            return null;
        }

        bool systemSounds =
            control2.IsSystemSoundsSession() == 0;
        int processId = 0;
        _ = control2.GetProcessId(
            out processId);
        string displayName =
            systemSounds
                ? "系统声音"
                : ReadDisplayName(control)
                  ?? ReadProcessName(processId)
                  ?? $"音频会话 {processId}";
        return new ApplicationAudioSessionSnapshot(
            sessionId,
            displayName,
            processId,
            Math.Clamp(volume, 0f, 1f),
            muted,
            state == AudioSessionState.Active,
            systemSounds);
    }

    private static bool TryGetSessionId(
        IAudioSessionControl2 control,
        out string sessionId)
    {
        sessionId = string.Empty;
        IntPtr value = IntPtr.Zero;
        try
        {
            if (control
                    .GetSessionInstanceIdentifier(
                        out value) < 0
                || value == IntPtr.Zero)
            {
                return false;
            }

            sessionId =
                Marshal.PtrToStringUni(value)
                ?? string.Empty;
            return !string.IsNullOrWhiteSpace(
                sessionId);
        }
        finally
        {
            if (value != IntPtr.Zero)
                Marshal.FreeCoTaskMem(value);
        }
    }

    private static string? ReadDisplayName(
        IAudioSessionControl control)
    {
        IntPtr value = IntPtr.Zero;
        try
        {
            if (control.GetDisplayName(
                    out value) < 0
                || value == IntPtr.Zero)
            {
                return null;
            }

            string? name =
                Marshal.PtrToStringUni(value)
                    ?.Trim();
            return string.IsNullOrWhiteSpace(name)
                   || name.StartsWith(
                       "@",
                       StringComparison.Ordinal)
                ? null
                : name;
        }
        finally
        {
            if (value != IntPtr.Zero)
                Marshal.FreeCoTaskMem(value);
        }
    }

    private static string? ReadProcessName(
        int processId)
    {
        if (processId <= 0)
            return null;

        try
        {
            using Process process =
                Process.GetProcessById(processId);
            try
            {
                string? fileName =
                    process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(
                        fileName))
                {
                    FileVersionInfo version =
                        FileVersionInfo
                            .GetVersionInfo(fileName);
                    string? description =
                        version.FileDescription
                            ?.Trim();
                    if (!string.IsNullOrWhiteSpace(
                            description))
                    {
                        return description;
                    }
                }
            }
            catch
            {
                // Protected and packaged processes still expose ProcessName.
            }

            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static void ReleaseComObject(
        object? value)
    {
        if (value != null
            && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private enum AudioSessionState
    {
        Inactive,
        Active,
        Expired
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

    private enum CoInit : uint
    {
        Multithreaded = 0
    }

    [Flags]
    private enum Clsctx : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All =
            InprocServer
            | InprocHandler
            | LocalServer
            | RemoteServer
    }

    [ComImport]
    [Guid(
        "BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid(
        "A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(
            EDataFlow dataFlow,
            int stateMask,
            out object devices);

        int GetDefaultAudioEndpoint(
            EDataFlow dataFlow,
            ERole role,
            out IMMDevice endpoint);
    }

    [ComImport]
    [Guid(
        "D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(
            ref Guid interfaceId,
            Clsctx clsctx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)]
            out object interfacePointer);
    }

    [ComImport]
    [Guid(
        "77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        int GetAudioSessionControl(
            ref Guid audioSessionGuid,
            uint streamFlags,
            out IAudioSessionControl sessionControl);

        int GetSimpleAudioVolume(
            ref Guid audioSessionGuid,
            uint streamFlags,
            out ISimpleAudioVolume audioVolume);

        int GetSessionEnumerator(
            out IAudioSessionEnumerator sessionEnumerator);

        int RegisterSessionNotification(
            IntPtr notification);

        int UnregisterSessionNotification(
            IntPtr notification);

        int RegisterDuckNotification(
            [MarshalAs(UnmanagedType.LPWStr)]
            string sessionId,
            IntPtr notification);

        int UnregisterDuckNotification(
            IntPtr notification);
    }

    [ComImport]
    [Guid(
        "E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int sessionCount);

        int GetSession(
            int sessionCount,
            out IAudioSessionControl session);
    }

    [ComImport]
    [Guid(
        "F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        int GetState(
            out AudioSessionState state);

        int GetDisplayName(out IntPtr value);

        int SetDisplayName(
            [MarshalAs(UnmanagedType.LPWStr)]
            string value,
            ref Guid context);

        int GetIconPath(out IntPtr value);

        int SetIconPath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string value,
            ref Guid context);

        int GetGroupingParam(out Guid groupingId);

        int SetGroupingParam(
            ref Guid groupingId,
            ref Guid context);

        int RegisterAudioSessionNotification(
            IntPtr events);

        int UnregisterAudioSessionNotification(
            IntPtr events);
    }

    [ComImport]
    [Guid(
        "BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2 :
        IAudioSessionControl
    {
        new int GetState(
            out AudioSessionState state);

        new int GetDisplayName(out IntPtr value);

        new int SetDisplayName(
            [MarshalAs(UnmanagedType.LPWStr)]
            string value,
            ref Guid context);

        new int GetIconPath(out IntPtr value);

        new int SetIconPath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string value,
            ref Guid context);

        new int GetGroupingParam(
            out Guid groupingId);

        new int SetGroupingParam(
            ref Guid groupingId,
            ref Guid context);

        new int RegisterAudioSessionNotification(
            IntPtr events);

        new int UnregisterAudioSessionNotification(
            IntPtr events);

        int GetSessionIdentifier(out IntPtr value);

        int GetSessionInstanceIdentifier(
            out IntPtr value);

        int GetProcessId(out int processId);

        int IsSystemSoundsSession();

        int SetDuckingPreference(
            [MarshalAs(UnmanagedType.Bool)]
            bool optOut);
    }

    [ComImport]
    [Guid(
        "87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        int SetMasterVolume(
            float level,
            ref Guid context);

        int GetMasterVolume(out float level);

        int SetMute(
            [MarshalAs(UnmanagedType.Bool)]
            bool muted,
            ref Guid context);

        int GetMute(
            [MarshalAs(UnmanagedType.Bool)]
            out bool muted);
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int CoInitializeEx(
            IntPtr reserved,
            CoInit concurrencyModel);

        [DllImport("ole32.dll")]
        internal static extern void CoUninitialize();
    }
}
