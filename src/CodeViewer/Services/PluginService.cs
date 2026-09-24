using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using CodeViewer.Plugins;
using CodeViewer.Plugins.BuiltIn;
using CodeViewer.ViewModels;

namespace CodeViewer.Services;

public class PluginService : IPluginService
{
    private readonly string _pluginsDirectory;
    private readonly List<IPlugin> _plugins = [];

    public IReadOnlyList<IPlugin> Plugins => _plugins.AsReadOnly();
    public event Action? PluginsChanged;

    public PluginService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _pluginsDirectory = Path.Combine(appData, "CodeViewer", "Plugins");

        EnsureDirectoriesAndDocumentation();
        RegisterBuiltInPlugins();
        LoadExternalPlugins();
    }

    private void RegisterBuiltInPlugins()
    {
        // JSON Tools
        _plugins.Add(new JsonFormatPlugin());
        _plugins.Add(new JsonMinifyPlugin());

        // Line Utilities
        _plugins.Add(new SortLinesAscendingPlugin());
        _plugins.Add(new SortLinesDescendingPlugin());
        _plugins.Add(new RemoveDuplicateLinesPlugin());
        _plugins.Add(new ReverseLinesPlugin());

        // Encodings
        _plugins.Add(new Base64EncodePlugin());
        _plugins.Add(new Base64DecodePlugin());
        _plugins.Add(new UrlEncodePlugin());
        _plugins.Add(new UrlDecodePlugin());

        // Case Transforms
        _plugins.Add(new UpperCasePlugin());
        _plugins.Add(new LowerCasePlugin());
        _plugins.Add(new TitleCasePlugin());

        // Text Analysis
        _plugins.Add(new TextStatisticsPlugin());
    }

    private void EnsureDirectoriesAndDocumentation()
    {
        try
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
            }

            var readmePath = Path.Combine(_pluginsDirectory, "README.txt");
            if (!File.Exists(readmePath))
            {
                var guide = @"Code Viewer - External Plugins Directory
=======================================

To add an external plugin:
1. Create a .NET 8 Class Library project.
2. Reference 'CodeViewer.exe' (or implement the IPlugin interface with matching signature).
3. Compile your project to a .dll assembly.
4. Copy the .dll into this folder, or use 'Tools -> Import Plugin (.dll)...' inside Code Viewer.

Sample Plugin Implementation:
-----------------------------
using System.Threading.Tasks;
using CodeViewer.Plugins;

namespace MyCustomPlugins;

public class MyRot13Plugin : IPlugin
{
    public string Id => ""my-rot13-plugin"";
    public string Name => ""ROT13 Cipher"";
    public string Category => ""Custom Tools"";
    public string Description => ""Applies ROT13 substitution cipher to text."";
    public string Author => ""Your Name"";
    public string Version => ""1.0.0"";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var buffer = input.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            if (c >= 'a' && c <= 'z') buffer[i] = (char)((c - 'a' + 13) % 26 + 'a');
            else if (c >= 'A' && c <= 'Z') buffer[i] = (char)((c - 'A' + 13) % 26 + 'A');
        }

        var result = new string(buffer);
        if (context.HasSelection) context.ReplaceSelectedText(result);
        else context.ReplaceAllText(result);
        return Task.CompletedTask;
    }
}
";
                File.WriteAllText(readmePath, guide);
            }
        }
        catch
        {
            // Fallback handled
        }
    }

    private void LoadExternalPlugins()
    {
        if (!Directory.Exists(_pluginsDirectory)) return;

        try
        {
            var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");
            foreach (var dll in dllFiles)
            {
                LoadPluginsFromAssemblyFile(dll);
            }
        }
        catch
        {
            // Ignored
        }
    }

    private List<IPlugin> LoadPluginsFromAssemblyFile(string dllPath)
    {
        var loaded = new List<IPlugin>();
        try
        {
            // Load assembly from memory/stream so the file on disk remains unlocked
            var alc = new AssemblyLoadContext(null, isCollectible: true);
            using var fs = File.OpenRead(dllPath);
            var assembly = alc.LoadFromStream(fs);

            var types = assembly.GetExportedTypes();
            foreach (var type in types)
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    if (Activator.CreateInstance(type) is IPlugin plugin)
                    {
                        if (!_plugins.Any(p => string.Equals(p.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            _plugins.Add(plugin);
                            loaded.Add(plugin);
                        }
                    }
                }
            }
        }
        catch
        {
            // Gracefully ignore incompatible assemblies
        }
        return loaded;
    }

    public async Task<IReadOnlyList<IPlugin>> ImportPluginAsync(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Plugin assembly not found", dllPath);
        }

        var fileName = Path.GetFileName(dllPath);
        var targetFile = Path.Combine(_pluginsDirectory, fileName);

        if (!string.Equals(Path.GetFullPath(dllPath), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            // Copy file to plugins directory
            using (var sourceStream = File.OpenRead(dllPath))
            using (var destStream = File.Create(targetFile))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }

        var newPlugins = LoadPluginsFromAssemblyFile(targetFile);
        if (newPlugins.Count == 0)
        {
            throw new InvalidOperationException($"No classes implementing {nameof(IPlugin)} were found in the assembly '{fileName}'.");
        }

        PluginsChanged?.Invoke();
        return newPlugins;
    }

    public string GetPluginsDirectory()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
        return _pluginsDirectory;
    }

    public async Task ExecutePluginAsync(
        IPlugin plugin,
        DocumentViewModel document,
        string selectedText,
        Action<string> replaceSelection,
        Action<string> replaceAll,
        Action<string> insertText,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(document);

        var context = new PluginExecutionContext(
            document,
            selectedText,
            replaceSelection,
            replaceAll,
            insertText,
            dialogService);

        await plugin.ExecuteAsync(context);
    }

    private sealed class PluginExecutionContext : IPluginContext
    {
        private readonly DocumentViewModel _doc;
        private readonly string _selectedText;
        private readonly Action<string> _replaceSelection;
        private readonly Action<string> _replaceAll;
        private readonly Action<string> _insertText;
        private readonly IDialogService _dialogService;

        public string CurrentText => _doc.TextDocument.Text;
        public string SelectedText => _selectedText;
        public string? FilePath => _doc.FilePath;
        public string Language => _doc.Language;
        public bool HasSelection => !string.IsNullOrEmpty(_selectedText);

        public PluginExecutionContext(
            DocumentViewModel doc,
            string selectedText,
            Action<string> replaceSelection,
            Action<string> replaceAll,
            Action<string> insertText,
            IDialogService dialogService)
        {
            _doc = doc;
            _selectedText = selectedText ?? string.Empty;
            _replaceSelection = replaceSelection;
            _replaceAll = replaceAll;
            _insertText = insertText;
            _dialogService = dialogService;
        }

        public void ReplaceSelectedText(string newText) => _replaceSelection(newText);
        public void ReplaceAllText(string newText) => _replaceAll(newText);
        public void InsertText(string text) => _insertText(text);
        public Task ShowMessageAsync(string title, string message) => _dialogService.ShowMessageAsync(title, message);
    }
}
