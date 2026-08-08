using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPanel.Views;

namespace FocusPanel.Services;

public sealed class FocusToastManager : IDisposable
{
    private static readonly TimeSpan DefaultDuration =
        TimeSpan.FromSeconds(7);

    private readonly Window _anchor;
    private readonly FocusToastQueue _queue = new();
    private readonly FocusNotificationCenter?
        _notificationCenter;
    private readonly DispatcherTimer _dismissTimer;
    private FocusToastWindow? _window;
    private bool _disposed;

    internal string PanelEdgeValue { get; set; } =
        ShellPanelEdgePolicy.RightValue;

    public FocusToastManager(
        Window anchor,
        FocusNotificationCenter? notificationCenter = null)
    {
        _anchor = anchor;
        _notificationCenter = notificationCenter;
        _dismissTimer = new DispatcherTimer(
            DispatcherPriority.Normal,
            anchor.Dispatcher);
        _dismissTimer.Tick += DismissTimer_Tick;
    }

    public void Enqueue(FocusToastNotification notification)
    {
        if (_disposed)
            return;

        if (!_anchor.Dispatcher.CheckAccess())
        {
            _anchor.Dispatcher.BeginInvoke(
                () => Enqueue(notification));
            return;
        }

        _notificationCenter?.Add(notification);
        if (_queue.Enqueue(notification))
            ShowCurrent();
    }

    public void Reposition()
    {
        if (_window?.IsVisible == true)
            PositionWindow(_window);
    }

    public void DismissAll()
    {
        if (_disposed)
            return;

        _dismissTimer.Stop();
        _queue.Clear();
        CloseWindow();
    }

    private void ShowCurrent()
    {
        FocusToastNotification? notification = _queue.Current;
        if (notification == null || _disposed)
            return;

        CloseWindow();
        var window = new FocusToastWindow();
        _window = window;
        window.Configure(notification);
        window.DismissRequested += Window_DismissRequested;
        window.ActionRequested += Window_ActionRequested;
        window.MouseEnter += (_, _) => _dismissTimer.Stop();
        window.MouseLeave += (_, _) => StartDismissTimer(notification);
        window.Loaded += (_, _) =>
        {
            PositionWindow(window);
            AnimateEntrance(window);
        };
        window.Show();
        PositionWindow(window);
        StartDismissTimer(notification);
    }

    private void PositionWindow(FocusToastWindow window)
    {
        Rect workArea = SystemParameters.WorkArea;
        double width = window.ActualWidth > 0
            ? window.ActualWidth
            : window.Width;
        double height = window.ActualHeight > 0
            ? window.ActualHeight
            : 156;
        double anchorHeight = _anchor.ActualHeight > 0
            ? _anchor.ActualHeight
            : _anchor.Height;

        window.Left =
            ShellPanelEdgePolicy.IsLeft(
                PanelEdgeValue)
                ? Math.Min(
                    workArea.Right
                        - width
                        - 12,
                    _anchor.Left
                        + _anchor.ActualWidth
                        + 12)
                : Math.Max(
                    workArea.Left + 12,
                    _anchor.Left - width - 12);
        window.Top = Math.Max(
            workArea.Top + 12,
            Math.Min(
                workArea.Bottom - height - 12,
                _anchor.Top + anchorHeight - height));
    }

    private static void AnimateEntrance(Window window)
    {
        if (!SystemParameters.ClientAreaAnimation
            || SystemParameters.HighContrast)
        {
            window.Opacity = 1;
            return;
        }

        double targetLeft = window.Left;
        window.Left = targetLeft + 16;
        window.Opacity = 0;
        var duration = new Duration(
            TimeSpan.FromMilliseconds(180));
        window.BeginAnimation(
            Window.LeftProperty,
            new DoubleAnimation(targetLeft, duration)
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });
        window.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(1, duration));
    }

    private void StartDismissTimer(
        FocusToastNotification notification)
    {
        _dismissTimer.Stop();
        _dismissTimer.Interval =
            notification.Duration ?? DefaultDuration;
        _dismissTimer.Start();
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        CompleteCurrent(invokeAction: false);
    }

    private void Window_DismissRequested(object? sender, EventArgs e) =>
        CompleteCurrent(invokeAction: false);

    private void Window_ActionRequested(object? sender, EventArgs e) =>
        CompleteCurrent(invokeAction: true);

    private void CompleteCurrent(bool invokeAction)
    {
        FocusToastNotification? completed = _queue.Current;
        _dismissTimer.Stop();
        CloseWindow();
        FocusToastNotification? next = _queue.CompleteCurrent();

        if (invokeAction)
            completed?.Action?.Invoke();

        if (next != null)
            ShowCurrent();
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _window.DismissRequested -= Window_DismissRequested;
        _window.ActionRequested -= Window_ActionRequested;
        _window.Close();
        _window = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dismissTimer.Stop();
        _dismissTimer.Tick -= DismissTimer_Tick;
        _queue.Clear();
        CloseWindow();
    }
}
