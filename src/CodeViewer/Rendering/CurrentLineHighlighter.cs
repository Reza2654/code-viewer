using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace CodeViewer.Rendering;

/// <summary>
/// Background renderer that visually highlights the current active line where the cursor is placed.
/// </summary>
public class CurrentLineHighlighter : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private IBrush _brush;

    public CurrentLineHighlighter(TextEditor editor)
    {
        _editor = editor;
        _brush = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255));
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void UpdateTheme(bool isDark)
    {
        _brush = isDark
            ? new SolidColorBrush(Color.FromArgb(22, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_editor.Document == null || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
            return;

        var caret = _editor.TextArea?.Caret;
        if (caret == null || caret.Line < 1 || caret.Line > _editor.Document.LineCount)
            return;

        var visualLine = textView.GetVisualLine(caret.Line);
        if (visualLine == null)
            return;

        var lineTop = visualLine.VisualTop - textView.VerticalOffset;
        var width = Math.Max(textView.Bounds.Width, 2000);
        var lineRect = new Rect(0, lineTop, width, visualLine.Height);

        drawingContext.DrawRectangle(_brush, null, lineRect);
    }
}
