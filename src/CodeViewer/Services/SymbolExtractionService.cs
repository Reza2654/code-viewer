using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CodeViewer.Models;

namespace CodeViewer.Services;

public interface ISymbolExtractionService
{
    IReadOnlyList<SymbolItem> ExtractSymbols(string? text, string? languageOrExtension);
}

public class SymbolExtractionService : ISymbolExtractionService
{
    private static readonly Regex MarkdownHeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);

    // Python
    private static readonly Regex PythonClassRegex = new(@"^([ \t]*)class\s+([A-Za-z0-9_]+)(?:\s*\((.*?)\))?\s*:", RegexOptions.Compiled);
    private static readonly Regex PythonDefRegex = new(@"^([ \t]*)(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\((.*?)\)\s*(?:->\s*[^:]+)?\s*:", RegexOptions.Compiled);

    // C# / Java / Dart
    private static readonly Regex CSharpTypeRegex = new(@"^\s*(?:(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*(class|interface|struct|record|enum)\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    private static readonly Regex CSharpMethodRegex = new(@"^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract)\s+)+(?:(?:Task|ValueTask|void|string|int|bool|var|[A-Za-z0-9_<>\[\],?]+)\s+)+([A-Za-z0-9_]+)\s*(?:<[^>]+>)?\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex CSharpPropertyRegex = new(@"^\s*(?:(?:public|private|protected|internal|static|virtual|override)\s+)+([A-Za-z0-9_<>\[\],?]+)\s+([A-Za-z0-9_]+)\s*\{\s*(?:get|set|init)", RegexOptions.Compiled);

    // JavaScript / TypeScript
    private static readonly Regex JsClassRegex = new(@"^\s*(?:export\s+)?(?:default\s+)?class\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    private static readonly Regex JsFunctionRegex = new(@"^\s*(?:export\s+)?(?:default\s+)?(?:async\s+)?function(?:\s*\*|\s+)([A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);
    private static readonly Regex JsArrowRegex = new(@"^\s*(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z0-9_]+)\s*=>", RegexOptions.Compiled);
    private static readonly Regex JsMethodRegex = new(@"^\s*(?:(?:public|private|protected|static|async|get|set)\s+)*([A-Za-z0-9_]+)\s*\([^)]*\)\s*\{", RegexOptions.Compiled);

    // PowerShell / Bash
    private static readonly Regex PowerShellFunctionRegex = new(@"^\s*(?:function|filter)\s+([A-Za-z0-9_\-:]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BashFunctionRegex = new(@"^\s*(?:function\s+)?([A-Za-z0-9_]+)\s*\(\)\s*\{", RegexOptions.Compiled);

    // YAML
    private static readonly Regex YamlKeyRegex = new(@"^([ \t]*)([A-Za-z0-9_\-.]+)\s*:\s*(.*)$", RegexOptions.Compiled);

    private static readonly HashSet<string> ControlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "while", "for", "foreach", "switch", "catch", "using", "lock", "return", "new", "throw", "case", "yield"
    };

    public IReadOnlyList<SymbolItem> ExtractSymbols(string? text, string? languageOrExtension)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<SymbolItem>();

        var normalizedLang = NormalizeLanguage(languageOrExtension);
        var lines = SplitLinesFast(text);
        var results = new List<SymbolItem>();

        switch (normalizedLang)
        {
            case "markdown":
                ExtractMarkdown(lines, results);
                break;
            case "python":
                ExtractPython(lines, results);
                break;
            case "csharp" or "java" or "dart":
                ExtractCSharp(lines, results);
                break;
            case "javascript" or "typescript":
                ExtractJavaScript(lines, results);
                break;
            case "powershell":
                ExtractPowerShell(lines, results);
                break;
            case "bash":
                ExtractBash(lines, results);
                break;
            case "yaml":
                ExtractYaml(lines, results);
                break;
            case "json":
                ExtractJson(lines, results);
                break;
            default:
                // Fallback: try markdown if headers present, otherwise C-style/generic
                if (text.StartsWith('#'))
                {
                    ExtractMarkdown(lines, results);
                }
                else
                {
                    ExtractCSharp(lines, results);
                }
                break;
        }

        return results;
    }

    private static string NormalizeLanguage(string? langOrExt)
    {
        if (string.IsNullOrWhiteSpace(langOrExt)) return "text";
        var cleaned = langOrExt.Trim().ToLowerInvariant();
        if (cleaned.StartsWith('.')) cleaned = cleaned[1..];

        return cleaned switch
        {
            "md" or "markdown" or "mdown" or "mkd" => "markdown",
            "py" or "python" or "pyw" => "python",
            "cs" or "csharp" or "c#" => "csharp",
            "java" => "java",
            "dart" => "dart",
            "js" or "javascript" or "jsx" or "mjs" => "javascript",
            "ts" or "typescript" or "tsx" => "typescript",
            "ps1" or "ps2" or "psm1" or "psd1" or "powershell" or "pwsh" => "powershell",
            "sh" or "bash" or "zsh" => "bash",
            "yaml" or "yml" => "yaml",
            "json" or "jsonc" => "json",
            _ => cleaned
        };
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

    private static void ExtractMarkdown(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length < 3 || line[0] != '#') continue;
            var match = MarkdownHeadingRegex.Match(line);
            if (match.Success)
            {
                var prefix = match.Groups[1].Value;
                var title = match.Groups[2].Value.Trim();
                int level = prefix.Length - 1; // H1 -> 0, H2 -> 1, ...
                int col = line.IndexOf(title, StringComparison.Ordinal) + 1;
                if (col <= 0) col = prefix.Length + 2;

                results.Add(new SymbolItem
                {
                    Name = title,
                    Kind = SymbolKind.Heading,
                    LineNumber = i + 1,
                    ColumnNumber = col,
                    Level = level,
                    IconGlyph = "📑",
                    Detail = $"Heading {prefix.Length}"
                });
            }
        }
    }

    private static void ExtractPython(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = line.AsSpan().TrimStart();
            if (span.IsEmpty || span[0] == '#') continue;

            if (span.StartsWith("class ", StringComparison.Ordinal))
            {
                var classMatch = PythonClassRegex.Match(line);
                if (classMatch.Success)
                {
                    var className = classMatch.Groups[2].Value;
                    var baseClass = classMatch.Groups[3].Value;
                    int level = CalculateIndentLevelSpan(line.AsSpan(0, classMatch.Groups[1].Length));
                    int col = line.IndexOf(className, StringComparison.Ordinal) + 1;

                    results.Add(new SymbolItem
                    {
                        Name = className,
                        Kind = SymbolKind.Class,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = level,
                        IconGlyph = "🅲",
                        Detail = string.IsNullOrEmpty(baseClass) ? null : $"({baseClass})"
                    });
                    continue;
                }
            }

            if (span.StartsWith("def ", StringComparison.Ordinal) || span.StartsWith("async ", StringComparison.Ordinal))
            {
                var defMatch = PythonDefRegex.Match(line);
                if (defMatch.Success)
                {
                    var funcName = defMatch.Groups[2].Value;
                    var args = defMatch.Groups[3].Value;
                    int level = CalculateIndentLevelSpan(line.AsSpan(0, defMatch.Groups[1].Length));
                    int col = line.IndexOf(funcName, StringComparison.Ordinal) + 1;
                    bool isMethod = level > 0;

                    results.Add(new SymbolItem
                    {
                        Name = funcName,
                        Kind = isMethod ? SymbolKind.Method : SymbolKind.Function,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = level,
                        IconGlyph = isMethod ? "ⓜ" : "ƒ",
                        Detail = $"({args})"
                    });
                }
            }
        }
    }

    private static void ExtractCSharp(List<string> lines, List<SymbolItem> results)
    {
        int currentTypeLevel = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = line.AsSpan().TrimStart();
            if (span.IsEmpty || span.StartsWith("//") || span.StartsWith("/*") || span[0] == '*')
                continue;

            // Type declaration (class, interface, struct, record, enum)
            if (span.Contains("class", StringComparison.Ordinal) ||
                span.Contains("interface", StringComparison.Ordinal) ||
                span.Contains("struct", StringComparison.Ordinal) ||
                span.Contains("record", StringComparison.Ordinal) ||
                span.Contains("enum", StringComparison.Ordinal))
            {
                var typeMatch = CSharpTypeRegex.Match(line);
                if (typeMatch.Success)
                {
                    var kindKeyword = typeMatch.Groups[1].Value;
                    var typeName = typeMatch.Groups[2].Value;
                    var kind = kindKeyword switch
                    {
                        "class" => SymbolKind.Class,
                        "interface" => SymbolKind.Interface,
                        "struct" => SymbolKind.Struct,
                        "record" => SymbolKind.Record,
                        "enum" => SymbolKind.Enum,
                        _ => SymbolKind.Class
                    };
                    int col = line.IndexOf(typeName, StringComparison.Ordinal) + 1;

                    results.Add(new SymbolItem
                    {
                        Name = typeName,
                        Kind = kind,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = 0,
                        IconGlyph = kind == SymbolKind.Interface ? "Ⓘ" : (kind == SymbolKind.Enum ? "Ⓔ" : "🅲"),
                        Detail = kindKeyword
                    });
                    currentTypeLevel = 1;
                    continue;
                }
            }

            // Method
            if (span.Contains('('))
            {
                var methodMatch = CSharpMethodRegex.Match(line);
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[1].Value;
                    if (!ControlKeywords.Contains(methodName))
                    {
                        int col = line.IndexOf(methodName, StringComparison.Ordinal) + 1;
                        var args = methodMatch.Groups[2].Value;

                        results.Add(new SymbolItem
                        {
                            Name = methodName,
                            Kind = SymbolKind.Method,
                            LineNumber = i + 1,
                            ColumnNumber = col,
                            Level = currentTypeLevel,
                            IconGlyph = "ⓜ",
                            Detail = $"({args})"
                        });
                        continue;
                    }
                }
            }

            // Property
            if (span.Contains('{'))
            {
                var propMatch = CSharpPropertyRegex.Match(line);
                if (propMatch.Success)
                {
                    var propType = propMatch.Groups[1].Value;
                    var propName = propMatch.Groups[2].Value;
                    if (!ControlKeywords.Contains(propName))
                    {
                        int col = line.IndexOf(propName, StringComparison.Ordinal) + 1;

                        results.Add(new SymbolItem
                        {
                            Name = propName,
                            Kind = SymbolKind.Property,
                            LineNumber = i + 1,
                            ColumnNumber = col,
                            Level = currentTypeLevel,
                            IconGlyph = "ⓟ",
                            Detail = propType
                        });
                    }
                }
            }
        }
    }

    private static void ExtractJavaScript(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = line.AsSpan().TrimStart();
            if (span.IsEmpty || span.StartsWith("//") || span.StartsWith("/*") || span[0] == '*')
                continue;

            // Class
            if (span.Contains("class", StringComparison.Ordinal))
            {
                var classMatch = JsClassRegex.Match(line);
                if (classMatch.Success)
                {
                    var className = classMatch.Groups[1].Value;
                    int col = line.IndexOf(className, StringComparison.Ordinal) + 1;

                    results.Add(new SymbolItem
                    {
                        Name = className,
                        Kind = SymbolKind.Class,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = 0,
                        IconGlyph = "🅲",
                        Detail = "class"
                    });
                    continue;
                }
            }

            // Standard Function
            if (span.Contains("function", StringComparison.Ordinal))
            {
                var funcMatch = JsFunctionRegex.Match(line);
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[1].Value;
                    int col = line.IndexOf(funcName, StringComparison.Ordinal) + 1;

                    results.Add(new SymbolItem
                    {
                        Name = funcName,
                        Kind = SymbolKind.Function,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = 0,
                        IconGlyph = "ƒ",
                        Detail = "function"
                    });
                    continue;
                }
            }

            // Arrow function
            if (span.Contains("=>", StringComparison.Ordinal))
            {
                var arrowMatch = JsArrowRegex.Match(line);
                if (arrowMatch.Success)
                {
                    var arrowName = arrowMatch.Groups[1].Value;
                    int col = line.IndexOf(arrowName, StringComparison.Ordinal) + 1;

                    results.Add(new SymbolItem
                    {
                        Name = arrowName,
                        Kind = SymbolKind.Function,
                        LineNumber = i + 1,
                        ColumnNumber = col,
                        Level = 0,
                        IconGlyph = "ƒ",
                        Detail = "arrow function"
                    });
                    continue;
                }
            }

            // Class method
            if (span.Contains('(') && span.Contains('{'))
            {
                var methodMatch = JsMethodRegex.Match(line);
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[1].Value;
                    if (!ControlKeywords.Contains(methodName))
                    {
                        int col = line.IndexOf(methodName, StringComparison.Ordinal) + 1;

                        results.Add(new SymbolItem
                        {
                            Name = methodName,
                            Kind = SymbolKind.Method,
                            LineNumber = i + 1,
                            ColumnNumber = col,
                            Level = 1,
                            IconGlyph = "ⓜ",
                            Detail = "method"
                        });
                    }
                }
            }
        }
    }

    private static void ExtractPowerShell(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("function", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("filter", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var match = PowerShellFunctionRegex.Match(line);
            if (match.Success)
            {
                var funcName = match.Groups[1].Value;
                int col = line.IndexOf(funcName, StringComparison.Ordinal) + 1;

                results.Add(new SymbolItem
                {
                    Name = funcName,
                    Kind = SymbolKind.Function,
                    LineNumber = i + 1,
                    ColumnNumber = col,
                    Level = 0,
                    IconGlyph = "ƒ",
                    Detail = "function"
                });
            }
        }
    }

    private static void ExtractBash(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains('(') && !line.Contains("function", StringComparison.Ordinal))
            {
                continue;
            }
            var match = BashFunctionRegex.Match(line);
            if (match.Success)
            {
                var funcName = match.Groups[1].Value;
                int col = line.IndexOf(funcName, StringComparison.Ordinal) + 1;

                results.Add(new SymbolItem
                {
                    Name = funcName,
                    Kind = SymbolKind.Function,
                    LineNumber = i + 1,
                    ColumnNumber = col,
                    Level = 0,
                    IconGlyph = "ƒ",
                    Detail = "function"
                });
            }
        }
    }

    private static void ExtractYaml(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = line.AsSpan().TrimStart();
            if (span.IsEmpty || span[0] == '#' || !span.Contains(':')) continue;

            var match = YamlKeyRegex.Match(line);
            if (match.Success)
            {
                var key = match.Groups[2].Value;
                var value = match.Groups[3].Value.Trim();
                int level = CalculateIndentLevelSpan(line.AsSpan(0, match.Groups[1].Length));
                int col = line.IndexOf(key, StringComparison.Ordinal) + 1;

                results.Add(new SymbolItem
                {
                    Name = key,
                    Kind = SymbolKind.Key,
                    LineNumber = i + 1,
                    ColumnNumber = col,
                    Level = level,
                    IconGlyph = "🔑",
                    Detail = string.IsNullOrEmpty(value) ? null : value
                });
            }
        }
    }

    private static void ExtractJson(List<string> lines, List<SymbolItem> results)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = line.AsSpan();
            int indentEnd = 0;
            while (indentEnd < span.Length && (span[indentEnd] == ' ' || span[indentEnd] == '\t'))
            {
                indentEnd++;
            }

            if (indentEnd >= span.Length || span[indentEnd] != '"')
                continue;

            int keyStart = indentEnd + 1;
            int closeQuoteRel = span[keyStart..].IndexOf('"');
            if (closeQuoteRel <= 0)
                continue;

            var keySpan = span.Slice(keyStart, closeQuoteRel);
            if (!IsValidJsonKeyToken(keySpan))
                continue;

            int afterQuote = keyStart + closeQuoteRel + 1;
            while (afterQuote < span.Length && char.IsWhiteSpace(span[afterQuote]))
            {
                afterQuote++;
            }

            if (afterQuote >= span.Length || span[afterQuote] != ':')
                continue;

            var restSpan = span[(afterQuote + 1)..].Trim().TrimEnd(',');
            int level = CalculateIndentLevelSpan(span[..indentEnd]);
            string? detail = null;
            if (!restSpan.IsEmpty && restSpan[0] != '{' && restSpan[0] != '[')
            {
                detail = restSpan.ToString();
            }

            results.Add(new SymbolItem
            {
                Name = keySpan.ToString(),
                Kind = SymbolKind.Key,
                LineNumber = i + 1,
                ColumnNumber = keyStart + 1,
                Level = level,
                IconGlyph = "🔑",
                Detail = detail
            });
        }
    }

    private static bool IsValidJsonKeyToken(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
            {
                return false;
            }
        }
        return true;
    }

    private static int CalculateIndentLevelSpan(ReadOnlySpan<char> indent)
    {
        if (indent.IsEmpty) return 0;
        int spaces = 0;
        foreach (char c in indent)
        {
            if (c == '\t') spaces += 4;
            else if (c == ' ') spaces += 1;
        }
        return Math.Min(spaces / 4, 6);
    }
}
