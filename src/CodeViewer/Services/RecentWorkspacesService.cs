using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Persists recently opened workspace folders to %LocalAppData%/CodeViewer/recent_workspaces.json.
/// Handles missing/deleted folders gracefully and maintains a maximum MRU limit.
/// </summary>
public class RecentWorkspacesService : IRecentWorkspacesService
{
    private readonly string _storagePath;
    private readonly int _maxItems;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<RecentWorkspaceItem> _cachedItems = [];
    private bool _isLoaded;

    public RecentWorkspacesService(int maxItems = 15)
        : this(GetDefaultStoragePath(), maxItems)
    {
    }

    public RecentWorkspacesService(string storagePath, int maxItems = 15)
    {
        _maxItems = maxItems;
        _storagePath = storagePath;
        var dir = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static string GetDefaultStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "CodeViewer");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "recent_workspaces.json");
    }

    public async Task<IReadOnlyList<RecentWorkspaceItem>> GetRecentWorkspacesAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            return _cachedItems.Where(i => i.Exists).ToList().AsReadOnly();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddRecentWorkspaceAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            _cachedItems.RemoveAll(i => string.Equals(Path.GetFullPath(i.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedPath, StringComparison.OrdinalIgnoreCase));

            _cachedItems.Insert(0, new RecentWorkspaceItem
            {
                FolderPath = normalizedPath,
                LastOpened = DateTime.UtcNow
            });

            if (_cachedItems.Count > _maxItems)
            {
                _cachedItems = _cachedItems.Take(_maxItems).ToList();
            }

            await SaveToDiskAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveRecentWorkspaceAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        var normalizedPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            _cachedItems.RemoveAll(i => string.Equals(Path.GetFullPath(i.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedPath, StringComparison.OrdinalIgnoreCase));
            await SaveToDiskAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _cachedItems.Clear();
            _isLoaded = true;
            await SaveToDiskAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task LoadFromDiskAsync()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
                var items = JsonSerializer.Deserialize<List<RecentWorkspaceItem>>(json);
                if (items != null)
                {
                    _cachedItems = items.Where(i => !string.IsNullOrWhiteSpace(i.FolderPath)).ToList();
                    return;
                }
            }
        }
        catch
        {
            // Fallback gracefully on disk read error
        }

        _cachedItems = [];
    }

    private async Task SaveToDiskAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_cachedItems, options);
            await File.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Fallback gracefully on disk write error
        }
    }
}
