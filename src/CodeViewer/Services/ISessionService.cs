using System.Collections.Generic;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service responsible for loading and saving open tab sessions.
/// </summary>
public interface ISessionService
{
    Task<SessionData?> LoadSessionAsync();
    Task SaveSessionAsync(IEnumerable<string> openFilePaths, string? activeFilePath);
    Task ClearSessionAsync();
}
