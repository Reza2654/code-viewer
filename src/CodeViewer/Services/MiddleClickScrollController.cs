using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;

namespace CodeViewer.Services;

/// <summary>
/// Provides silky-smooth middle-mouse-button autoscroll (pan-scrolling) for AvaloniaEdit.
/// Drops an anchor adorner indicator at the middle-click origin, captures pointer,
/// dynamically updates directional cursor, and scrolls viewport proportionally to distance.
/// Supports both click-and-drag and click-to-toggle modes, smoothly dismissed on click, wheel, or Escape.
/// </summary>
public sealed class MiddleClickScrollController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Control? _anchorIndicator;
    private readonly DispatcherTimer _timer;

    private bool _isAutoScrolling;
    private bool _isDragGesture;
    private Point _anchorPoint;
    private double _currentDeltaX;
    private double _currentDeltaY;
    private IPointer? _capturedPointer;
    private bool _isDisposed;

    public const double DeadZone = 8.0;
    public const double DefaultSpeedFactor = 0.28;

    public bool IsAutoScrolling => _isAutoScrolling;
    public Point AnchorPoint => _anchorPoint;
    public double CurrentDeltaX => _currentDeltaX;
    public double CurrentDeltaY => _currentDeltaY;

    public MiddleClickScrollController(TextEditor editor, Control? anchorIndicator = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _anchorIndicator = anchorIndicator;

        if (_anchorIndicator != null)
        {
            _anchorIndicator.IsVisible = false;
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTimerTick);

        _editor.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _editor.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        _editor.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        _editor.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(_editor).Properties;

        if (props.IsMiddleButtonPressed)
        {
            if (_isAutoScrolling)
            {
                Stop();
                e.Handled = true;
            }
            else
            {
                Start(e);
                e.Handled = true;
            }
            return;
        }

        // Left or right button click dismisses active autoscroll
        if (_isAutoScrolling)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void Start(PointerPressedEventArgs e)
    {
        _anchorPoint = e.GetPosition(_editor);
        _currentDeltaX = 0;
        _currentDeltaY = 0;
        _isDragGesture = false;
        _isAutoScrolling = true;

        if (_anchorIndicator != null)
        {
            var width = _anchorIndicator.Bounds.Width > 0 ? _anchorIndicator.Bounds.Width : 32.0;
            var height = _anchorIndicator.Bounds.Height > 0 ? _anchorIndicator.Bounds.Height : 32.0;

            Canvas.SetLeft(_anchorIndicator, _anchorPoint.X - width / 2.0);
            Canvas.SetTop(_anchorIndicator, _anchorPoint.Y - height / 2.0);
            _anchorIndicator.IsVisible = true;
        }

        _capturedPointer = e.Pointer;
        try
        {
            _capturedPointer.Capture(_editor);
        }
        catch
        {
            // Graceful fallback if pointer capture is not supported
        }

        UpdateCursor(0, 0);
        _timer.Start();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isAutoScrolling) return;

        var current = e.GetPosition(_editor);
        _currentDeltaX = current.X - _anchorPoint.X;
        _currentDeltaY = current.Y - _anchorPoint.Y;

        var distSquared = (_currentDeltaX * _currentDeltaX) + (_currentDeltaY * _currentDeltaY);
        if (distSquared > (DeadZone + 4.0) * (DeadZone + 4.0))
        {
            _isDragGesture = true;
        }

        UpdateCursor(_currentDeltaX, _currentDeltaY);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isAutoScrolling) return;

        // If user was doing a middle-button drag-and-release beyond the deadzone, end autoscroll
        if (_isDragGesture && e.InitialPressMouseButton == MouseButton.Middle)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_isAutoScrolling)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isAutoScrolling && e.Key == Key.Escape)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isAutoScrolling || _isDisposed)
        {
            _timer.Stop();
            return;
        }

        var (vx, vy) = CalculateScrollVelocity(_currentDeltaX, _currentDeltaY, DeadZone, DefaultSpeedFactor);

        if (Math.Abs(vy) > 0.001)
        {
            var targetV = _editor.VerticalOffset + vy;
            var maxV = Math.Max(0.0, _editor.ExtentHeight - _editor.ViewportHeight);
            _editor.ScrollToVerticalOffset(Math.Clamp(targetV, 0.0, maxV));
        }

        if (Math.Abs(vx) > 0.001)
        {
            var targetH = _editor.HorizontalOffset + vx;
            var maxH = Math.Max(0.0, _editor.ExtentWidth - _editor.ViewportWidth);
            _editor.ScrollToHorizontalOffset(Math.Clamp(targetH, 0.0, maxH));
        }
    }

    public void Stop()
    {
        if (!_isAutoScrolling) return;

        _timer.Stop();
        _isAutoScrolling = false;
        _isDragGesture = false;
        _currentDeltaX = 0;
        _currentDeltaY = 0;

        if (_anchorIndicator != null)
        {
            _anchorIndicator.IsVisible = false;
        }

        try
        {
            _capturedPointer?.Capture(null);
        }
        catch
        {
        }
        _capturedPointer = null;

        _editor.Cursor = null;
    }

    private void UpdateCursor(double dx, double dy)
    {
        var cursorType = GetAutoScrollCursor(dx, dy, DeadZone);
        _editor.Cursor = new Cursor(cursorType);
    }

    public static (double vx, double vy) CalculateScrollVelocity(double dx, double dy, double deadZone = DeadZone, double speedFactor = DefaultSpeedFactor)
    {
        double vx = 0;
        double vy = 0;

        var absX = Math.Abs(dx);
        if (absX > deadZone)
        {
            var excessX = absX - deadZone;
            vx = Math.Sign(dx) * Math.Pow(excessX, 1.15) * speedFactor;
        }

        var absY = Math.Abs(dy);
        if (absY > deadZone)
        {
            var excessY = absY - deadZone;
            vy = Math.Sign(dy) * Math.Pow(excessY, 1.15) * speedFactor;
        }

        return (vx, vy);
    }

    public static StandardCursorType GetAutoScrollCursor(double dx, double dy, double deadZone = DeadZone)
    {
        var absX = Math.Abs(dx);
        var absY = Math.Abs(dy);

        if (absX <= deadZone && absY <= deadZone)
        {
            return StandardCursorType.SizeAll;
        }

        // Determine dominant direction
        bool isVertical = absY > absX * 1.8;
        bool isHorizontal = absX > absY * 1.8;

        if (isVertical)
        {
            return dy < 0 ? StandardCursorType.TopSide : StandardCursorType.BottomSide;
        }
        if (isHorizontal)
        {
            return dx < 0 ? StandardCursorType.LeftSide : StandardCursorType.RightSide;
        }

        if (dy < 0 && dx < 0) return StandardCursorType.TopLeftCorner;
        if (dy < 0 && dx > 0) return StandardCursorType.TopRightCorner;
        if (dy > 0 && dx < 0) return StandardCursorType.BottomLeftCorner;
        if (dy > 0 && dx > 0) return StandardCursorType.BottomRightCorner;

        return StandardCursorType.SizeAll;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();

        _editor.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _editor.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _editor.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        _editor.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel);
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }
}
