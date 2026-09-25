using System;

namespace CodeViewer.Models;

/// <summary>
/// Represents an entry in the Ctrl+P Quick Open palette (either an open tab or a recent file).
/// </summary>
public class QuickOpenItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsOpenTab { get; set; }
    public bool IsActive { get; set; }
    public bool IsModified { get; set; }

    public string DisplayName
    {
        get => string.IsNullOrEmpty(Title) ? System.IO.Path.GetFileName(FilePath) : Title;
        set => Title = value;
    }

    public string RelativeOrFullPath
    {
        get => string.IsNullOrEmpty(Subtitle) ? FilePath : Subtitle;
        set => Subtitle = value;
    }

    public string BadgeText => IsOpenTab ? (IsModified ? "Open *" : "Open") : "Recent";
    public string TabBadge => BadgeText;
}
