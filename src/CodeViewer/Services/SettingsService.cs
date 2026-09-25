using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service managing persistent user settings stored in JSON format.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSettings _currentSettings = new();

    public AppSettings CurrentSettings => _currentSettings;

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService(string? customSettingsFilePath = null)
    {
        if (!string.IsNullOrEmpty(customSettingsFilePath))
        {
            _settingsFilePath = customSettingsFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsFilePath = Path.Combine(appData, "CodeViewer", "settings.json");
        }

        LoadSynchronous();
    }

    private void LoadSynchronous()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                }
            }
        }
        catch
        {
            _currentSettings = new AppSettings();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                    SettingsChanged?.Invoke(_currentSettings);
                }
            }
        }
        catch
        {
            // Fallback to defaults
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            _currentSettings = settings.Clone();
            SettingsChanged?.Invoke(_currentSettings);

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_currentSettings, options);
                await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch
        {
            // Best effort save
        }
    }

    public AppSettings GetDefaultSettings()
    {
        return new AppSettings();
    }
}
