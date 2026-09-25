using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CodeViewer.Services;

/// <summary>
/// Robust CLI argument parser supporting Windows paths and trailing :line or :line:col coordinates.
/// Correctly distinguishes Windows drive letters (e.g. C:\...) from line number colons.
/// </summary>
public static class CommandLineParser
{
    // Matches trailing :line or :line:col, ensuring path before it is captured without splitting Windows drive letters
    private static readonly Regex LineColRegex = new(@"^(?<path>.*?):(?<line>\d+)(?::(?<col>\d+))?$", RegexOptions.Compiled);

    public record ParsedFileArgument(string FilePath, int Line = 1, int Column = 1);

    public static ParsedFileArgument ParseArgument(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return new ParsedFileArgument(string.Empty);
        }

        var trimmed = argument.Trim().Trim('"', '\'');

        // Check if raw argument exists as an exact file path on disk directly
        if (File.Exists(trimmed))
        {
            return new ParsedFileArgument(Path.GetFullPath(trimmed), 1, 1);
        }

        var match = LineColRegex.Match(trimmed);
        if (match.Success)
        {
            var rawPath = match.Groups["path"].Value;
            var lineStr = match.Groups["line"].Value;
            var colStr = match.Groups["col"].Value;

            if (int.TryParse(lineStr, out var line) && line >= 1)
            {
                var col = 1;
                if (!string.IsNullOrEmpty(colStr) && int.TryParse(colStr, out var c) && c >= 1)
                {
                    col = c;
                }

                // If path exists on disk or is non-empty
                if (File.Exists(rawPath))
                {
                    return new ParsedFileArgument(Path.GetFullPath(rawPath), line, col);
                }

                return new ParsedFileArgument(rawPath, line, col);
            }
        }

        return new ParsedFileArgument(trimmed, 1, 1);
    }

    public static IEnumerable<ParsedFileArgument> ParseArguments(IEnumerable<string>? arguments)
    {
        if (arguments == null) yield break;
        foreach (var arg in arguments)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                yield return ParseArgument(arg);
            }
        }
    }
}
