using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeViewer.Models;

/// <summary>
/// Data contract for persisting open files and active editor state across application sessions.
/// </summary>
public class SessionData
{
    [JsonPropertyName("openFiles")]
    public List<string> OpenFiles { get; set; } = new();

    [JsonPropertyName("activeFile")]
    public string? ActiveFile { get; set; }
}
