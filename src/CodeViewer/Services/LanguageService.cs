using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaEdit.Highlighting;

namespace CodeViewer.Services;

/// <summary>
/// Language detection and syntax highlighting provider for AvaloniaEdit.
/// Supports Dart, C#, C, C++, Java, Kotlin, JavaScript, TypeScript, Python,
/// HTML, CSS, JSON, XML, YAML, Markdown, SQL, PowerShell, Shell/Bash.
/// </summary>
public class LanguageService : ILanguageService
{
    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        // Dart
        { ".dart", "Dart" },

        // C#
        { ".cs", "C#" },
        { ".csx", "C#" },

        // C / C++
        { ".c", "C" },
        { ".h", "C/C++ Header" },
        { ".cpp", "C++" },
        { ".cxx", "C++" },
        { ".cc", "C++" },
        { ".hpp", "C++ Header" },
        { ".hxx", "C++ Header" },

        // Java / Kotlin
        { ".java", "Java" },
        { ".kt", "Kotlin" },
        { ".kts", "Kotlin" },

        // JavaScript / TypeScript
        { ".js", "JavaScript" },
        { ".jsx", "JavaScript" },
        { ".mjs", "JavaScript" },
        { ".cjs", "JavaScript" },
        { ".ts", "TypeScript" },
        { ".tsx", "TypeScript" },

        // Python
        { ".py", "Python" },
        { ".pyw", "Python" },
        { ".ipynb", "Jupyter Notebook" },

        // Web
        { ".html", "HTML" },
        { ".htm", "HTML" },
        { ".xhtml", "HTML" },
        { ".css", "CSS" },
        { ".scss", "SCSS" },
        { ".sass", "SASS" },
        { ".less", "LESS" },

        // Data / Markup
        { ".json", "JSON" },
        { ".jsonc", "JSON" },
        { ".xml", "XML" },
        { ".axaml", "XML" },
        { ".xaml", "XML" },
        { ".svg", "XML" },
        { ".config", "XML" },
        { ".csproj", "XML" },
        { ".sln", "Plain Text" },
        { ".slnx", "XML" },
        { ".yaml", "YAML" },
        { ".yml", "YAML" },
        { ".md", "Markdown" },
        { ".markdown", "Markdown" },

        // Database
        { ".sql", "SQL" },

        // Scripting / Shell
        { ".ps1", "PowerShell" },
        { ".psm1", "PowerShell" },
        { ".psd1", "PowerShell" },
        { ".sh", "Shell / Bash" },
        { ".bash", "Shell / Bash" },
        { ".zsh", "Shell / Bash" },

        // Others
        { ".rs", "Rust" },
        { ".go", "Go" },
        { ".php", "PHP" },
        { ".rb", "Ruby" },
        { ".lua", "Lua" },
        { ".swift", "Swift" },
        { ".txt", "Plain Text" },
        { ".log", "Plain Text" },
        { ".env", "Plain Text" }
    };

    public LanguageService()
    {
        RegisterCustomHighlighters();
    }

    private void RegisterCustomHighlighters()
    {
        try
        {
            var asm = typeof(LanguageService).Assembly;
            using var stream = asm.GetManifestResourceStream("CodeViewer.Assets.Syntax.Dart.xshd");
            if (stream != null)
            {
                using var reader = System.Xml.XmlReader.Create(stream);
                var dartDef = AvaloniaEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("Dart", new[] { ".dart" }, dartDef);
            }
        }
        catch
        {
            // Fallback handled in GetHighlightingDefinition
        }
    }

    private static readonly Dictionary<string, string> SpecialFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Dockerfile", "Docker" },
        { "Makefile", "Make" },
        { "CMakeLists.txt", "CMake" },
        { ".gitignore", "Plain Text" },
        { ".gitattributes", "Plain Text" },
        { ".editorconfig", "Plain Text" }
    };

    public string DetectLanguage(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Plain Text";
        }

        var fileName = Path.GetFileName(filePath);
        if (SpecialFileNames.TryGetValue(fileName, out var specialLang))
        {
            return specialLang;
        }

        var ext = Path.GetExtension(filePath);
        if (!string.IsNullOrEmpty(ext) && ExtensionToLanguage.TryGetValue(ext, out var lang))
        {
            return lang;
        }

        return "Plain Text";
    }

    public IHighlightingDefinition? GetHighlightingDefinition(string language)
    {
        var manager = HighlightingManager.Instance;

        // Try exact name first
        var def = manager.GetDefinition(language);
        if (def != null)
        {
            return def;
        }

        // Map languages to built-in AvaloniaEdit highlighting definitions
        string? targetName = language switch
        {
            "C#" => "C#",
            "C" or "C++" or "C/C++ Header" or "C++ Header" => "C++",
            "Java" => "Java",
            "Kotlin" => "Java", // High compatibility with Java syntax
            "JavaScript" => "JavaScript",
            "TypeScript" => "JavaScript", // High compatibility with JS syntax
            "HTML" => "HTML",
            "CSS" or "SCSS" or "SASS" or "LESS" => "CSS",
            "XML" => "XML",
            "PHP" => "PHP",
            "Python" => "Python",
            "JSON" => "Json",
            "Markdown" => "MarkDown",
            "PowerShell" => "PowerShell",
            "Dart" => "Java", // C-family syntax highlighting fallback
            "Rust" or "Go" or "Swift" => "C++",
            "Shell / Bash" => "PowerShell",
            "SQL" => "SQL",
            _ => null
        };

        if (targetName != null)
        {
            def = manager.GetDefinition(targetName);
            if (def != null)
            {
                return def;
            }
        }

        // Try extension-based lookup
        return null;
    }

    private static readonly string[] AllSupportedLanguages = new[]
    {
        "Plain Text",
        "C#",
        "C",
        "C++",
        "C/C++ Header",
        "CSS",
        "Dart",
        "Docker",
        "Go",
        "HTML",
        "Java",
        "JavaScript",
        "JSON",
        "Kotlin",
        "Lua",
        "Markdown",
        "PHP",
        "PowerShell",
        "Python",
        "Rust",
        "Shell / Bash",
        "SQL",
        "Swift",
        "TypeScript",
        "XML",
        "YAML"
    };

    public IReadOnlyList<string> GetSupportedLanguages() => AllSupportedLanguages;

    public string? GetCommentPrefix(string language)
    {
        return language switch
        {
            "Python" or "Shell / Bash" or "PowerShell" or "YAML" or "Docker" or "Ruby" or "R" or "Perl" => "# ",
            "SQL" or "Lua" => "-- ",
            "Plain Text" or "Markdown" or "JSON" or "HTML" or "XML" => null,
            _ => "// "
        };
    }
}

