using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Saves and restores open editor tabs to/from session.json in local application data.
/// </summary>
public class SessionService : ISessionService
{
    private readonly string _sessionFilePath;

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
    }

    public async Task<SessionData?> LoadSessionAsync()
    {
        try
        {
            if (!File.Exists(_sessionFilePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_sessionFilePath);
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
    }

    public async Task SaveSessionAsync(IEnumerable<string> openFilePaths, string? activeFilePath)
    {
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
            await File.WriteAllTextAsync(_sessionFilePath, json);
        }
        catch
        {
            // Best effort persistence
        }
    }

    public Task ClearSessionAsync()
    {
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
        return Task.CompletedTask;
    }
}
