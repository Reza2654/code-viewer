using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class JsonFormatPlugin : IPlugin
{
    public string Id => "builtin-json-format";
    public string Name => "Format JSON (2 spaces)";
    public string Category => "JSON Tools";
    public string Description => "Parses and indents JSON text cleanly with 2-space indentation.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrWhiteSpace(input))
        {
            await context.ShowMessageAsync("Format JSON", "Document is empty.");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            var formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (context.HasSelection)
            {
                context.ReplaceSelectedText(formatted);
            }
            else
            {
                context.ReplaceAllText(formatted);
            }
        }
        catch (JsonException ex)
        {
            await context.ShowMessageAsync("JSON Format Error", $"Invalid JSON format at line {ex.LineNumber}, position {ex.BytePositionInLine}:\n{ex.Message}");
        }
        catch (Exception ex)
        {
            await context.ShowMessageAsync("JSON Format Error", ex.Message);
        }
    }
}

public class JsonMinifyPlugin : IPlugin
{
    public string Id => "builtin-json-minify";
    public string Name => "Minify JSON";
    public string Category => "JSON Tools";
    public string Description => "Removes all unnecessary whitespace, indentations, and newlines from JSON.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrWhiteSpace(input))
        {
            await context.ShowMessageAsync("Minify JSON", "Document is empty.");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            var minified = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            if (context.HasSelection)
            {
                context.ReplaceSelectedText(minified);
            }
            else
            {
                context.ReplaceAllText(minified);
            }
        }
        catch (JsonException ex)
        {
            await context.ShowMessageAsync("JSON Minify Error", $"Invalid JSON format:\n{ex.Message}");
        }
        catch (Exception ex)
        {
            await context.ShowMessageAsync("JSON Minify Error", ex.Message);
        }
    }
}
