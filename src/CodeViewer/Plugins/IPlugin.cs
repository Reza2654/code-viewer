using System.Threading.Tasks;

namespace CodeViewer.Plugins;

/// <summary>
/// Interface that all Code Viewer plugins must implement.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// User-friendly display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Category under which this tool appears in the menu (e.g. "Formatting", "Text Utilities", "Encodings").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Brief description of what the plugin does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin author or creator.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Semantic version of the plugin.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Executes the plugin operation with the given context.
    /// </summary>
    Task ExecuteAsync(IPluginContext context);
}
