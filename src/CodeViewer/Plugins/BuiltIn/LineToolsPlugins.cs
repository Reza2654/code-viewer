using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class SortLinesAscendingPlugin : IPlugin
{
    public string Id => "builtin-sort-lines-asc";
    public string Name => "Sort Lines (A to Z)";
    public string Category => "Line Utilities";
    public string Description => "Sorts selected lines (or whole document) alphabetically in ascending order.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var separator = input.Contains("\r\n") ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sorted = lines.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToArray();
        var result = string.Join(separator, sorted);

        if (context.HasSelection)
        {
            context.ReplaceSelectedText(result);
        }
        else
        {
            context.ReplaceAllText(result);
        }

        return Task.CompletedTask;
    }
}

public class SortLinesDescendingPlugin : IPlugin
{
    public string Id => "builtin-sort-lines-desc";
    public string Name => "Sort Lines (Z to A)";
    public string Category => "Line Utilities";
    public string Description => "Sorts selected lines (or whole document) alphabetically in descending order.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var separator = input.Contains("\r\n") ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sorted = lines.OrderByDescending(l => l, StringComparer.OrdinalIgnoreCase).ToArray();
        var result = string.Join(separator, sorted);

        if (context.HasSelection)
        {
            context.ReplaceSelectedText(result);
        }
        else
        {
            context.ReplaceAllText(result);
        }

        return Task.CompletedTask;
    }
}

public class RemoveDuplicateLinesPlugin : IPlugin
{
    public string Id => "builtin-remove-duplicate-lines";
    public string Name => "Remove Duplicate Lines";
    public string Category => "Line Utilities";
    public string Description => "Removes duplicate lines while preserving the original line sequence.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var separator = input.Contains("\r\n") ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var unique = lines.Distinct(StringComparer.Ordinal).ToArray();
        var result = string.Join(separator, unique);

        if (context.HasSelection)
        {
            context.ReplaceSelectedText(result);
        }
        else
        {
            context.ReplaceAllText(result);
        }

        return Task.CompletedTask;
    }
}

public class ReverseLinesPlugin : IPlugin
{
    public string Id => "builtin-reverse-lines";
    public string Name => "Reverse Lines";
    public string Category => "Line Utilities";
    public string Description => "Inverts the order of lines from top to bottom.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var separator = input.Contains("\r\n") ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        Array.Reverse(lines);
        var result = string.Join(separator, lines);

        if (context.HasSelection)
        {
            context.ReplaceSelectedText(result);
        }
        else
        {
            context.ReplaceAllText(result);
        }

        return Task.CompletedTask;
    }
}
