using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeViewer.Plugins;
using CodeViewer.ViewModels;

namespace CodeViewer.Services;

/// <summary>
/// Service managing discovery, loading, importing, and execution of Code Viewer plugins.
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// All currently loaded plugins (built-in and dynamically imported).
    /// </summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>
    /// Event fired when new plugins are loaded or imported.
    /// </summary>
    event Action? PluginsChanged;

    /// <summary>
    /// Imports a third-party plugin from a .dll assembly, copies it to the plugins directory, and activates it.
    /// </summary>
    Task<IReadOnlyList<IPlugin>> ImportPluginAsync(string dllPath);

    /// <summary>
    /// Returns the directory where external plugins are located (%LocalAppData%/CodeViewer/Plugins).
    /// </summary>
    string GetPluginsDirectory();

    /// <summary>
    /// Executes a plugin on the given document and editor context.
    /// </summary>
    Task ExecutePluginAsync(
        IPlugin plugin,
        DocumentViewModel document,
        string selectedText,
        Action<string> replaceSelection,
        Action<string> replaceAll,
        Action<string> insertText,
        IDialogService dialogService);
}
