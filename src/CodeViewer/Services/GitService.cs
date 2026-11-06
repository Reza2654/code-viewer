using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Executes non-blocking Git commands to discover repositories and parse porcelain statuses.
/// </summary>
public class GitService : IGitService
{
    public async Task<bool> IsGitInstalledAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> FindGitRootAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return null;

        // Fast filesystem pre-check: avoid spawning a process if no .git marker exists in the folder hierarchy
        if (!HasGitMarkerInHierarchy(directoryPath))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                WorkingDirectory = directoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                var output = (await outputTask).Trim();
                if (!string.IsNullOrEmpty(output))
                {
                    return Path.GetFullPath(output);
                }
            }
        }
        catch
        {
            // Graceful fallback if git CLI is unavailable or directory is inaccessible
        }

        return null;
    }

    public async Task<Dictionary<string, GitFileStatus>> GetStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return result;

        var repoRoot = await FindGitRootAsync(workingDirectory, cancellationToken);
        if (string.IsNullOrEmpty(repoRoot))
            return result;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain=v1 -uall",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            using var reader = process.StandardOutput;
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                var parsed = ParsePorcelainLine(line);
                if (parsed.HasValue)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(repoRoot, parsed.Value.RelativePath));
                    result[fullPath] = parsed.Value.Status;
                }
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            // Graceful fallback on command cancellation or missing git tool
        }

        return result;
    }

    /// <summary>
    /// Parses a single line from 'git status --porcelain=v1' into a normalized relative path and GitFileStatus.
    /// </summary>
    public static (string RelativePath, GitFileStatus Status)? ParsePorcelainLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < 4 || line.StartsWith("##"))
            return null;

        char x = line[0];
        char y = line[1];

        var pathPart = line.Substring(3).Trim();
        if (string.IsNullOrEmpty(pathPart))
            return null;

        // If path was quoted due to spaces or special characters, unquote it
        if (pathPart.StartsWith('"') && pathPart.EndsWith('"') && pathPart.Length >= 2)
        {
            pathPart = pathPart.Substring(1, pathPart.Length - 2);
        }

        // Handle rename entries like 'R  old.txt -> new.txt'
        var arrowIndex = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrowIndex >= 0)
        {
            pathPart = pathPart.Substring(arrowIndex + 4).Trim().Trim('"');
        }

        // Map status codes
        GitFileStatus status;
        if (x == '?' && y == '?')
        {
            status = GitFileStatus.Untracked;
        }
        else if (x == '!' && y == '!')
        {
            status = GitFileStatus.Ignored;
        }
        else if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
        {
            status = GitFileStatus.Conflicted;
        }
        else if (x == 'A' || y == 'A')
        {
            status = GitFileStatus.Added;
        }
        else if (x == 'D' || y == 'D')
        {
            status = GitFileStatus.Deleted;
        }
        else if (x == 'R' || y == 'R')
        {
            status = GitFileStatus.Renamed;
        }
        else if (x == 'M' || y == 'M')
        {
            status = GitFileStatus.Modified;
        }
        else
        {
            status = GitFileStatus.Modified;
        }

        // Normalize separators to OS standard
        var normalizedPath = pathPart.Replace('/', Path.DirectorySeparatorChar);
        return (normalizedPath, status);
    }

    private static bool HasGitMarkerInHierarchy(string startPath)
    {
        try
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    return true;
                }
                dir = dir.Parent;
            }
        }
        catch
        {
            // Directory access denied
        }

        return false;
    }
}
