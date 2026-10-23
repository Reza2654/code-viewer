using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace CodeViewer.Rendering;

/// <summary>
/// Transformer that applies distinct, harmonious rainbow colors to matching nested bracket pairs () [] {}.
/// </summary>
public class RainbowBracketColorizer : DocumentColorizingTransformer
{
    public bool IsEnabled { get; set; } = true;

    public static readonly IBrush[] RainbowBrushes =
    [
        new SolidColorBrush(Color.Parse("#FFD700")), // Gold
        new SolidColorBrush(Color.Parse("#DA70D6")), // Orchid
        new SolidColorBrush(Color.Parse("#179FFF")), // Sky Cyan
        new SolidColorBrush(Color.Parse("#FF8C00")), // Dark Orange
        new SolidColorBrush(Color.Parse("#50FA7B")), // Bright Green
        new SolidColorBrush(Color.Parse("#FF79C6"))  // Pink
    ];

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!IsEnabled || CurrentContext.Document == null) return;

        var text = CurrentContext.Document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(text)) return;

        var depth = ComputeNestingDepthAt(line.Offset);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(' || c == '[' || c == '{')
            {
                var brush = RainbowBrushes[Math.Abs(depth) % RainbowBrushes.Length];
                depth++;
                var start = line.Offset + i;
                ChangeLinePart(start, start + 1, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                });
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                depth--;
                var brush = RainbowBrushes[Math.Abs(depth) % RainbowBrushes.Length];
                var start = line.Offset + i;
                ChangeLinePart(start, start + 1, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                });
            }
        }
    }

    private int ComputeNestingDepthAt(int targetOffset)
    {
        var doc = CurrentContext.Document;
        if (doc == null || targetOffset <= 0) return 0;

        var scanStart = Math.Max(0, targetOffset - 10000);
        var text = doc.GetText(scanStart, targetOffset - scanStart);

        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}') depth--;
        }

        return depth;
    }
}
