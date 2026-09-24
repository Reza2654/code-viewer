using System;
using System.Threading.Tasks;

namespace CodeViewer.Plugins;

/// <summary>
/// Provides plugins with access to the active document and editor interaction capabilities.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Gets the full text of the currently active document.
    /// </summary>
    string CurrentText { get; }

    /// <summary>
    /// Gets the selected text in the active editor, or empty string if no text is selected.
    /// </summary>
    string SelectedText { get; }

    /// <summary>
    /// Gets the file path of the current document, if saved.
    /// </summary>
    string? FilePath { get; }

    /// <summary>
    /// Gets the detected programming or markup language of the document.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// True if any text is currently selected.
    /// </summary>
    bool HasSelection { get; }

    /// <summary>
    /// Replaces the currently selected text with new text.
    /// </summary>
    void ReplaceSelectedText(string newText);

    /// <summary>
    /// Replaces the entire document text with new text.
    /// </summary>
    void ReplaceAllText(string newText);

    /// <summary>
    /// Inserts text at the current caret position.
    /// </summary>
    void InsertText(string text);

    /// <summary>
    /// Displays an informational or alert message dialog to the user.
    /// </summary>
    Task ShowMessageAsync(string title, string message);
}
