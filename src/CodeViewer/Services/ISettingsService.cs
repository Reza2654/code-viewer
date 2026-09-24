using System;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service managing persistent application and editor settings.
/// </summary>
public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    event Action<AppSettings>? SettingsChanged;
    Task LoadAsync();
    Task SaveAsync(AppSettings settings);
    AppSettings GetDefaultSettings();
}
