using System.Collections.Generic;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service managing Most Recently Used (MRU) workspaces and project folders.
/// </summary>
public interface IRecentWorkspacesService
{
    /// <summary>
    /// Loads the recent workspaces list from persistent storage.
    /// </summary>
    Task<IReadOnlyList<RecentWorkspaceItem>> GetRecentWorkspacesAsync();

    /// <summary>
    /// Adds or moves a folder path to the top of the recent workspaces list.
    /// </summary>
    Task AddRecentWorkspaceAsync(string folderPath);

    /// <summary>
    /// Removes a specific folder from the recent workspaces list.
    /// </summary>
    Task RemoveRecentWorkspaceAsync(string folderPath);

    /// <summary>
    /// Clears the recent workspaces list.
    /// </summary>
    Task ClearAsync();
}
