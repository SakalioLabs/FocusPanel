using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace FocusPanel.Services;

public readonly record struct BrightnessStatusSnapshot(
    bool IsAvailable,
    int Percent,
    string Detail)
{
    public static BrightnessStatusSnapshot Unavailable =>
        new(
            false,
            0,
            "此设备未公开内置显示器亮度控制");
}

public interface IDisplayBrightnessService :
    IDisposable
{
    BrightnessStatusSnapshot GetStatus();
    bool TrySetBrightness(int percent);
}

internal readonly record struct
    BrightnessDeviceObservation(
        string InstanceName,
        int Percent);

internal interface IBrightnessNativeApi
{
    IReadOnlyList<BrightnessDeviceObservation>
        GetActiveDevices();

    bool TrySetBrightness(
        string instanceName,
        byte percent);
}

public sealed class DisplayBrightnessService :
    IDisplayBrightnessService
{
    private readonly IBrightnessNativeApi _nativeApi;

    public DisplayBrightnessService()
        : this(new WmiBrightnessNativeApi())
    {
    }

    internal DisplayBrightnessService(
        IBrightnessNativeApi nativeApi)
    {
        _nativeApi =
            nativeApi
            ?? throw new ArgumentNullException(
                nameof(nativeApi));
    }

    public BrightnessStatusSnapshot GetStatus()
    {
        try
        {
            BrightnessDeviceObservation[] devices =
                _nativeApi.GetActiveDevices()
                    .Where(device =>
                        !string.IsNullOrWhiteSpace(
                            device.InstanceName))
                    .OrderBy(device =>
                        device.InstanceName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            if (devices.Length == 0)
                return BrightnessStatusSnapshot.Unavailable;

            int average =
                (int)Math.Round(
                    devices.Average(device =>
                        Math.Clamp(
                            device.Percent,
                            0,
                            100)),
                    MidpointRounding.AwayFromZero);
            return new BrightnessStatusSnapshot(
                true,
                average,
                devices.Length == 1
                    ? "内置显示器"
                    : $"{devices.Length} 个内置显示设备");
        }
        catch
        {
            return BrightnessStatusSnapshot.Unavailable;
        }
    }

    public bool TrySetBrightness(int percent)
    {
        try
        {
            string[] instances =
                _nativeApi.GetActiveDevices()
                    .Select(device =>
                        device.InstanceName)
                    .Where(instance =>
                        !string.IsNullOrWhiteSpace(
                            instance))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            if (instances.Length == 0)
                return false;

            byte value =
                (byte)Math.Clamp(
                    percent,
                    0,
                    100);
            bool succeeded = true;
            foreach (string instance in instances)
            {
                succeeded &=
                    _nativeApi.TrySetBrightness(
                        instance,
                        value);
            }

            return succeeded;
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

internal sealed class WmiBrightnessNativeApi :
    IBrightnessNativeApi
{
    private const string ScopePath = @"root\wmi";

    public IReadOnlyList<BrightnessDeviceObservation>
        GetActiveDevices()
    {
        var devices =
            new List<BrightnessDeviceObservation>();
        using var searcher =
            new ManagementObjectSearcher(
                ScopePath,
                "SELECT InstanceName, CurrentBrightness "
                + "FROM WmiMonitorBrightness "
                + "WHERE Active = TRUE");
        using ManagementObjectCollection results =
            searcher.Get();
        foreach (ManagementBaseObject item in results)
        {
            using (item)
            {
                string? instance =
                    item["InstanceName"]?.ToString();
                if (string.IsNullOrWhiteSpace(
                        instance))
                {
                    continue;
                }

                int percent =
                    Convert.ToInt32(
                        item["CurrentBrightness"]);
                devices.Add(
                    new BrightnessDeviceObservation(
                        instance,
                        Math.Clamp(
                            percent,
                            0,
                            100)));
            }
        }

        return devices;
    }

    public bool TrySetBrightness(
        string instanceName,
        byte percent)
    {
        string escapedInstance =
            instanceName.Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
                .Replace(
                    "'",
                    "\\'",
                    StringComparison.Ordinal);
        using var searcher =
            new ManagementObjectSearcher(
                ScopePath,
                "SELECT * FROM "
                + "WmiMonitorBrightnessMethods "
                + "WHERE Active = TRUE AND "
                + $"InstanceName = '{escapedInstance}'");
        using ManagementObjectCollection results =
            searcher.Get();
        ManagementObject? method =
            results.Cast<ManagementObject>()
                .FirstOrDefault();
        if (method == null)
            return false;

        using (method)
        using (ManagementBaseObject input =
               method.GetMethodParameters(
                   "WmiSetBrightness"))
        {
            input["Timeout"] = 0u;
            input["Brightness"] = percent;
            using ManagementBaseObject? output =
                method.InvokeMethod(
                    "WmiSetBrightness",
                    input,
                    new InvokeMethodOptions());
            return output != null
                   && Convert.ToUInt32(
                       output["ReturnValue"]) == 0u;
        }
    }
}
