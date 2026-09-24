using System;
using System.Net;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class UrlEncodePlugin : IPlugin
{
    public string Id => "builtin-url-encode";
    public string Name => "URL Encode";
    public string Category => "Encodings";
    public string Description => "Escapes reserved and special characters for URLs (Percent-encoding).";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input))
        {
            await context.ShowMessageAsync("URL Encode", "No text to encode.");
            return;
        }

        var encoded = WebUtility.UrlEncode(input);
        if (context.HasSelection)
        {
            context.ReplaceSelectedText(encoded);
        }
        else
        {
            context.ReplaceAllText(encoded);
        }
    }
}

public class UrlDecodePlugin : IPlugin
{
    public string Id => "builtin-url-decode";
    public string Name => "URL Decode";
    public string Category => "Encodings";
    public string Description => "Decodes percent-encoded URL characters back to plain text.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input))
        {
            await context.ShowMessageAsync("URL Decode", "No text to decode.");
            return;
        }

        var decoded = WebUtility.UrlDecode(input);
        if (context.HasSelection)
        {
            context.ReplaceSelectedText(decoded);
        }
        else
        {
            context.ReplaceAllText(decoded);
        }
    }
}
