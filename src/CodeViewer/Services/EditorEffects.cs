using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;

namespace CodeViewer.Services;

/// <summary>
/// Provides high-performance, silky-smooth cursor blinking for AvaloniaEdit.
/// Replaces harsh abrupt blinking with smooth cosine opacity breathing (~60fps),
/// while instantly locking to solid 1.0 opacity on typing or navigation.
/// </summary>
public sealed class SmoothCaretBlinkController : IDisposable
{
    private readonly TextEditor _editor;
    private Control? _caretLayer;
    private DispatcherTimer? _blinkTimer;
    private double _phase;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private bool _isDisposed;

    public SmoothCaretBlinkController(TextEditor editor)
    {
        _editor = editor;
        Attach();
    }

    private void Attach()
    {
        if (_editor.TextArea == null) return;

        try
        {
            var caret = _editor.TextArea.Caret;
            var adornerField = typeof(AvaloniaEdit.Editing.Caret).GetField("_caretAdorner",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _caretLayer = adornerField?.GetValue(caret) as Control;

            if (_caretLayer != null)
            {
                // Stop harsh binary timer
                var timerField = _caretLayer.GetType().GetField("_caretBlinkTimer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var timer = timerField?.GetValue(_caretLayer) as DispatcherTimer;
                timer?.Stop();

                // Keep internal _blink flag true so CaretLayer always renders when Opacity is non-zero
                var blinkField = _caretLayer.GetType().GetField("_blink",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                blinkField?.SetValue(_caretLayer, true);

                _caretLayer.Opacity = 1.0;
            }

            caret.PositionChanged += OnCaretActivity;
            _editor.TextInput += OnTextInput;
            _editor.GotFocus += OnFocusChanged;
            _editor.LostFocus += OnFocusChanged;

            _blinkTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Render, OnBlinkTick);
            _blinkTimer.Start();
        }
        catch
        {
            // Fallback gracefully to default editor behavior
        }
    }

    private void OnCaretActivity(object? sender, EventArgs e) => ResetSolid();
    private void OnTextInput(object? sender, TextInputEventArgs e) => ResetSolid();
    private void OnFocusChanged(object? sender, RoutedEventArgs e) => ResetSolid();

    public void ResetSolid()
    {
        _lastActivityTime = DateTime.UtcNow;
        _phase = 0.0;
        if (_caretLayer != null)
        {
            _caretLayer.Opacity = 1.0;
        }
    }

    private void OnBlinkTick(object? sender, EventArgs e)
    {
        if (_isDisposed || _caretLayer == null) return;

        if (!_editor.IsFocused && !_editor.TextArea.IsFocused)
        {
            _caretLayer.Opacity = 0.0;
            return;
        }

        // Keep 100% solid for 400ms following any user interaction (typing or cursor movement)
        var idleMs = (DateTime.UtcNow - _lastActivityTime).TotalMilliseconds;
        if (idleMs < 400)
        {
            _caretLayer.Opacity = 1.0;
            return;
        }

        // Advance smooth breathing wave (1000ms full oscillation period)
        _phase += (2.0 * Math.PI) * (20.0 / 1000.0);
        if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

        // Smooth cosine curve: oscillates between 1.0 (visible) and 0.05 (dim)
        var wave = 0.5 * (1.0 + Math.Cos(_phase));
        var opacity = 0.05 + (0.95 * Math.Pow(wave, 0.8));

        _caretLayer.Opacity = Math.Clamp(opacity, 0.0, 1.0);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _blinkTimer?.Stop();
        _blinkTimer = null;

        if (_editor.TextArea != null)
        {
            _editor.TextArea.Caret.PositionChanged -= OnCaretActivity;
            _editor.TextInput -= OnTextInput;
            _editor.GotFocus -= OnFocusChanged;
            _editor.LostFocus -= OnFocusChanged;
        }

        if (_caretLayer != null)
        {
            _caretLayer.Opacity = 1.0;
        }
    }
}

/// <summary>
/// Provides silky-smooth 60fps/120fps inertial mouse-wheel scrolling for AvaloniaEdit.
/// Smoothly glides vertical and horizontal scroll offsets using responsive exponential decay lerp without jitter.
/// Synchronizes immediately on pointer press, scrollbar thumb drag, and keyboard navigation.
/// </summary>
public sealed class SmoothScrollController : IDisposable
{
    private readonly TextEditor _editor;
    private DispatcherTimer? _scrollTimer;
    private double _targetVerticalOffset;
    private double _targetHorizontalOffset;
    private bool _isEnabled = true;
    private bool _isDisposed;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                _scrollTimer?.Stop();
            }
        }
    }

    public SmoothScrollController(TextEditor editor)
    {
        _editor = editor;
        _targetVerticalOffset = editor.VerticalOffset;
        _targetHorizontalOffset = editor.HorizontalOffset;

        editor.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        editor.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        _scrollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnScrollTick);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SyncCurrentOffset();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            SyncCurrentOffset();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabled) return;

        // Don't intercept zoom (Ctrl + MouseWheel)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        // Shift + Wheel = Horizontal scroll
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            var delta = Math.Abs(e.Delta.X) > 0.01 ? e.Delta.X : e.Delta.Y;
            if (Math.Abs(delta) > 0.01)
            {
                e.Handled = true;

                if (_scrollTimer == null || !_scrollTimer.IsEnabled)
                {
                    _targetHorizontalOffset = _editor.HorizontalOffset;
                }

                var lineDelta = delta * 60.0;
                _targetHorizontalOffset -= lineDelta;

                var maxOffsetH = Math.Max(0.0, _editor.ExtentWidth - _editor.ViewportWidth);
                _targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset, 0.0, maxOffsetH);

                var currentH = _editor.HorizontalOffset;
                var maxLeadH = Math.Max(300.0, _editor.ViewportWidth * 2.0);
                _targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset, currentH - maxLeadH, currentH + maxLeadH);

                if (_scrollTimer != null && !_scrollTimer.IsEnabled)
                {
                    _scrollTimer.Start();
                }
            }
            return;
        }

        // Dedicated horizontal tilt/pan wheel
        if (Math.Abs(e.Delta.X) > 0.01 && Math.Abs(e.Delta.Y) <= 0.01)
        {
            e.Handled = true;

            if (_scrollTimer == null || !_scrollTimer.IsEnabled)
            {
                _targetHorizontalOffset = _editor.HorizontalOffset;
            }

            var lineDelta = e.Delta.X * 60.0;
            _targetHorizontalOffset -= lineDelta;

            var maxOffsetH = Math.Max(0.0, _editor.ExtentWidth - _editor.ViewportWidth);
            _targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset, 0.0, maxOffsetH);

            var currentH = _editor.HorizontalOffset;
            var maxLeadH = Math.Max(300.0, _editor.ViewportWidth * 2.0);
            _targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset, currentH - maxLeadH, currentH + maxLeadH);

            if (_scrollTimer != null && !_scrollTimer.IsEnabled)
            {
                _scrollTimer.Start();
            }
            return;
        }

        // Vertical scroll
        if (Math.Abs(e.Delta.Y) > 0.01)
        {
            e.Handled = true;

            if (_scrollTimer == null || !_scrollTimer.IsEnabled)
            {
                _targetVerticalOffset = _editor.VerticalOffset;
            }

            var lineDelta = e.Delta.Y * 60.0;
            _targetVerticalOffset -= lineDelta;

            var maxOffsetV = Math.Max(0.0, _editor.ExtentHeight - _editor.ViewportHeight);
            _targetVerticalOffset = Math.Clamp(_targetVerticalOffset, 0.0, maxOffsetV);

            var currentV = _editor.VerticalOffset;
            var maxLeadV = Math.Max(300.0, _editor.ViewportHeight * 2.5);
            _targetVerticalOffset = Math.Clamp(_targetVerticalOffset, currentV - maxLeadV, currentV + maxLeadV);

            if (_scrollTimer != null && !_scrollTimer.IsEnabled)
            {
                _scrollTimer.Start();
            }
        }
    }

    private void OnScrollTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        var currentV = _editor.VerticalOffset;
        var diffV = _targetVerticalOffset - currentV;

        var currentH = _editor.HorizontalOffset;
        var diffH = _targetHorizontalOffset - currentH;

        bool doneV = Math.Abs(diffV) < 0.8;
        bool doneH = Math.Abs(diffH) < 0.8;

        if (doneV && doneH)
        {
            _editor.ScrollToVerticalOffset(_targetVerticalOffset);
            _editor.ScrollToHorizontalOffset(_targetHorizontalOffset);
            _scrollTimer?.Stop();
        }
        else
        {
            if (!doneV)
            {
                var stepV = diffV * 0.38;
                if (Math.Abs(stepV) < 1.0)
                {
                    stepV = Math.Sign(diffV) * Math.Min(Math.Abs(diffV), 1.0);
                }
                _editor.ScrollToVerticalOffset(currentV + stepV);
            }
            else
            {
                _editor.ScrollToVerticalOffset(_targetVerticalOffset);
            }

            if (!doneH)
            {
                var stepH = diffH * 0.38;
                if (Math.Abs(stepH) < 1.0)
                {
                    stepH = Math.Sign(diffH) * Math.Min(Math.Abs(diffH), 1.0);
                }
                _editor.ScrollToHorizontalOffset(currentH + stepH);
            }
            else
            {
                _editor.ScrollToHorizontalOffset(_targetHorizontalOffset);
            }
        }
    }

    public void SyncCurrentOffset()
    {
        _targetVerticalOffset = _editor.VerticalOffset;
        _targetHorizontalOffset = _editor.HorizontalOffset;
        _scrollTimer?.Stop();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _scrollTimer?.Stop();
        _scrollTimer = null;
        _editor.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _editor.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }
}
