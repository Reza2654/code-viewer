using System;

namespace CodeViewer.Models;

public enum SymbolKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Record,
    Method,
    Function,
    Property,
    Field,
    Heading,
    Key
}

public class SymbolItem
{
    public string Name { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public int LineNumber { get; set; } = 1;
    public int ColumnNumber { get; set; } = 1;
    public int Level { get; set; }
    public string? Detail { get; set; }
    public string IconGlyph { get; set; } = "•";

    public string KindBadge => Kind switch
    {
        SymbolKind.Class => "C",
        SymbolKind.Interface => "I",
        SymbolKind.Struct => "S",
        SymbolKind.Enum => "E",
        SymbolKind.Record => "R",
        SymbolKind.Method => "M",
        SymbolKind.Function => "f",
        SymbolKind.Property => "P",
        SymbolKind.Field => "F",
        SymbolKind.Heading => Level switch
        {
            0 => "H1",
            1 => "H2",
            2 => "H3",
            3 => "H4",
            4 => "H5",
            _ => "H6"
        },
        SymbolKind.Key => "K",
        _ => "•"
    };

    public string KindColor => Kind switch
    {
        SymbolKind.Class or SymbolKind.Record => "#4EC9B0", // Teal/Cyan
        SymbolKind.Interface => "#B8D7A3",                 // Light Sage
        SymbolKind.Struct => "#86C1B9",                    // Slate Teal
        SymbolKind.Enum => "#B5CEA8",                      // Olive Green
        SymbolKind.Method => "#DCDCAA",                    // Yellow
        SymbolKind.Function => "#C586C0",                  // Purple / Magenta
        SymbolKind.Property => "#9CDCFE",                  // Light Blue
        SymbolKind.Field => "#4FC1FF",                     // Sky Blue
        SymbolKind.Heading => "#CE9178",                   // Coral / Orange
        SymbolKind.Key => "#569CD6",                       // Keyword Blue
        _ => "#D4D4D4"
    };

    public Avalonia.Thickness IndentMargin => new(Level * 14, 2, 4, 2);

    public override string ToString() => $"{Kind} {Name} (Line {LineNumber})";
}
