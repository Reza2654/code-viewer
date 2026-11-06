using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Provides Git repository discovery and porcelain status querying.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Finds the Git repository root directory for the given folder, or returns null if not in a repository.
    /// </summary>
    Task<string?> FindGitRootAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a dictionary mapping full file paths to their current Git status.
    /// </summary>
    Task<Dictionary<string, GitFileStatus>> GetStatusAsync(string workingDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the Git CLI executable is installed and accessible on PATH.
    /// </summary>
    Task<bool> IsGitInstalledAsync();
}
