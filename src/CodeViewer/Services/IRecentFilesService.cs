using System.Collections.Generic;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service managing Most Recently Used (MRU) files.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>
    /// Loads the recent files list from persistent storage.
    /// </summary>
    Task<IReadOnlyList<RecentFileItem>> GetRecentFilesAsync();

    /// <summary>
    /// Adds or moves a file path to the top of the recent files list.
    /// </summary>
    Task AddRecentFileAsync(string filePath);

    /// <summary>
    /// Removes a specific file from the recent files list.
    /// </summary>
    Task RemoveRecentFileAsync(string filePath);

    /// <summary>
    /// Clears the recent files list.
    /// </summary>
    Task ClearAsync();
}
