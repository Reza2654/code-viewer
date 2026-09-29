using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public class WorkspaceSearchMatch
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public int MatchLength { get; set; }

    public string DisplayText => $"{FileName} : {LineNumber} - {LineContent.Trim()}";
}

public class WorkspaceSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int TotalFilesSearched { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public IReadOnlyList<WorkspaceSearchMatch> Matches { get; set; } = Array.Empty<WorkspaceSearchMatch>();
}

public interface IWorkspaceSearchService
{
    Task<WorkspaceSearchResult> SearchAsync(
        string rootPath,
        string query,
        bool matchCase = false,
        bool wholeWord = false,
        string? fileFilter = null,
        int maxMatches = 500,
        CancellationToken ct = default);
}

public class WorkspaceSearchService : IWorkspaceSearchService
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode", "bin", "obj", "node_modules", "dist", "build", "packages", ".gradle"
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".so", ".dylib", ".bin", ".obj", ".o", ".a", ".lib",
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".webp", ".bmp", ".tiff",
        ".zip", ".tar", ".gz", ".7z", ".rar", ".iso", ".nupkg",
        ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv",
        ".pdf", ".docx", ".xlsx", ".pptx", ".ttf", ".woff", ".woff2", ".eot"
    };

    private const long MaxFileSize = 2 * 1024 * 1024; // 2 MB limit per file for fast responsiveness

    public async Task<WorkspaceSearchResult> SearchAsync(
        string rootPath,
        string query,
        bool matchCase = false,
        bool wholeWord = false,
        string? fileFilter = null,
        int maxMatches = 500,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrEmpty(query) || !Directory.Exists(rootPath))
        {
            return new WorkspaceSearchResult { Query = query };
        }

        var sw = Stopwatch.StartNew();

        return await Task.Run(() =>
        {
            var filesToSearch = new List<string>();
            try
            {
                CollectFiles(new DirectoryInfo(rootPath), filesToSearch, fileFilter, ct);
            }
            catch
            {
                // Fallback or ignore directory enumeration errors
            }

            if (filesToSearch.Count == 0 || ct.IsCancellationRequested)
            {
                sw.Stop();
                return new WorkspaceSearchResult
                {
                    Query = query,
                    TotalFilesSearched = 0,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            var matches = new ConcurrentBag<WorkspaceSearchMatch>();
            int filesSearchedCount = 0;
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // Regex pattern for whole-word search if requested
            Regex? wholeWordRegex = null;
            if (wholeWord)
            {
                var escaped = Regex.Escape(query);
                var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                wholeWordRegex = new Regex($@"\b{escaped}\b", options | RegexOptions.Compiled);
            }

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.ForEach(filesToSearch, parallelOptions, (filePath, state) =>
                {
                    if (matches.Count >= maxMatches || state.ShouldExitCurrentIteration)
                    {
                        state.Break();
                        return;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > MaxFileSize) return;

                        // Quick binary check on first 512 bytes
                        if (IsLikelyBinary(filePath)) return;

                        var relativePath = Path.GetRelativePath(rootPath, filePath);
                        var fileName = Path.GetFileName(filePath);

                        using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, true);
                        string? line;
                        int lineNumber = 1;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (matches.Count >= maxMatches)
                            {
                                state.Break();
                                return;
                            }

                            if (wholeWord && wholeWordRegex != null)
                            {
                                var matchCollection = wholeWordRegex.Matches(line);
                                foreach (Match m in matchCollection)
                                {
                                    matches.Add(new WorkspaceSearchMatch
                                    {
                                        FilePath = filePath,
                                        RelativePath = relativePath,
                                        FileName = fileName,
                                        LineNumber = lineNumber,
                                        ColumnNumber = m.Index + 1,
                                        LineContent = line,
                                        MatchLength = m.Length
                                    });

                                    if (matches.Count >= maxMatches) break;
                                }
                            }
                            else
                            {
                                int colIndex = 0;
                                while ((colIndex = line.IndexOf(query, colIndex, comparison)) >= 0)
                                {
                                    matches.Add(new WorkspaceSearchMatch
                                    {
                                        FilePath = filePath,
                                        RelativePath = relativePath,
                                        FileName = fileName,
                                        LineNumber = lineNumber,
                                        ColumnNumber = colIndex + 1,
                                        LineContent = line,
                                        MatchLength = query.Length
                                    });

                                    colIndex += query.Length;
                                    if (matches.Count >= maxMatches) break;
                                }
                            }

                            lineNumber++;
                        }

                        Interlocked.Increment(ref filesSearchedCount);
                    }
                    catch
                    {
                        // Ignore unreadable or locked files
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Graceful early exit
            }

            sw.Stop();

            // Order matches deterministically: by relative path, then by line number
            var orderedMatches = matches
                .OrderBy(m => m.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.LineNumber)
                .Take(maxMatches)
                .ToList();

            return new WorkspaceSearchResult
            {
                Query = query,
                TotalFilesSearched = filesSearchedCount,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                Matches = orderedMatches
            };
        }, ct);
    }

    private void CollectFiles(DirectoryInfo dir, List<string> files, string? fileFilter, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        try
        {
            foreach (var subDir in dir.EnumerateDirectories())
            {
                if (IgnoredDirectories.Contains(subDir.Name) || subDir.Name.StartsWith('.'))
                {
                    continue;
                }
                CollectFiles(subDir, files, fileFilter, ct);
            }
        }
        catch
        {
            // Ignore unauthorized access
        }

        try
        {
            var fileList = !string.IsNullOrWhiteSpace(fileFilter)
                ? dir.EnumerateFiles(fileFilter)
                : dir.EnumerateFiles();

            foreach (var file in fileList)
            {
                if (ct.IsCancellationRequested) return;
                var ext = file.Extension;
                if (!IgnoredExtensions.Contains(ext))
                {
                    files.Add(file.FullName);
                }
            }
        }
        catch
        {
            // Ignore unauthorized access
        }
    }

    private static bool IsLikelyBinary(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[Math.Min(512, (int)stream.Length)];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0) // Null byte usually indicates binary
                {
                    return true;
                }
            }
        }
        catch
        {
            return true;
        }

        return false;
    }
}
