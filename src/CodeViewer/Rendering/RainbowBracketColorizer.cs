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

    public static readonly IBrush[] DarkRainbowBrushes =
    [
        new SolidColorBrush(Color.Parse("#FFD700")), // Gold
        new SolidColorBrush(Color.Parse("#DA70D6")), // Orchid
        new SolidColorBrush(Color.Parse("#179FFF")), // Sky Cyan
        new SolidColorBrush(Color.Parse("#FF9E64")), // Warm Orange
        new SolidColorBrush(Color.Parse("#50FA7B")), // Bright Green
        new SolidColorBrush(Color.Parse("#FF79C6"))  // Pink
    ];

    public static readonly IBrush[] LightRainbowBrushes =
    [
        new SolidColorBrush(Color.Parse("#B8860B")), // Dark Goldenrod
        new SolidColorBrush(Color.Parse("#8A2BE2")), // Blue Violet
        new SolidColorBrush(Color.Parse("#007ACC")), // Deep Blue
        new SolidColorBrush(Color.Parse("#D9534F")), // Coral Crimson
        new SolidColorBrush(Color.Parse("#2E7D32")), // Forest Green
        new SolidColorBrush(Color.Parse("#C2185B"))  // Dark Berry Pink
    ];

    public static readonly IBrush[] RainbowBrushes = DarkRainbowBrushes;

    private IBrush[] _activeBrushes = DarkRainbowBrushes;

    public void UpdateTheme(bool isDark)
    {
        _activeBrushes = isDark ? DarkRainbowBrushes : LightRainbowBrushes;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!IsEnabled || CurrentContext.Document == null) return;

        var text = CurrentContext.Document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(text)) return;

        var depth = ComputeNestingDepthAt(line.Offset);
        var brushes = _activeBrushes;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(' || c == '[' || c == '{')
            {
                var brush = brushes[Math.Abs(depth) % brushes.Length];
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
                var brush = brushes[Math.Abs(depth) % brushes.Length];
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
