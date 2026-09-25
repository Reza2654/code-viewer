using System;

namespace CodeViewer.Services;

/// <summary>
/// Service that monitors open files on disk for external modifications.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Raised when a watched file is modified or changed externally on disk.
    /// </summary>
    event EventHandler<string>? FileChangedOnDisk;

    /// <summary>
    /// Starts watching a file for external disk changes.
    /// </summary>
    void WatchFile(string filePath);

    /// <summary>
    /// Stops watching a specific file.
    /// </summary>
    void UnwatchFile(string filePath);

    /// <summary>
    /// Stops watching all files.
    /// </summary>
    void UnwatchAll();

    /// <summary>
    /// Temporarily suppresses change events for a file (e.g. during an internal application save).
    /// </summary>
    void TemporarilyIgnore(string filePath, int durationMs = 1500);
}
