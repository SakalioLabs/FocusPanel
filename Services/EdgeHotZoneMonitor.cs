using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FocusPanel.Services;

public sealed class EdgeHotZoneMonitor : IDisposable
{
    private static readonly TimeSpan DefaultPollInterval =
        TimeSpan.FromMilliseconds(30);

    private readonly object _sync = new();
    private readonly Func<Rectangle> _boundsProvider;
    private readonly Func<Point> _cursorProvider;
    private readonly Func<bool> _isSuppressed;
    private readonly Action<Action> _postToUi;
    private readonly Func<long> _clock;
    private readonly TimeSpan _pollInterval;
    private readonly EdgeHotZoneDetector _detector;
    private Rectangle _targetBounds;
    private CancellationTokenSource? _pollCancellation;
    private Task _pollTask = Task.CompletedTask;
    private bool? _lastAvailability;
    private long _generation;
    private long _boundsRevision;
    private bool _disposed;

    public EdgeHotZoneMonitor(
        IWindowTracker windowTracker,
        Func<bool>? suppressInFullscreen = null,
        Func<Rectangle>? boundsProvider = null)
        : this(
            boundsProvider
                ?? (() =>
                    ShellDisplayTarget.GetBounds()),
            () => Forms.Cursor.Position,
            CreateSuppressionProbe(
                windowTracker,
                suppressInFullscreen),
            CreateDispatcherPoster(
                Dispatcher.CurrentDispatcher),
            () => Stopwatch.GetTimestamp()
                * 1000L
                / Stopwatch.Frequency,
            DefaultPollInterval)
    {
    }

