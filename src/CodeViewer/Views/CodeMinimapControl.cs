using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;

namespace CodeViewer.Views;

/// <summary>
/// High-performance code minimap providing a birds-eye document overview and interactive scroll slider.
/// </summary>
public class CodeMinimapControl : Control
{
    private TextEditor? _editor;
    private bool _isDragging;
    private bool _isHovered;

    private static readonly IBrush NormalThumbBrush = new SolidColorBrush(Color.FromArgb(40, 200, 200, 200));
    private static readonly IBrush HoverThumbBrush = new SolidColorBrush(Color.FromArgb(65, 200, 200, 200));
    private static readonly IPen ThumbPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 220, 220, 220)), 1);

    public CodeMinimapControl()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void Attach(TextEditor? editor)
    {
        if (_editor != null)
        {
            if (_editor.TextArea?.TextView != null)
            {
                _editor.TextArea.TextView.ScrollOffsetChanged -= OnEditorScrolled;
            }
            _editor.TextChanged -= OnEditorTextChanged;
        }

        _editor = editor;

        if (_editor != null)
        {
            if (_editor.TextArea?.TextView != null)
            {
                _editor.TextArea.TextView.ScrollOffsetChanged += OnEditorScrolled;
            }
            _editor.TextChanged += OnEditorTextChanged;
        }

        InvalidateVisual();
    }

    private void OnEditorScrolled(object? sender, EventArgs e) => InvalidateVisual();
    private void OnEditorTextChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovered = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isHovered = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_editor == null) return;

        _isDragging = true;
        e.Pointer.Capture(this);
        ScrollToPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging && _editor != null)
        {
            ScrollToPoint(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            InvalidateVisual();
        }
    }

    private void ScrollToPoint(Point point)
    {
        if (_editor?.TextArea?.TextView == null) return;

        var h = Bounds.Height;
        if (h <= 0) return;

        var doc = _editor.Document;
        var lineCount = doc?.LineCount ?? 0;
        if (lineCount <= 0) return;

        var viewportH = _editor.TextArea.TextView.Bounds.Height;
        var totalH = Math.Max(viewportH, _editor.ExtentHeight > 0 ? _editor.ExtentHeight : lineCount * 18.0);
        var maxScroll = Math.Max(0, totalH - viewportH);

        var ratio = Math.Clamp(point.Y / h, 0, 1);
        var targetScrollY = ratio * maxScroll;

        _editor.ScrollToVerticalOffset(targetScrollY);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 10 || h <= 10 || _editor == null) return;

        var doc = _editor.Document;
        if (doc == null || doc.LineCount == 0) return;

        var lineCount = doc.LineCount;
        var lineSpacing = Math.Min(3.0, h / Math.Max(1, lineCount));
        var penThickness = Math.Clamp(lineSpacing - 0.5, 0.8, 2.0);

        // Resolve code line brush from editor foreground
        var fg = _editor.Foreground as ISolidColorBrush;
        var fgColor = fg?.Color ?? Colors.LightGray;
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(90, fgColor.R, fgColor.G, fgColor.B)), penThickness);

        // Draw miniature code lines
        var step = lineSpacing < 1.0 ? (int)Math.Ceiling(1.0 / lineSpacing) : 1;
        for (int i = 1; i <= lineCount; i += step)
        {
            var line = doc.GetLineByNumber(i);
            if (line.Length == 0) continue;

            var lineText = doc.GetText(line.Offset, line.Length);
            if (string.IsNullOrWhiteSpace(lineText)) continue;

            int leadingSpaces = 0;
            while (leadingSpaces < lineText.Length && char.IsWhiteSpace(lineText[leadingSpaces]))
            {
                leadingSpaces += (lineText[leadingSpaces] == '\t' ? 4 : 1);
            }

            double startX = Math.Min(w - 12, 4 + leadingSpaces * 1.5);
            double length = Math.Min(w - startX - 4, (lineText.Length - leadingSpaces) * 1.3);
            if (length <= 0) length = 3;

            double y = (i - 1) * lineSpacing;
            context.DrawLine(linePen, new Point(startX, y), new Point(startX + length, y));
        }

        // Compute and draw viewport slider thumb
        var textView = _editor.TextArea?.TextView;
        if (textView != null)
        {
            var scrollY = textView.ScrollOffset.Y;
            var viewportH = textView.Bounds.Height;
            var totalH = Math.Max(viewportH, _editor.ExtentHeight > 0 ? _editor.ExtentHeight : lineCount * 18.0);

            var thumbTop = (scrollY / totalH) * h;
            var thumbHeight = Math.Max(18, (viewportH / totalH) * h);
            if (thumbTop + thumbHeight > h)
            {
                thumbTop = Math.Max(0, h - thumbHeight);
            }

            var thumbRect = new Rect(2, thumbTop, w - 4, thumbHeight);
            var thumbBrush = (_isDragging || _isHovered) ? HoverThumbBrush : NormalThumbBrush;

            context.FillRectangle(thumbBrush, thumbRect, 3);
            context.DrawRectangle(ThumbPen, thumbRect, 3);
        }
    }
}
