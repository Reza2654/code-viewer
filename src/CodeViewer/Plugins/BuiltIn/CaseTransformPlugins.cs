using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class UpperCasePlugin : IPlugin
{
    public string Id => "builtin-case-upper";
    public string Name => "UPPERCASE";
    public string Category => "Case Transform";
    public string Description => "Converts text to uppercase.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var result = input.ToUpperInvariant();
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

public class LowerCasePlugin : IPlugin
{
    public string Id => "builtin-case-lower";
    public string Name => "lowercase";
    public string Category => "Case Transform";
    public string Description => "Converts text to lowercase.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var result = input.ToLowerInvariant();
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

public class TitleCasePlugin : IPlugin
{
    public string Id => "builtin-case-title";
    public string Name => "Title Case";
    public string Category => "Case Transform";
    public string Description => "Capitalizes the first character of each word.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input)) return Task.CompletedTask;

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var result = textInfo.ToTitleCase(input.ToLowerInvariant());
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
