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
    private Rectangle _primaryBounds;
    private bool _hasPrimaryScreen;
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
        Forms.Screen? primary = Forms.Screen.PrimaryScreen;
        _hasPrimaryScreen = primary != null;
        _primaryBounds = primary?.Bounds ?? Rectangle.Empty;
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
        if (!_hasPrimaryScreen)
        {
            RefreshDisplayBounds();
            if (!_hasPrimaryScreen)
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
        if (_detector.Update(Forms.Cursor.Position, _primaryBounds, _clock.ElapsedMilliseconds))
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