    internal EdgeHotZoneMonitor(
        Func<Rectangle> boundsProvider,
        Func<Point> cursorProvider,
        Func<bool> isSuppressed,
        Action<Action> postToUi,
        Func<long> clock,
        TimeSpan pollInterval)
    {
        _boundsProvider =
            boundsProvider
            ?? throw new ArgumentNullException(
                nameof(boundsProvider));
        _cursorProvider =
            cursorProvider
            ?? throw new ArgumentNullException(
                nameof(cursorProvider));
        _isSuppressed =
            isSuppressed
            ?? throw new ArgumentNullException(
                nameof(isSuppressed));
        _postToUi =
            postToUi
            ?? throw new ArgumentNullException(
                nameof(postToUi));
        _clock =
            clock
            ?? throw new ArgumentNullException(
                nameof(clock));
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval));
        }

        _pollInterval = pollInterval;
        _detector = new EdgeHotZoneDetector();
        RefreshDisplayBounds();
    }

    public event EventHandler? OpenRequested;
    public event Action<bool>? AvailabilityChanged;

    public void Start()
    {
        Rectangle bounds =
            ReadBoundsSafely();
        CancellationTokenSource? previousCancellation;
        Task previousTask;
        CancellationTokenSource cancellation;
        long generation;
        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(EdgeHotZoneMonitor));
            }

            previousCancellation =
                _pollCancellation;
            previousTask = _pollTask;
            generation = ++_generation;
            _targetBounds = bounds;
            _boundsRevision++;
            _detector.Reset();
            _lastAvailability = null;
            cancellation =
                new CancellationTokenSource();
            _pollCancellation =
                cancellation;
            _pollTask =
                Task.Run(
                    () =>
                        PollAsync(
                            generation,
                            cancellation.Token));
        }

        CancelAndDisposeWhenComplete(
            previousCancellation,
            previousTask);
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;
        Task pollTask;
        long generation;
        bool publishUnavailable;
        lock (_sync)
        {
            generation = ++_generation;
            cancellation =
                _pollCancellation;
            pollTask = _pollTask;
            _pollCancellation = null;
            _pollTask = Task.CompletedTask;
            _detector.Reset();
            publishUnavailable =
                _lastAvailability != false;
            _lastAvailability = false;
        }

        CancelAndDisposeWhenComplete(
            cancellation,
            pollTask);
        if (publishUnavailable)
        {
            PostResult(
                generation,
                availability: false,
                openRequested: false);
        }
    }

    public void RefreshDisplayBounds()
    {
        Rectangle bounds =
            ReadBoundsSafely();
        lock (_sync)
        {
            if (_disposed)
                return;

            _targetBounds = bounds;
            _boundsRevision++;
            _detector.Reset();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
        }

        Stop();
        lock (_sync)
        {
            _disposed = true;
            _generation++;
        }
    }

    private async Task PollAsync(
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer =
                new PeriodicTimer(
                    _pollInterval);
            PollOnce(generation);
            while (await timer
                       .WaitForNextTickAsync(
                           cancellationToken)
                       .ConfigureAwait(false))
            {
                PollOnce(generation);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
        }
        catch
        {
            PublishAvailability(
                generation,
                false);
        }
    }

    private void PollOnce(long generation)
    {
        Rectangle bounds;
        long boundsRevision;
        lock (_sync)
        {
            if (!IsCurrentLocked(
                    generation))
            {
                return;
            }

            bounds = _targetBounds;
            boundsRevision =
                _boundsRevision;
        }

        if (bounds.Width <= 0
            || bounds.Height <= 0)
        {
            Rectangle refreshed =
                ReadBoundsSafely();
            lock (_sync)
            {
                if (!IsCurrentLocked(
                        generation))
                {
                    return;
                }

                _targetBounds = refreshed;
                _boundsRevision++;
                _detector.Reset();
                bounds = refreshed;
                boundsRevision =
                    _boundsRevision;
            }
        }

        bool isSuppressed;
        Point cursor;
        long now;
        try
        {
            isSuppressed =
                _isSuppressed();
            cursor =
                _cursorProvider();
            now = _clock();
        }
        catch
        {
            ResetAndPublishUnavailable(
                generation);
            return;
        }

        bool openRequested = false;
        bool availability;
        bool publishAvailability;
        lock (_sync)
        {
            if (!IsCurrentLocked(
                    generation)
                || boundsRevision
                    != _boundsRevision)
            {
                return;
            }

            availability =
                !isSuppressed
                && bounds.Width > 0
                && bounds.Height > 0;
            if (!availability)
            {
                _detector.Reset();
            }
            else
            {
                openRequested =
                    _detector.Update(
                        cursor,
                        bounds,
                        now);
            }

            publishAvailability =
                _lastAvailability
                    != availability;
            _lastAvailability =
                availability;
        }

        if (publishAvailability
            || openRequested)
        {
            PostResult(
                generation,
                publishAvailability
                    ? availability
                    : null,
                openRequested);
        }
    }

    private void ResetAndPublishUnavailable(
        long generation)
    {
        bool publish;
        lock (_sync)
        {
            if (!IsCurrentLocked(
                    generation))
            {
                return;
            }

            _detector.Reset();
            publish =
                _lastAvailability != false;
            _lastAvailability = false;
        }

        if (publish)
        {
            PostResult(
                generation,
                availability: false,
                openRequested: false);
        }
    }

    private void PublishAvailability(
        long generation,
        bool availability)
    {
        bool publish;
        lock (_sync)
        {
            if (!IsCurrentLocked(
                    generation))
            {
                return;
            }

            _detector.Reset();
            publish =
                _lastAvailability
                    != availability;
            _lastAvailability =
                availability;
        }

        if (publish)
        {
            PostResult(
                generation,
                availability,
                openRequested: false);
        }
    }

    private void PostResult(
        long generation,
        bool? availability,
        bool openRequested)
    {
        try
        {
            _postToUi(
                () =>
                {
                    lock (_sync)
                    {
                        if (_disposed
                            || generation
                                != _generation)
                        {
                            return;
                        }
                    }

                    if (availability
                        is bool isAvailable)
                    {
                        AvailabilityChanged?
                            .Invoke(isAvailable);
                    }

                    if (openRequested)
                    {
                        OpenRequested?
                            .Invoke(
                                this,
                                EventArgs.Empty);
                    }
                });
        }
        catch
        {
            // The WPF dispatcher can already be shutting down.
        }
    }

    private bool IsCurrentLocked(
        long generation) =>
        !_disposed
        && _pollCancellation != null
        && generation == _generation;

    private Rectangle ReadBoundsSafely()
    {
        try
        {
            return _boundsProvider();
        }
        catch
        {
            return Rectangle.Empty;
        }
    }

    private static Action<Action>
        CreateDispatcherPoster(
            Dispatcher dispatcher) =>
            action =>
            {
                if (dispatcher.HasShutdownStarted
                    || dispatcher
                        .HasShutdownFinished)
                {
                    return;
                }

                if (dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                _ = dispatcher.BeginInvoke(
                    action,
                    DispatcherPriority.Input);
            };

    private static Func<bool>
        CreateSuppressionProbe(
            IWindowTracker windowTracker,
            Func<bool>?
                suppressInFullscreen)
    {
        ArgumentNullException.ThrowIfNull(
            windowTracker);
        return () =>
            (suppressInFullscreen?.Invoke()
                ?? true)
            && windowTracker
                .IsForegroundFullscreen();
    }

    private static void
        CancelAndDisposeWhenComplete(
            CancellationTokenSource?
                cancellation,
            Task task)
    {
        if (cancellation == null)
            return;

        cancellation.Cancel();
        if (task.IsCompleted)
        {
            cancellation.Dispose();
            return;
        }

        _ = task.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions
                .ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
