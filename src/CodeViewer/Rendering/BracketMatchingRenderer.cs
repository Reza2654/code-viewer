using System;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CodeViewer.Models;

namespace CodeViewer.Rendering;

/// <summary>
/// Background renderer that automatically detects and highlights matching pairs of parentheses (), brackets [], and braces {}.
/// </summary>
public class BracketMatchingRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private int _firstOffset = -1;
    private int _secondOffset = -1;
    private IBrush _fillBrush;
    private IPen _borderPen;

    public BracketMatchingRenderer(TextEditor editor)
    {
        _editor = editor;
        _fillBrush = new SolidColorBrush(Color.FromArgb(50, 0, 122, 204));
        _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 122, 204)), 1);
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void UpdateTheme(ColorTheme theme)
    {
        try
        {
            var accent = Color.Parse(theme.AccentColor);
            _fillBrush = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B));
            _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)), 1);
        }
        catch
        {
            _fillBrush = new SolidColorBrush(Color.FromArgb(50, 0, 122, 204));
            _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 122, 204)), 1);
        }
    }

    public void UpdateMatchingBrackets()
    {
        var doc = _editor.Document;
        if (doc == null || doc.TextLength == 0)
        {
            Clear();
            return;
        }

        var caretOffset = _editor.CaretOffset;
        var text = doc.Text;

        // Check character right before caret first, then at caret
        var bracketOffset = -1;
        var bracketChar = '\0';

        if (caretOffset > 0 && caretOffset <= text.Length)
        {
            var prevChar = text[caretOffset - 1];
            if (IsBracket(prevChar))
            {
                bracketOffset = caretOffset - 1;
                bracketChar = prevChar;
            }
        }

        if (bracketOffset == -1 && caretOffset < text.Length)
        {
            var currChar = text[caretOffset];
            if (IsBracket(currChar))
            {
                bracketOffset = caretOffset;
                bracketChar = currChar;
            }
        }

        if (bracketOffset == -1)
        {
            Clear();
            return;
        }

        var matchingOffset = FindMatchingBracket(text, bracketOffset, bracketChar);
        if (matchingOffset != -1)
        {
            _firstOffset = bracketOffset;
            _secondOffset = matchingOffset;
        }
        else
        {
            Clear();
        }
    }

    private void Clear()
    {
        _firstOffset = -1;
        _secondOffset = -1;
    }

    private static bool IsBracket(char c)
    {
        return c is '(' or ')' or '[' or ']' or '{' or '}';
    }

    private static int FindMatchingBracket(string text, int offset, char bracket)
    {
        var isOpen = bracket is '(' or '[' or '{';
        var matchingBracket = bracket switch
        {
            '(' => ')',
            ')' => '(',
            '[' => ']',
            ']' => '[',
            '{' => '}',
            '}' => '{',
            _ => '\0'
        };

        if (matchingBracket == '\0') return -1;

        if (isOpen)
        {
            var depth = 1;
            for (var i = offset + 1; i < text.Length; i++)
            {
                var c = text[i];
                if (c == bracket)
                {
                    depth++;
                }
                else if (c == matchingBracket)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }
        else
        {
            var depth = 1;
            for (var i = offset - 1; i >= 0; i--)
            {
                var c = text[i];
                if (c == bracket)
                {
                    depth++;
                }
                else if (c == matchingBracket)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }

        return -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_firstOffset < 0 || _secondOffset < 0 || _editor.Document == null)
            return;

        DrawBracketHighlight(textView, drawingContext, _firstOffset);
        DrawBracketHighlight(textView, drawingContext, _secondOffset);
    }

    private void DrawBracketHighlight(TextView textView, DrawingContext drawingContext, int offset)
    {
        if (offset < 0 || offset >= _editor.Document.TextLength) return;

        var builder = new BackgroundGeometryBuilder
        {
            CornerRadius = 2,
            AlignToWholePixels = true
        };

        builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
        var geometry = builder.CreateGeometry();

        if (geometry != null)
        {
            drawingContext.DrawGeometry(_fillBrush, _borderPen, geometry);
        }
    }
}
