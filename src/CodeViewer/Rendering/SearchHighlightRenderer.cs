using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace CodeViewer.Rendering;

/// <summary>
/// Background renderer that highlights all occurrences of the search query and emphasizes the active match.
/// </summary>
public class SearchHighlightRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly List<int> _matches = new();
    private int _searchLength = 0;
    private int _activeMatchOffset = -1;

    private readonly IBrush _allMatchesBrush = new SolidColorBrush(Color.FromArgb(60, 255, 220, 40));
    private readonly IBrush _activeMatchBrush = new SolidColorBrush(Color.FromArgb(120, 255, 180, 0));
    private readonly IPen _activeMatchPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 180, 0)), 1);

    public SearchHighlightRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetMatches(IEnumerable<int> matches, int length, int activeOffset = -1)
    {
        _matches.Clear();
        _matches.AddRange(matches);
        _searchLength = length;
        _activeMatchOffset = activeOffset;
    }

    public void SetActiveMatch(int activeOffset)
    {
        _activeMatchOffset = activeOffset;
    }

    public void Clear()
    {
        _matches.Clear();
        _searchLength = 0;
        _activeMatchOffset = -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0 || _searchLength <= 0 || _editor.Document == null)
            return;

        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0)
            return;

        var firstLine = textView.VisualLines[0];
        var lastLine = textView.VisualLines[^1];
        var visibleStart = firstLine.FirstDocumentLine.Offset;
        var visibleEnd = lastLine.LastDocumentLine.EndOffset;

        foreach (var offset in _matches)
        {
            if (offset < 0 || offset + _searchLength > _editor.Document.TextLength)
                continue;

            // Cull matches completely outside the visible lines
            if (offset + _searchLength < visibleStart || offset > visibleEnd)
                continue;

            var isActive = offset == _activeMatchOffset;
            var builder = new BackgroundGeometryBuilder
            {
                CornerRadius = 1,
                AlignToWholePixels = true
            };

            builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = _searchLength });
            var geometry = builder.CreateGeometry();

            if (geometry != null)
            {
                if (isActive)
                {
                    drawingContext.DrawGeometry(_activeMatchBrush, _activeMatchPen, geometry);
                }
                else
                {
                    drawingContext.DrawGeometry(_allMatchesBrush, null, geometry);
                }
            }
        }
    }
}
