using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CodeViewer.Models;

namespace CodeViewer.Rendering;

/// <summary>
/// Background renderer that automatically highlights all occurrences of the word/identifier
/// under the caret or within the active selection.
/// </summary>
public class WordOccurrenceRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly List<int> _matches = [];
    private int _wordLength;
    private int _activeWordOffset = -1;
    private bool _isEnabled = true;

    private IBrush _matchBrush;
    private IPen _matchPen;
    private IBrush _activeBrush;
    private IPen _activePen;

    public KnownLayer Layer => KnownLayer.Selection;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    Clear();
                }
            }
        }
    }

    public WordOccurrenceRenderer(TextEditor editor)
    {
        _editor = editor;

        // Default Dark Palette
        _matchBrush = new SolidColorBrush(Color.FromArgb(35, 130, 175, 255));
        _matchPen = new Pen(new SolidColorBrush(Color.FromArgb(85, 130, 175, 255)), 1);
        _activeBrush = new SolidColorBrush(Color.FromArgb(50, 130, 175, 255));
        _activePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 130, 175, 255)), 1);
    }

    public void UpdateTheme(ColorTheme theme)
    {
        try
        {
            var accent = Color.Parse(theme.AccentColor);
            var isDark = theme.IsDark;

            byte matchAlpha = isDark ? (byte)35 : (byte)25;
            byte penAlpha = isDark ? (byte)90 : (byte)80;
            byte activeAlpha = isDark ? (byte)55 : (byte)45;
            byte activePenAlpha = isDark ? (byte)150 : (byte)140;

            _matchBrush = new SolidColorBrush(Color.FromArgb(matchAlpha, accent.R, accent.G, accent.B));
            _matchPen = new Pen(new SolidColorBrush(Color.FromArgb(penAlpha, accent.R, accent.G, accent.B)), 1);
            _activeBrush = new SolidColorBrush(Color.FromArgb(activeAlpha, accent.R, accent.G, accent.B));
            _activePen = new Pen(new SolidColorBrush(Color.FromArgb(activePenAlpha, accent.R, accent.G, accent.B)), 1);
        }
        catch
        {
            // Fallback gracefully
        }
    }

    public void Clear()
    {
        _matches.Clear();
        _wordLength = 0;
        _activeWordOffset = -1;
    }

    public void UpdateOccurrences()
    {
        if (!_isEnabled)
        {
            Clear();
            return;
        }

        var doc = _editor.Document;
        if (doc == null || doc.TextLength == 0)
        {
            Clear();
            return;
        }

        string word;
        int wordOffset;

        // Check if there is an active selection
        if (_editor.SelectionLength > 0)
        {
            if (_editor.SelectionLength > 80)
            {
                Clear();
                return;
            }

            var selText = _editor.SelectedText.Trim();
            if (selText.Length < 2 || !IsWord(selText))
            {
                Clear();
                return;
            }

            word = selText;
            wordOffset = _editor.SelectionStart;
        }
        else
        {
            // Extract word under caret
            var caret = _editor.CaretOffset;
            if (caret < 0 || caret > doc.TextLength)
            {
                Clear();
                return;
            }

            var (w, start) = GetWordAtOffset(doc, caret);
            if (string.IsNullOrEmpty(w) || w.Length < 2 || w.Length > 80 || !IsWord(w))
            {
                Clear();
                return;
            }

            word = w;
            wordOffset = start;
        }

        FindAllWordOccurrences(doc, word, wordOffset);
    }

    private static bool IsWord(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static (string Word, int StartOffset) GetWordAtOffset(TextDocument doc, int offset)
    {
        var textLength = doc.TextLength;
        if (textLength == 0) return (string.Empty, -1);

        // Position offset within bounds
        var checkOffset = Math.Clamp(offset, 0, textLength - 1);
        var curr = doc.GetCharAt(checkOffset);

        if (!IsWordChar(curr))
        {
            if (checkOffset > 0 && IsWordChar(doc.GetCharAt(checkOffset - 1)))
            {
                checkOffset--;
            }
            else
            {
                return (string.Empty, -1);
            }
        }

        int start = checkOffset;
        while (start > 0 && IsWordChar(doc.GetCharAt(start - 1)))
        {
            start--;
        }

        int end = checkOffset;
        while (end < textLength - 1 && IsWordChar(doc.GetCharAt(end + 1)))
        {
            end++;
        }

        int length = (end - start) + 1;
        if (length <= 0 || length > 80) return (string.Empty, -1);

        return (doc.GetText(start, length), start);
    }

    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void FindAllWordOccurrences(TextDocument doc, string word, int activeOffset)
    {
        _matches.Clear();
        _wordLength = word.Length;
        _activeWordOffset = activeOffset;

        var text = doc.Text;
        if (string.IsNullOrEmpty(text) || text.Length < word.Length)
        {
            return;
        }

        int index = 0;
        int wordLen = word.Length;
        int textLen = text.Length;

        while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
        {
            // Verify whole word boundary
            bool isLeftBoundary = (index == 0) || !IsWordChar(text[index - 1]);
            bool isRightBoundary = (index + wordLen >= textLen) || !IsWordChar(text[index + wordLen]);

            if (isLeftBoundary && isRightBoundary)
            {
                _matches.Add(index);
            }

            index += wordLen;
            if (index >= textLen) break;
        }

        // Only display highlights if there are multiple occurrences in the document
        if (_matches.Count <= 1)
        {
            _matches.Clear();
            _activeWordOffset = -1;
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0 || _wordLength <= 0 || _editor.Document == null)
            return;

        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0)
            return;

        var firstLine = textView.VisualLines[0];
        var lastLine = textView.VisualLines[^1];
        var visibleStart = firstLine.FirstDocumentLine.Offset;
        var visibleEnd = lastLine.LastDocumentLine.EndOffset;

        foreach (var offset in _matches)
        {
            if (offset < 0 || offset + _wordLength > _editor.Document.TextLength)
                continue;

            // Cull matches outside visible lines
            if (offset + _wordLength < visibleStart || offset > visibleEnd)
                continue;

            var isActive = (offset == _activeWordOffset);
            var builder = new BackgroundGeometryBuilder
            {
                CornerRadius = 2,
                AlignToWholePixels = true
            };

            builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = _wordLength });
            var geometry = builder.CreateGeometry();

            if (geometry != null)
            {
                if (isActive)
                {
                    drawingContext.DrawGeometry(_activeBrush, _activePen, geometry);
                }
                else
                {
                    drawingContext.DrawGeometry(_matchBrush, _matchPen, geometry);
                }
            }
        }
    }
}
