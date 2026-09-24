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
/// Persists recently opened files to %LocalAppData%/CodeViewer/recent_files.json.
/// Handles missing/deleted files gracefully and maintains a maximum MRU limit.
/// </summary>
public class RecentFilesService : IRecentFilesService
{
    private readonly string _storagePath;
    private readonly int _maxItems;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<RecentFileItem> _cachedItems = [];
    private bool _isLoaded;

    public RecentFilesService(int maxItems = 20)
    {
        _maxItems = maxItems;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "CodeViewer");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _storagePath = Path.Combine(dir, "recent_files.json");
    }

    public async Task<IReadOnlyList<RecentFileItem>> GetRecentFilesAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            // Return items (pruning those that no longer exist)
            return _cachedItems.Where(i => i.Exists).ToList().AsReadOnly();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddRecentFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            var fullPath = Path.GetFullPath(filePath);

            // Remove existing entry if present
            _cachedItems.RemoveAll(i => string.Equals(i.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

            // Insert at beginning (MRU)
            _cachedItems.Insert(0, new RecentFileItem
            {
                FilePath = fullPath,
                LastOpened = DateTime.UtcNow
            });

            // Enforce max count
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

    public async Task RemoveRecentFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isLoaded)
            {
                await LoadFromDiskAsync().ConfigureAwait(false);
                _isLoaded = true;
            }

            var fullPath = Path.GetFullPath(filePath);
            var removed = _cachedItems.RemoveAll(i => string.Equals(i.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await SaveToDiskAsync().ConfigureAwait(false);
            }
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
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task LoadFromDiskAsync()
    {
        if (!File.Exists(_storagePath))
        {
            _cachedItems = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<RecentFileItem>>(json);
            _cachedItems = list ?? [];
        }
        catch
        {
            _cachedItems = [];
        }
    }

    private async Task SaveToDiskAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cachedItems, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Best effort persistence
        }
    }
}
