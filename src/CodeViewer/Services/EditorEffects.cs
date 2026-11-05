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
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (!_isEnabled)
            {
                _blinkTimer?.Stop();
                if (_caretLayer != null)
                {
                    _caretLayer.Opacity = 1.0;
                }
            }
            else
            {
                ResetSolid();
                _blinkTimer?.Start();
            }
        }
    }

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
        if (_isDisposed || _caretLayer == null || !_isEnabled) return;

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
