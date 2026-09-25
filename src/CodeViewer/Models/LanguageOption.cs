namespace CodeViewer.Models;

/// <summary>
/// Represents a language item for selection in the Code Mode panel.
/// </summary>
public sealed class LanguageOption
{
    public string Name { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
}
