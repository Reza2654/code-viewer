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
/// Saves and restores open editor tabs to/from session.json in local application data with thread-safe synchronization.
/// </summary>
public class SessionService : ISessionService
{
    private readonly string _sessionFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionService(string? customSessionFilePath = null)
    {
        if (!string.IsNullOrEmpty(customSessionFilePath))
        {
            _sessionFilePath = customSessionFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _sessionFilePath = Path.Combine(appData, "CodeViewer", "session.json");
        }

        CleanupOrphanedTempFiles();
    }

    private void CleanupOrphanedTempFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(_sessionFilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            var tempFiles = Directory.GetFiles(dir, "*tmp*")
                .Concat(Directory.GetFiles(dir, "*~*"));

            foreach (var file in tempFiles)
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (DateTime.UtcNow - lastWrite > TimeSpan.FromMinutes(1))
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    public async Task<SessionData?> LoadSessionAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_sessionFilePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_sessionFilePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var session = JsonSerializer.Deserialize<SessionData>(json);
            if (session == null)
            {
                return null;
            }

            // Filter out non-existent files
            session.OpenFiles = session.OpenFiles
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(session.ActiveFile) && !File.Exists(session.ActiveFile))
            {
                session.ActiveFile = session.OpenFiles.FirstOrDefault();
            }

            return session;
        }
        catch
        {
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSessionAsync(IEnumerable<string> openFilePaths, string? activeFilePath)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        string? tempFile = null;
        try
        {
            var dir = Path.GetDirectoryName(_sessionFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var validFiles = openFilePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var session = new SessionData
            {
                OpenFiles = validFiles,
                ActiveFile = !string.IsNullOrEmpty(activeFilePath) && File.Exists(activeFilePath) ? activeFilePath : validFiles.LastOrDefault()
            };

            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            tempFile = _sessionFilePath + $".tmp_{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tempFile, json).ConfigureAwait(false);

            if (File.Exists(_sessionFilePath))
            {
                try
                {
                    File.Replace(tempFile, _sessionFilePath, null);
                }
                catch
                {
                    File.Copy(tempFile, _sessionFilePath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempFile, _sessionFilePath);
            }
        }
        catch
        {
            // Best effort persistence
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            _lock.Release();
        }
    }

    public async Task ClearSessionAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
            }
        }
        catch
        {
            // Ignored
        }
        finally
        {
            _lock.Release();
        }
    }
}
