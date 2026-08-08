using FocusPanel.Models;

namespace FocusPanel.Services;

public sealed record WindowLayoutRequest(
    WindowReference Window,
    WindowLayoutTarget Target);

internal static class WindowLayoutPresentation
{
    internal static string GetName(
        WindowLayoutTarget target) =>
        target switch
        {
            WindowLayoutTarget.LeftHalf =>
                "左半屏",
            WindowLayoutTarget.RightHalf =>
                "右半屏",
            WindowLayoutTarget.TopLeftQuarter =>
                "左上四分区",
            WindowLayoutTarget.TopRightQuarter =>
                "右上四分区",
            WindowLayoutTarget.BottomLeftQuarter =>
                "左下四分区",
            WindowLayoutTarget.BottomRightQuarter =>
                "右下四分区",
            _ => "所选区域"
        };
}
