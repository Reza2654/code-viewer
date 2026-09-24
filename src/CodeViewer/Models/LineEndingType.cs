namespace CodeViewer.Models;

/// <summary>
/// Text line ending format.
/// </summary>
public enum LineEndingType
{
    CRLF,   // Windows (\r\n)
    LF,     // Unix / Linux / macOS (\n)
    CR,     // Legacy Mac (\r)
    Mixed,  // Inconsistent line endings
    Unknown
}

public static class LineEndingExtensions
{
    public static string ToDisplayString(this LineEndingType lineEnding) => lineEnding switch
    {
        LineEndingType.CRLF => "CRLF",
        LineEndingType.LF => "LF",
        LineEndingType.CR => "CR",
        LineEndingType.Mixed => "Mixed",
        _ => "LF"
    };

    public static string GetString(this LineEndingType lineEnding) => lineEnding switch
    {
        LineEndingType.CRLF => "\r\n",
        LineEndingType.LF => "\n",
        LineEndingType.CR => "\r",
        _ => "\n"
    };
}
