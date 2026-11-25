using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace CodeViewer.Rendering;

public static class MarkdownRenderer
{
    private readonly record struct MarkdownPalette(
        IBrush InlineCode,
        IBrush Link,
        IBrush CheckedTask,
        IBrush BlockquoteAccent,
        IBrush StrikeText,
        IBrush MutedText,
        IBrush CodeBackground,
        IBrush CodeBorder);

    private static readonly FontFamily MonospaceFamily = new("Google Sans Code, avares://CodeViewer/Assets/Fonts#Google Sans Code, Roboto Mono, avares://CodeViewer/Assets/Fonts#Roboto Mono, Cascadia Code, Consolas, Courier New, monospace");
    private static readonly SearchValues<char> InlineTokenStartChars = SearchValues.Create(['`', '~', '*', '[']);
    private static readonly IBrush DefaultTableHeaderBg = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
    private static readonly IBrush DefaultTableAltRowBg = new SolidColorBrush(Color.FromArgb(15, 128, 128, 128));
    private static readonly IBrush DefaultCodeBg = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
    private static readonly IBrush DefaultCodeBorder = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));

    private static readonly MarkdownPalette DarkPalette = new(
        InlineCode: Brushes.SandyBrown,
        Link: Brushes.DeepSkyBlue,
        CheckedTask: Brushes.LimeGreen,
        BlockquoteAccent: Brushes.CornflowerBlue,
        StrikeText: Brushes.Gray,
        MutedText: Brushes.Gray,
        CodeBackground: DefaultCodeBg,
        CodeBorder: DefaultCodeBorder);

    private static readonly MarkdownPalette LightPalette = new(
        InlineCode: new SolidColorBrush(Color.Parse("#A31515")),
        Link: new SolidColorBrush(Color.Parse("#0550AE")),
        CheckedTask: new SolidColorBrush(Color.Parse("#1B7F37")),
        BlockquoteAccent: new SolidColorBrush(Color.Parse("#0969DA")),
        StrikeText: new SolidColorBrush(Color.Parse("#57606A")),
        MutedText: new SolidColorBrush(Color.Parse("#57606A")),
        CodeBackground: new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
        CodeBorder: new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));

    private static MarkdownPalette ResolvePalette(IBrush? foreground, IBrush? codeBackground)
    {
        if (foreground is ISolidColorBrush fgSolid)
        {
            var fgLuma = (0.2126 * fgSolid.Color.R + 0.7152 * fgSolid.Color.G + 0.0722 * fgSolid.Color.B) / 255.0;
            return fgLuma < 0.50 ? LightPalette : DarkPalette;
        }

        if (codeBackground is ISolidColorBrush bgSolid && bgSolid.Color.A > 100)
        {
            var bgLuma = (0.2126 * bgSolid.Color.R + 0.7152 * bgSolid.Color.G + 0.0722 * bgSolid.Color.B) / 255.0;
            if (bgLuma > 0.50) return LightPalette;
        }

        return DarkPalette;
    }

    public static Control Render(string markdown, IBrush? foreground = null, IBrush? borderBrush = null, IBrush? codeBackground = null)
    {
        var palette = ResolvePalette(foreground, codeBackground);
        var panel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(16, 12, 16, 24)
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "(Empty Markdown Document)",
                Foreground = palette.MutedText,
                FontStyle = FontStyle.Italic
            });
            return panel;
        }

        var lines = SplitLinesFast(markdown);
        bool inCodeBlock = false;
        var codeBlockLines = new List<string>();

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];

            // Handle fenced code blocks
            if (line.AsSpan().TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    panel.Children.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines), foreground, codeBackground, borderBrush, palette));
                    codeBlockLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                    codeBlockLines.Clear();
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            var trimmedSpan = line.AsSpan().Trim();

            // Horizontal rule
            if (trimmedSpan is "---" or "***" or "___")
            {
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = borderBrush ?? palette.MutedText,
                    Opacity = 0.4,
                    Margin = new Thickness(0, 8, 0, 8)
                });
                continue;
            }

            // Headings
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[2..], 22, FontWeight.Bold, foreground, new Thickness(0, 10, 0, 4), palette));
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[3..], 18, FontWeight.Bold, foreground, new Thickness(0, 8, 0, 3), palette));
                continue;
            }
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[4..], 15, FontWeight.SemiBold, foreground, new Thickness(0, 6, 0, 2), palette));
                continue;
            }
            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[5..], 13, FontWeight.SemiBold, foreground, new Thickness(0, 4, 0, 2), palette));
                continue;
            }
            if (line.StartsWith("##### ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[6..], 12, FontWeight.SemiBold, foreground, new Thickness(0, 3, 0, 1), palette));
                continue;
            }
            if (line.StartsWith("###### ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateHeading(line[7..], 11, FontWeight.SemiBold, foreground, new Thickness(0, 2, 0, 1), palette));
                continue;
            }

            // Tables: begins and ends with |
            if (trimmedSpan.Length > 2 && trimmedSpan[0] == '|' && trimmedSpan[^1] == '|')
            {
                var tableLines = new List<string> { line };
                while (lineIndex + 1 < lines.Count)
                {
                    var nextSpan = lines[lineIndex + 1].AsSpan().Trim();
                    if (nextSpan.Length >= 2 && nextSpan[0] == '|' && nextSpan[^1] == '|')
                    {
                        lineIndex++;
                        tableLines.Add(lines[lineIndex]);
                    }
                    else
                    {
                        break;
                    }
                }

                panel.Children.Add(CreateTable(tableLines, foreground, borderBrush, codeBackground, palette));
                continue;
            }

            // Blockquote
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                panel.Children.Add(CreateBlockquote(line[2..], foreground, borderBrush, palette));
                continue;
            }

            // Task list item
            if (trimmedSpan.StartsWith("- [ ] ") || trimmedSpan.StartsWith("* [ ] "))
            {
                panel.Children.Add(CreateTaskListItem(false, trimmedSpan[6..].ToString(), foreground, palette));
                continue;
            }
            if (trimmedSpan.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || trimmedSpan.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
            {
                panel.Children.Add(CreateTaskListItem(true, trimmedSpan[6..].ToString(), foreground, palette));
                continue;
            }

            // Bullet list
            if (trimmedSpan.StartsWith("- ") || trimmedSpan.StartsWith("* "))
            {
                panel.Children.Add(CreateListItem("•", trimmedSpan[2..].ToString(), foreground, palette));
                continue;
            }

            // Numbered list
            if (trimmedSpan.Length > 2 && char.IsDigit(trimmedSpan[0]) && (trimmedSpan[1] == '.' || (trimmedSpan.Length > 3 && char.IsDigit(trimmedSpan[1]) && trimmedSpan[2] == '.')))
            {
                int dotIdx = trimmedSpan.IndexOf('.');
                var num = trimmedSpan[..(dotIdx + 1)].ToString();
                var itemText = trimmedSpan[(dotIdx + 1)..].TrimStart().ToString();
                panel.Children.Add(CreateListItem(num, itemText, foreground, palette));
                continue;
            }

            // Empty line
            if (trimmedSpan.IsEmpty)
            {
                panel.Children.Add(new Border { Height = 4 });
                continue;
            }

            // Regular paragraph
            panel.Children.Add(CreateParagraph(line, foreground, palette));
        }

        // Unclosed code block fallback
        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            panel.Children.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines), foreground, codeBackground, borderBrush, palette));
        }

        return panel;
    }

    private static List<string> SplitLinesFast(string text)
    {
        var lines = new List<string>(Math.Max(16, text.Length / 40));
        int start = 0;
        int len = text.Length;
        for (int i = 0; i < len; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n')
            {
                lines.Add(text.Substring(start, i - start));
                if (c == '\r' && i + 1 < len && text[i + 1] == '\n')
                {
                    i++;
                }
                start = i + 1;
            }
        }
        lines.Add(start < len ? text.Substring(start) : string.Empty);
        return lines;
    }

    public static bool IsRtlText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if ((c >= 0x0600 && c <= 0x06FF) ||
                (c >= 0x0750 && c <= 0x077F) ||
                (c >= 0x08A0 && c <= 0x08FF) ||
                (c >= 0xFB50 && c <= 0xFDFF) ||
                (c >= 0xFE70 && c <= 0xFEFF) ||
                (c >= 0x0590 && c <= 0x05FF))
            {
                return true;
            }
        }
        return false;
    }

    private static TextBlock CreateHeading(string text, double fontSize, FontWeight weight, IBrush? fg, Thickness margin, MarkdownPalette palette)
    {
        bool isRtl = IsRtlText(text);
        var tb = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            Margin = margin,
            TextWrapping = TextWrapping.Wrap,
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        if (fg != null) tb.Foreground = fg;
        AddFormattedInlines(tb, text, palette);
        return tb;
    }

    private static Border CreateBlockquote(string text, IBrush? fg, IBrush? borderBrush, MarkdownPalette palette)
    {
        bool isRtl = IsRtlText(text);
        var tb = new TextBlock
        {
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        if (fg != null) tb.Foreground = fg;
        AddFormattedInlines(tb, text, palette);

        return new Border
        {
            BorderThickness = isRtl ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
            BorderBrush = borderBrush ?? palette.BlockquoteAccent,
            Padding = isRtl ? new Thickness(4, 4, 10, 4) : new Thickness(10, 4, 4, 4),
            Margin = new Thickness(0, 2, 0, 4),
            Child = tb
        };
    }

    private static ColumnDefinitions CreateSideColumns(double fixedWidth, bool fixedOnRight) =>
        fixedOnRight
            ? new ColumnDefinitions { new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(fixedWidth, GridUnitType.Pixel) }
            : new ColumnDefinitions { new ColumnDefinition(fixedWidth, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star) };

    private static Grid CreateListItem(string bullet, string text, IBrush? fg, MarkdownPalette palette)
    {
        bool isRtl = IsRtlText(text);
        var grid = new Grid
        {
            ColumnDefinitions = CreateSideColumns(20, isRtl),
            Margin = new Thickness(4, 1, 0, 1)
        };

        var bulletBlock = new TextBlock
        {
            Text = bullet,
            FontWeight = FontWeight.Bold,
            Foreground = fg ?? palette.BlockquoteAccent,
            VerticalAlignment = VerticalAlignment.Top,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        Grid.SetColumn(bulletBlock, isRtl ? 1 : 0);

        var contentBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        if (fg != null) contentBlock.Foreground = fg;
        AddFormattedInlines(contentBlock, text, palette);
        Grid.SetColumn(contentBlock, isRtl ? 0 : 1);

        grid.Children.Add(bulletBlock);
        grid.Children.Add(contentBlock);
        return grid;
    }

    private static Grid CreateTaskListItem(bool isChecked, string text, IBrush? fg, MarkdownPalette palette)
    {
        bool isRtl = IsRtlText(text);
        var grid = new Grid
        {
            ColumnDefinitions = CreateSideColumns(22, isRtl),
            Margin = new Thickness(4, 2, 0, 2)
        };

        var checkBlock = new TextBlock
        {
            Text = isChecked ? "☑" : "☐",
            FontWeight = FontWeight.Bold,
            Foreground = isChecked ? palette.CheckedTask : (fg ?? palette.MutedText),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        Grid.SetColumn(checkBlock, isRtl ? 1 : 0);

        var contentBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left,
            Opacity = isChecked ? 0.7 : 1.0
        };
        if (fg != null) contentBlock.Foreground = fg;
        AddFormattedInlines(contentBlock, text, palette);
        if (isChecked)
        {
            contentBlock.TextDecorations = TextDecorations.Strikethrough;
        }
        Grid.SetColumn(contentBlock, isRtl ? 0 : 1);

        grid.Children.Add(checkBlock);
        grid.Children.Add(contentBlock);
        return grid;
    }

    private static bool IsSeparatorRow(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (c != '-' && c != '|' && c != ':' && c != ' ')
            {
                return false;
            }
        }
        return true;
    }

    private static Control CreateTable(List<string> tableLines, IBrush? fg, IBrush? borderBrush, IBrush? headerBg, MarkdownPalette palette)
    {
        if (tableLines.Count == 0) return new Border();

        var parsedRows = new List<string[]>(tableLines.Count);
        int colCount = 0;

        foreach (var line in tableLines)
        {
            var span = line.AsSpan().Trim();
            if (!span.IsEmpty && span[0] == '|') span = span[1..];
            if (!span.IsEmpty && span[^1] == '|') span = span[..^1];

            // If line is separator like ---|---|---, skip adding as data row
            if (IsSeparatorRow(span))
            {
                continue;
            }

            var cells = span.ToString().Split('|');
            for (int k = 0; k < cells.Length; k++)
            {
                cells[k] = cells[k].Trim();
            }
            colCount = Math.Max(colCount, cells.Length);
            parsedRows.Add(cells);
        }

        if (parsedRows.Count == 0 || colCount == 0) return new Border();

        var resolvedHeaderBg = headerBg ?? DefaultTableHeaderBg;
        var resolvedBorder = borderBrush ?? palette.MutedText;

        var grid = new Grid
        {
            Margin = new Thickness(0, 4, 0, 8)
        };

        for (int c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        }

        for (int r = 0; r < parsedRows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var rowCells = parsedRows[r];
            bool isHeader = (r == 0);

            for (int c = 0; c < colCount; c++)
            {
                var cellText = c < rowCells.Length ? rowCells[c] : string.Empty;

                var tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = isHeader ? FontWeight.Bold : FontWeight.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = isHeader ? 12.5 : 12
                };
                if (fg != null) tb.Foreground = fg;
                AddFormattedInlines(tb, cellText, palette);

                var cellBorder = new Border
                {
                    Background = isHeader
                        ? resolvedHeaderBg
                        : (r % 2 == 1 ? DefaultTableAltRowBg : Brushes.Transparent),
                    BorderBrush = resolvedBorder,
                    BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, r < parsedRows.Count - 1 ? 1 : 0),
                    Padding = new Thickness(8, 5),
                    Child = tb
                };

                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        return new Border
        {
            BorderBrush = resolvedBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = grid
        };
    }

    private static Border CreateCodeBlock(string code, IBrush? fg, IBrush? background, IBrush? borderBrush, MarkdownPalette palette)
    {
        var tb = new TextBlock
        {
            Text = code,
            FontFamily = MonospaceFamily,
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };
        if (fg != null) tb.Foreground = fg;

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = tb
        };

        return new Border
        {
            Background = background ?? palette.CodeBackground,
            BorderBrush = borderBrush ?? palette.CodeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 4, 0, 6),
            Child = scroller
        };
    }

    private static TextBlock CreateParagraph(string line, IBrush? fg, MarkdownPalette palette)
    {
        bool isRtl = IsRtlText(line);
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
        };
        if (fg != null) tb.Foreground = fg;
        AddFormattedInlines(tb, line, palette);
        return tb;
    }

    private static void AddFormattedInlines(TextBlock tb, string text, MarkdownPalette palette)
    {
        if (string.IsNullOrEmpty(text))
        {
            tb.Text = text;
            return;
        }

        // Fast-path: pure text with zero inline token delimiters needs no Inlines or Run allocations
        if (text.AsSpan().IndexOfAny(InlineTokenStartChars) < 0)
        {
            tb.Text = text;
            return;
        }

        int i = 0;
        int len = text.Length;

        while (i < len)
        {
            // Code `inline`
            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    var snippet = text.Substring(i + 1, end - i - 1);
                    tb.Inlines?.Add(new Run(snippet)
                    {
                        FontFamily = MonospaceFamily,
                        FontSize = tb.FontSize * 0.92,
                        Foreground = palette.InlineCode
                    });
                    i = end + 1;
                    continue;
                }
            }

            // Strikethrough ~~strike~~
            if (i + 1 < len && text[i] == '~' && text[i + 1] == '~')
            {
                int end = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    var snippet = text.Substring(i + 2, end - i - 2);
                    tb.Inlines?.Add(new Run(snippet)
                    {
                        TextDecorations = TextDecorations.Strikethrough,
                        Foreground = palette.StrikeText
                    });
                    i = end + 2;
                    continue;
                }
            }

            // Bold **bold**
            if (i + 1 < len && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    var snippet = text.Substring(i + 2, end - i - 2);
                    tb.Inlines?.Add(new Run(snippet)
                    {
                        FontWeight = FontWeight.Bold
                    });
                    i = end + 2;
                    continue;
                }
            }

            // Italic *italic*
            if (text[i] == '*')
            {
                int end = text.IndexOf('*', i + 1);
                if (end > i)
                {
                    var snippet = text.Substring(i + 1, end - i - 1);
                    tb.Inlines?.Add(new Run(snippet)
                    {
                        FontStyle = FontStyle.Italic
                    });
                    i = end + 1;
                    continue;
                }
            }

            // Link [text](url)
            if (text[i] == '[')
            {
                int closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket > i && closeBracket + 1 < len && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > closeBracket + 1)
                    {
                        var linkText = text.Substring(i + 1, closeBracket - i - 1);
                        tb.Inlines?.Add(new Run(linkText)
                        {
                            Foreground = palette.Link,
                            TextDecorations = TextDecorations.Underline
                        });
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            // Plain text chunk until next inline token candidate (SIMD-accelerated span scan)
            int nextRel = i + 1 < len
                ? text.AsSpan(i + 1).IndexOfAny(InlineTokenStartChars)
                : -1;
            int nextToken = nextRel >= 0 ? i + 1 + nextRel : len;

            var chunk = text.Substring(i, nextToken - i);
            tb.Inlines?.Add(new Run(chunk));
            i = nextToken;
        }
    }
}
