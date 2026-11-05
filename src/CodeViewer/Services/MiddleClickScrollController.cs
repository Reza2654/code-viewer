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
/// Drops a directional compass anchor at the middle-click origin, captures pointer,
/// dynamically updates directional cursors on both Editor and TextArea, and smoothly scrolls
/// the viewport with gentle, controllable, progressive velocity.
/// Supports both click-and-drag and click-to-toggle modes, smoothly dismissed on click, wheel, or Escape.
/// </summary>
public sealed class MiddleClickScrollController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Control? _anchorIndicator;
    private readonly DispatcherTimer _timer;

    private bool _isAutoScrolling;
    private Point _anchorPoint;
    private double _currentDeltaX;
    private double _currentDeltaY;
    private IPointer? _capturedPointer;
    private bool _isDisposed;
    private bool _isEnabled = true;
    private DateTime _pressTime;
    private bool _hasMovedBeyondDeadzone;

    public const double DeadZone = 10.0;
    public const double MaxSpeed = 28.0;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (!_isEnabled && _isAutoScrolling)
            {
                Stop();
            }
        }
    }

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
        if (!_isEnabled) return;

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
        _pressTime = DateTime.UtcNow;
        _hasMovedBeyondDeadzone = false;
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
        if (distSquared > 18.0 * 18.0)
        {
            _hasMovedBeyondDeadzone = true;
        }

        UpdateCursor(_currentDeltaX, _currentDeltaY);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isAutoScrolling) return;

        var holdDurationMs = (DateTime.UtcNow - _pressTime).TotalMilliseconds;

        // If user held down the middle button for > 300ms AND moved away, stop on button release (Drag gesture).
        // If it was a quick click (< 300ms), KEEP AUTOSCROLLING! User is in click-to-scroll mode.
        if (_hasMovedBeyondDeadzone && holdDurationMs > 300 && e.InitialPressMouseButton == MouseButton.Middle)
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
        if (_isAutoScrolling && (e.Key is Key.Escape or Key.Enter or Key.Space))
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

        var (vx, vy) = CalculateScrollVelocity(_currentDeltaX, _currentDeltaY, DeadZone, MaxSpeed);

        if (Math.Abs(vy) > 0.001)
        {
            var currentV = _editor.VerticalOffset;
            var targetV = currentV + vy;
            var maxV = _editor.ExtentHeight - _editor.ViewportHeight;
            if (maxV > 0)
            {
                targetV = Math.Clamp(targetV, 0.0, maxV);
            }
            else
            {
                targetV = Math.Max(0.0, targetV);
            }
            _editor.ScrollToVerticalOffset(targetV);
        }

        if (Math.Abs(vx) > 0.001)
        {
            var currentH = _editor.HorizontalOffset;
            var targetH = currentH + vx;
            var maxH = _editor.ExtentWidth - _editor.ViewportWidth;
            if (maxH > 0)
            {
                targetH = Math.Clamp(targetH, 0.0, maxH);
            }
            else
            {
                targetH = Math.Max(0.0, targetH);
            }
            _editor.ScrollToHorizontalOffset(targetH);
        }
    }

    public void Stop()
    {
        if (!_isAutoScrolling) return;

        _timer.Stop();
        _isAutoScrolling = false;
        _hasMovedBeyondDeadzone = false;
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
        if (_editor.TextArea != null)
        {
            _editor.TextArea.Cursor = new Cursor(StandardCursorType.Ibeam);
        }
    }

    private void UpdateCursor(double dx, double dy)
    {
        var cursorType = GetAutoScrollCursor(dx, dy, DeadZone);
        var cursor = new Cursor(cursorType);
        _editor.Cursor = cursor;
        if (_editor.TextArea != null)
        {
            _editor.TextArea.Cursor = cursor;
        }
    }

    public static (double vx, double vy) CalculateScrollVelocity(double dx, double dy, double deadZone = DeadZone, double maxSpeed = MaxSpeed)
    {
        var absX = Math.Abs(dx);
        var absY = Math.Abs(dy);

        if (absX <= deadZone && absY <= deadZone)
        {
            return (0.0, 0.0);
        }

        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= deadZone)
        {
            return (0.0, 0.0);
        }

        var excess = dist - deadZone;
        // Smooth, progressive velocity curve with controllable acceleration
        var normalized = excess / 60.0;
        var speed = Math.Min(maxSpeed, (excess * 0.08) + (Math.Pow(normalized, 1.8) * 3.5));

        var unitX = dx / dist;
        var unitY = dy / dist;

        return (unitX * speed, unitY * speed);
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
