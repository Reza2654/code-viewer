using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace CodeViewer.Models;

/// <summary>
/// Model representing a recently opened file item in the MRU list.
/// </summary>
public class RecentFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    public bool Exists => File.Exists(FilePath);

    [JsonIgnore]
    public ICommand? OpenCommand { get; set; }
}
