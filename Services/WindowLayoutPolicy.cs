using System.Drawing;

namespace FocusPanel.Services;

public enum WindowLayoutTarget
{
    LeftHalf,
    RightHalf,
    TopLeftQuarter,
    TopRightQuarter,
    BottomLeftQuarter,
    BottomRightQuarter
}

internal static class WindowLayoutPolicy
{
    internal static Rectangle CalculateBounds(
        Rectangle workArea,
        WindowLayoutTarget target)
    {
        if (workArea.Width < 2
            || workArea.Height < 2)
        {
            return Rectangle.Empty;
        }

        int leftWidth = workArea.Width / 2;
        int rightWidth =
            workArea.Width - leftWidth;
        int topHeight = workArea.Height / 2;
        int bottomHeight =
            workArea.Height - topHeight;
        int right = workArea.Left + leftWidth;
        int bottom = workArea.Top + topHeight;

        return target switch
        {
            WindowLayoutTarget.LeftHalf =>
                new Rectangle(
                    workArea.Left,
                    workArea.Top,
                    leftWidth,
                    workArea.Height),
            WindowLayoutTarget.RightHalf =>
                new Rectangle(
                    right,
                    workArea.Top,
                    rightWidth,
                    workArea.Height),
            WindowLayoutTarget.TopLeftQuarter =>
                new Rectangle(
                    workArea.Left,
                    workArea.Top,
                    leftWidth,
                    topHeight),
            WindowLayoutTarget.TopRightQuarter =>
                new Rectangle(
                    right,
                    workArea.Top,
                    rightWidth,
                    topHeight),
            WindowLayoutTarget.BottomLeftQuarter =>
                new Rectangle(
                    workArea.Left,
                    bottom,
                    leftWidth,
                    bottomHeight),
            WindowLayoutTarget.BottomRightQuarter =>
                new Rectangle(
                    right,
                    bottom,
                    rightWidth,
                    bottomHeight),
            _ => Rectangle.Empty
        };
    }
}
