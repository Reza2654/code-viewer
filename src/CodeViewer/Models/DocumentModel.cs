using System;
using System.IO;

namespace CodeViewer.Models;

/// <summary>
/// Domain model representing an open document file.
/// </summary>
public class DocumentModel
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string? FilePath { get; set; }
    public string Title { get; set; } = "Untitled";
    public string Text { get; set; } = string.Empty;
    public bool IsModified { get; set; }
    public FileEncodingInfo EncodingInfo { get; set; } = FileEncodingInfo.Utf8NoBom;
    public LineEndingType LineEnding { get; set; } = LineEndingType.LF;
    public string Language { get; set; } = "Plain Text";
    public long FileSizeBytes { get; set; }
    public DateTime? LastModifiedOnDisk { get; set; }

    public bool IsNewFile => string.IsNullOrEmpty(FilePath);

    public static DocumentModel CreateNew(string title = "Untitled") => new()
    {
        Title = title,
        Text = string.Empty,
        IsModified = false,
        EncodingInfo = FileEncodingInfo.Utf8NoBom,
        LineEnding = LineEndingType.LF,
        Language = "Plain Text"
    };
}
