using System;
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
    private static readonly FontFamily MonospaceFamily = new("Google Sans Code, Cascadia Code, Consolas, Courier New, monospace");

    public static Control Render(string markdown, IBrush? foreground = null, IBrush? borderBrush = null, IBrush? codeBackground = null)
    {
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
                Foreground = Brushes.Gray,
                FontStyle = FontStyle.Italic
            });
            return panel;
        }

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        bool inCodeBlock = false;
        var codeBlockLines = new List<string>();

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var line = rawLine;

            // Handle fenced code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    panel.Children.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines), codeBackground, borderBrush));
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

            var trimmed = line.Trim();

            // Horizontal rule
            if (trimmed is "---" or "***" or "___")
            {
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = borderBrush ?? Brushes.Gray,
                    Opacity = 0.4,
                    Margin = new Thickness(0, 8, 0, 8)
                });
                continue;
            }

            // Headings
            if (line.StartsWith("# "))
            {
                panel.Children.Add(CreateHeading(line[2..], 22, FontWeight.Bold, foreground, new Thickness(0, 10, 0, 4)));
                continue;
            }
            if (line.StartsWith("## "))
            {
                panel.Children.Add(CreateHeading(line[3..], 18, FontWeight.Bold, foreground, new Thickness(0, 8, 0, 3)));
                continue;
            }
            if (line.StartsWith("### "))
            {
                panel.Children.Add(CreateHeading(line[4..], 15, FontWeight.SemiBold, foreground, new Thickness(0, 6, 0, 2)));
                continue;
            }
            if (line.StartsWith("#### "))
            {
                panel.Children.Add(CreateHeading(line[5..], 13, FontWeight.SemiBold, foreground, new Thickness(0, 4, 0, 2)));
                continue;
            }
            if (line.StartsWith("##### "))
            {
                panel.Children.Add(CreateHeading(line[6..], 12, FontWeight.SemiBold, foreground, new Thickness(0, 3, 0, 1)));
                continue;
            }
            if (line.StartsWith("###### "))
            {
                panel.Children.Add(CreateHeading(line[7..], 11, FontWeight.SemiBold, foreground, new Thickness(0, 2, 0, 1)));
                continue;
            }

            // Tables: begins and ends with |
            if (trimmed.StartsWith('|') && trimmed.EndsWith('|') && trimmed.Length > 2)
            {
                var tableLines = new List<string> { line };
                while (lineIndex + 1 < lines.Length && lines[lineIndex + 1].Trim().StartsWith('|') && lines[lineIndex + 1].Trim().EndsWith('|'))
                {
                    lineIndex++;
                    tableLines.Add(lines[lineIndex]);
                }

                panel.Children.Add(CreateTable(tableLines, foreground, borderBrush, codeBackground));
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                panel.Children.Add(CreateBlockquote(line[2..], foreground, borderBrush));
                continue;
            }

            // Task list item
            if (trimmed.StartsWith("- [ ] ") || trimmed.StartsWith("* [ ] "))
            {
                panel.Children.Add(CreateTaskListItem(false, trimmed[6..], foreground));
                continue;
            }
            if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
            {
                panel.Children.Add(CreateTaskListItem(true, trimmed[6..], foreground));
                continue;
            }

            // Bullet list
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                panel.Children.Add(CreateListItem("•", trimmed[2..], foreground));
                continue;
            }

            // Numbered list
            if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && (trimmed[1] == '.' || (trimmed.Length > 3 && char.IsDigit(trimmed[1]) && trimmed[2] == '.')))
            {
                int dotIdx = trimmed.IndexOf('.');
                var num = trimmed[..(dotIdx + 1)];
                var itemText = trimmed[(dotIdx + 1)..].TrimStart();
                panel.Children.Add(CreateListItem(num, itemText, foreground));
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(line))
            {
                panel.Children.Add(new Border { Height = 4 });
                continue;
            }

            // Regular paragraph
            panel.Children.Add(CreateParagraph(line, foreground));
        }

        // Unclosed code block fallback
        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            panel.Children.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeBlockLines), codeBackground, borderBrush));
        }

        return panel;
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

    private static TextBlock CreateHeading(string text, double fontSize, FontWeight weight, IBrush? fg, Thickness margin)
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
        AddFormattedInlines(tb, text);
        return tb;
    }

    private static Border CreateBlockquote(string text, IBrush? fg, IBrush? borderBrush)
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
        AddFormattedInlines(tb, text);

        return new Border
        {
            BorderThickness = isRtl ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
            BorderBrush = borderBrush ?? Brushes.CornflowerBlue,
            Padding = isRtl ? new Thickness(4, 4, 10, 4) : new Thickness(10, 4, 4, 4),
            Margin = new Thickness(0, 2, 0, 4),
            Child = tb
        };
    }

    private static Grid CreateListItem(string bullet, string text, IBrush? fg)
    {
        bool isRtl = IsRtlText(text);
        var grid = new Grid
        {
            ColumnDefinitions = isRtl ? new ColumnDefinitions("*,20") : new ColumnDefinitions("20,*"),
            Margin = new Thickness(4, 1, 0, 1)
        };

        var bulletBlock = new TextBlock
        {
            Text = bullet,
            FontWeight = FontWeight.Bold,
            Foreground = fg ?? Brushes.CornflowerBlue,
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
        AddFormattedInlines(contentBlock, text);
        Grid.SetColumn(contentBlock, isRtl ? 0 : 1);

        grid.Children.Add(bulletBlock);
        grid.Children.Add(contentBlock);
        return grid;
    }

    private static Grid CreateTaskListItem(bool isChecked, string text, IBrush? fg)
    {
        bool isRtl = IsRtlText(text);
        var grid = new Grid
        {
            ColumnDefinitions = isRtl ? new ColumnDefinitions("*,22") : new ColumnDefinitions("22,*"),
            Margin = new Thickness(4, 2, 0, 2)
        };

        var checkBlock = new TextBlock
        {
            Text = isChecked ? "☑" : "☐",
            FontWeight = FontWeight.Bold,
            Foreground = isChecked ? Brushes.LimeGreen : (fg ?? Brushes.Gray),
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
        AddFormattedInlines(contentBlock, text);
        if (isChecked)
        {
            contentBlock.TextDecorations = TextDecorations.Strikethrough;
        }
        Grid.SetColumn(contentBlock, isRtl ? 0 : 1);

        grid.Children.Add(checkBlock);
        grid.Children.Add(contentBlock);
        return grid;
    }

    private static Control CreateTable(List<string> tableLines, IBrush? fg, IBrush? borderBrush, IBrush? headerBg)
    {
        if (tableLines.Count == 0) return new Border();

        var parsedRows = new List<string[]>();
        int colCount = 0;

        foreach (var line in tableLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
            if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];

            // If line is separator like ---|---|---, skip adding as data row
            var stripped = trimmed.Replace("-", "").Replace("|", "").Replace(":", "").Replace(" ", "");
            if (stripped.Length == 0)
            {
                continue;
            }

            var cells = trimmed.Split('|').Select(c => c.Trim()).ToArray();
            colCount = Math.Max(colCount, cells.Length);
            parsedRows.Add(cells);
        }

        if (parsedRows.Count == 0 || colCount == 0) return new Border();

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
                AddFormattedInlines(tb, cellText);

                var cellBorder = new Border
                {
                    Background = isHeader
                        ? (headerBg ?? new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)))
                        : (r % 2 == 1 ? new SolidColorBrush(Color.FromArgb(15, 128, 128, 128)) : Brushes.Transparent),
                    BorderBrush = borderBrush ?? Brushes.Gray,
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
            BorderBrush = borderBrush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = grid
        };
    }

    private static Border CreateCodeBlock(string code, IBrush? background, IBrush? borderBrush)
    {
        var tb = new TextBlock
        {
            Text = code,
            FontFamily = MonospaceFamily,
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = tb
        };

        return new Border
        {
            Background = background ?? new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
            BorderBrush = borderBrush ?? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 4, 0, 6),
            Child = scroller
        };
    }

    private static TextBlock CreateParagraph(string line, IBrush? fg)
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
        AddFormattedInlines(tb, line);
        return tb;
    }

    private static void AddFormattedInlines(TextBlock tb, string text)
    {
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
                        Foreground = Brushes.SandyBrown
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
                        Foreground = Brushes.Gray
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
                            Foreground = Brushes.DeepSkyBlue,
                            TextDecorations = TextDecorations.Underline
                        });
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            // Plain text chunk until next token
            int nextToken = len;
            int nextBacktick = text.IndexOf('`', i);
            int nextTilde = (i + 1 < len && text.IndexOf("~~", i, StringComparison.Ordinal) >= 0)
                ? text.IndexOf("~~", i, StringComparison.Ordinal)
                : -1;
            int nextStar = text.IndexOf('*', i);
            int nextBracket = text.IndexOf('[', i);

            if (nextBacktick >= 0) nextToken = Math.Min(nextToken, nextBacktick);
            if (nextTilde >= 0) nextToken = Math.Min(nextToken, nextTilde);
            if (nextStar >= 0) nextToken = Math.Min(nextToken, nextStar);
            if (nextBracket >= 0) nextToken = Math.Min(nextToken, nextBracket);

            if (nextToken <= i) nextToken = i + 1;

            var chunk = text.Substring(i, nextToken - i);
            tb.Inlines?.Add(new Run(chunk));
            i = nextToken;
        }
    }
}
