using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service for fast, asynchronous file operations, encoding detection, and line ending handling.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Opens and reads a file asynchronously, detecting encoding and line endings.
    /// </summary>
    Task<DocumentModel> OpenFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a document to disk preserving its encoding and line endings.
    /// </summary>
    Task SaveFileAsync(DocumentModel document, string? targetPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects line ending format from text content without duplicate allocations.
    /// </summary>
    LineEndingType DetectLineEndings(string text);

    /// <summary>
    /// Formats file size in readable units (B, KB, MB, GB).
    /// </summary>
    string FormatFileSize(long bytes);
}
