using AvaloniaEdit.Highlighting;

namespace CodeViewer.Services;

/// <summary>
/// Service responsible for file extension to programming language resolution
/// and syntax highlighting configuration for AvaloniaEdit.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Detects programming language name from a file path or extension.
    /// </summary>
    string DetectLanguage(string? filePath);

    /// <summary>
    /// Resolves the AvaloniaEdit syntax highlighting definition for a language.
    /// </summary>
    IHighlightingDefinition? GetHighlightingDefinition(string language);
}
