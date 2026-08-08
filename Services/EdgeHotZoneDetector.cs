using System.Drawing;

namespace FocusPanel.Services;

internal sealed class EdgeHotZoneDetector
{
    private readonly int _edgeWidth;
    private readonly long _dwellMilliseconds;
    private readonly int _resetDistance;
    private readonly bool _isLeftEdge;
    private long? _enteredAtMilliseconds;
    private bool _isLatched;

    public EdgeHotZoneDetector(
        int edgeWidth = 12,
        long dwellMilliseconds = 100,
        int resetDistance = 32,
        string? panelEdge = null)
    {
        _edgeWidth = edgeWidth;
        _dwellMilliseconds = dwellMilliseconds;
        _resetDistance = resetDistance;
        _isLeftEdge =
            ShellPanelEdgePolicy.IsLeft(
                panelEdge);
    }

    public bool Update(Point cursor, Rectangle screenBounds, long nowMilliseconds)
    {
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            Reset();
            return false;
        }

        bool withinVerticalBounds =
            cursor.Y >= screenBounds.Top
            && cursor.Y < screenBounds.Bottom;
        bool withinEdge =
            withinVerticalBounds
            && (_isLeftEdge
                ? cursor.X >= screenBounds.Left
                  && cursor.X < screenBounds.Left
                      + _edgeWidth
                : cursor.X >= screenBounds.Right
                      - _edgeWidth
                  && cursor.X < screenBounds.Right);

        if (_isLatched)
        {
            bool leftResetZone =
                !withinVerticalBounds
                || (_isLeftEdge
                    ? cursor.X >= screenBounds.Left
                          + _resetDistance
                      || cursor.X < screenBounds.Left
                    : cursor.X < screenBounds.Right
                          - _resetDistance
                      || cursor.X >= screenBounds.Right);
            if (leftResetZone)
                Reset();

            return false;
        }

        if (!withinEdge)
        {
            _enteredAtMilliseconds = null;
            return false;
        }

        _enteredAtMilliseconds ??= nowMilliseconds;
        if (nowMilliseconds - _enteredAtMilliseconds.Value < _dwellMilliseconds)
            return false;

        _isLatched = true;
        return true;
    }

    public void Reset()
    {
        _enteredAtMilliseconds = null;
        _isLatched = false;
    }
}
