using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace CodeViewer.Models;

/// <summary>
/// Model representing a recently opened workspace / folder item in the MRU list.
/// </summary>
public class RecentWorkspaceItem
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName => string.IsNullOrEmpty(FolderPath) ? "Workspace" : Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    public bool Exists => Directory.Exists(FolderPath);

    [JsonIgnore]
    public ICommand? OpenCommand { get; set; }
}
