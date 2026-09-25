using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace CodeViewer.Services;

/// <summary>
/// Monitors open files on disk for external changes using FileSystemWatcher with debouncing and internal-save suppression.
/// </summary>
public class FileWatcherService : IFileWatcherService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (FileSystemWatcher Watcher, HashSet<string> Files)> _directoryWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _ignoredUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastTriggered = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public event EventHandler<string>? FileChangedOnDisk;

    public void WatchFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _isDisposed)
            return;

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;

            lock (_lock)
            {
                if (!_directoryWatchers.TryGetValue(dir, out var entry))
                {
                    var watcher = new FileSystemWatcher(dir)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = false
                    };

                    watcher.Changed += OnFileSystemEvent;
                    watcher.Created += OnFileSystemEvent;
                    watcher.Deleted += OnFileSystemEvent;
                    watcher.Renamed += OnFileSystemRenamed;

                    entry = (watcher, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    _directoryWatchers[dir] = entry;

                    try
                    {
                        watcher.EnableRaisingEvents = true;
                    }
                    catch
                    {
                        // Fallback if unable to start watcher
                    }
                }

                entry.Files.Add(fullPath);
            }
        }
        catch
        {
            // Best effort watching
        }
    }

    public void UnwatchFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _isDisposed)
            return;

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
                return;

            lock (_lock)
            {
                if (_directoryWatchers.TryGetValue(dir, out var entry))
                {
                    entry.Files.Remove(fullPath);
                    if (entry.Files.Count == 0)
                    {
                        entry.Watcher.EnableRaisingEvents = false;
                        entry.Watcher.Dispose();
                        _directoryWatchers.Remove(dir);
                    }
                }
            }
        }
        catch
        {
            // Ignored
        }
    }

    public void UnwatchAll()
    {
        lock (_lock)
        {
            foreach (var entry in _directoryWatchers.Values)
            {
                try
                {
                    entry.Watcher.EnableRaisingEvents = false;
                    entry.Watcher.Dispose();
                }
                catch
                {
                    // Ignored
                }
            }
            _directoryWatchers.Clear();
            _ignoredUntil.Clear();
            _lastTriggered.Clear();
        }
    }

    public void TemporarilyIgnore(string filePath, int durationMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            _ignoredUntil[fullPath] = DateTime.UtcNow.AddMilliseconds(durationMs);
        }
        catch
        {
            // Ignored
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        CheckAndRaiseChange(e.FullPath);
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        CheckAndRaiseChange(e.FullPath);
    }

    private void CheckAndRaiseChange(string path)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(path))
            return;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return;
        }

        // Check if file is actively monitored
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir) || !_directoryWatchers.TryGetValue(dir, out var entry) || !entry.Files.Contains(fullPath))
            {
                return;
            }
        }

        // Check if suppressed due to internal save
        if (_ignoredUntil.TryGetValue(fullPath, out var ignoreExpiry) && DateTime.UtcNow < ignoreExpiry)
        {
            return;
        }

        // Debounce rapid events within 400ms
        var now = DateTime.UtcNow;
        if (_lastTriggered.TryGetValue(fullPath, out var lastTime) && (now - lastTime).TotalMilliseconds < 400)
        {
            return;
        }

        _lastTriggered[fullPath] = now;
        FileChangedOnDisk?.Invoke(this, fullPath);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnwatchAll();
    }
}
