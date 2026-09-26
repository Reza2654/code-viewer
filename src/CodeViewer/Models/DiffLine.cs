namespace CodeViewer.Models;

public enum DiffLineType
{
    Unchanged,
    Added,
    Removed
}

public class DiffLine
{
    public DiffLineType Type { get; }
    public int? OldLineNumber { get; }
    public int? NewLineNumber { get; }
    public string Text { get; }
    public string Prefix => Type switch
    {
        DiffLineType.Added => "+",
        DiffLineType.Removed => "-",
        _ => " "
    };

    public DiffLine(DiffLineType type, int? oldLine, int? newLine, string text)
    {
        Type = type;
        OldLineNumber = oldLine;
        NewLineNumber = newLine;
        Text = text;
    }
}
