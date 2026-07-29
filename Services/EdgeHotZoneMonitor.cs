using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class EdgeHotZoneMonitor : IDisposable
{
    private readonly IWindowTracker _windowTracker;
    private readonly Func<bool> _suppressInFullscreen;
    private readonly EdgeHotZoneDetector _detector;
    private readonly DispatcherTimer _pollTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Rectangle _targetBounds;
    private bool _hasTargetScreen;
    private bool _disposed;
    private bool? _lastAvailability;

    public EdgeHotZoneMonitor(
        IWindowTracker windowTracker,
        Func<bool>? suppressInFullscreen = null)
    {
        _windowTracker = windowTracker;
        _suppressInFullscreen = suppressInFullscreen ?? (() => true);
        _detector = new EdgeHotZoneDetector();
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _pollTimer.Tick += PollTimer_Tick;
        RefreshDisplayBounds();
    }

    public event EventHandler? OpenRequested;
    public event Action<bool>? AvailabilityChanged;

    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EdgeHotZoneMonitor));

        RefreshDisplayBounds();
        _detector.Reset();
        _pollTimer.Start();
    }

    public void Stop()
    {
        _pollTimer.Stop();
        _detector.Reset();
        SetAvailability(false);
    }

    public void RefreshDisplayBounds()
    {
        _targetBounds = ShellDisplayTarget.GetBounds();
        _hasTargetScreen =
            _targetBounds.Width > 0
            && _targetBounds.Height > 0;
        _detector.Reset();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _pollTimer.Tick -= PollTimer_Tick;
        _disposed = true;
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_hasTargetScreen)
        {
            RefreshDisplayBounds();
            if (!_hasTargetScreen)
            {
                SetAvailability(false);
                return;
            }
        }

        if (_suppressInFullscreen() && _windowTracker.IsForegroundFullscreen())
        {
            _detector.Reset();
            SetAvailability(false);
            return;
        }

        SetAvailability(true);
        if (_detector.Update(Forms.Cursor.Position, _targetBounds, _clock.ElapsedMilliseconds))
            OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetAvailability(bool isAvailable)
    {
        if (_lastAvailability == isAvailable)
            return;

        _lastAvailability = isAvailable;
        AvailabilityChanged?.Invoke(isAvailable);
    }
}
