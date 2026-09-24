using System;
using System.Text;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class Base64EncodePlugin : IPlugin
{
    public string Id => "builtin-base64-encode";
    public string Name => "Base64 Encode";
    public string Category => "Encodings";
    public string Description => "Encodes UTF-8 text into standard Base64 representation.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrEmpty(input))
        {
            await context.ShowMessageAsync("Base64 Encode", "No text to encode.");
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var encoded = Convert.ToBase64String(bytes);

            if (context.HasSelection)
            {
                context.ReplaceSelectedText(encoded);
            }
            else
            {
                context.ReplaceAllText(encoded);
            }
        }
        catch (Exception ex)
        {
            await context.ShowMessageAsync("Base64 Encode Error", ex.Message);
        }
    }
}

public class Base64DecodePlugin : IPlugin
{
    public string Id => "builtin-base64-decode";
    public string Name => "Base64 Decode";
    public string Category => "Encodings";
    public string Description => "Decodes Base64 text back into UTF-8 plaintext.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var input = context.HasSelection ? context.SelectedText : context.CurrentText;
        if (string.IsNullOrWhiteSpace(input))
        {
            await context.ShowMessageAsync("Base64 Decode", "No Base64 text to decode.");
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(input.Trim());
            var decoded = Encoding.UTF8.GetString(bytes);

            if (context.HasSelection)
            {
                context.ReplaceSelectedText(decoded);
            }
            else
            {
                context.ReplaceAllText(decoded);
            }
        }
        catch (FormatException)
        {
            await context.ShowMessageAsync("Base64 Decode Error", "The selected text is not a valid Base64 string.");
        }
        catch (Exception ex)
        {
            await context.ShowMessageAsync("Base64 Decode Error", ex.Message);
        }
    }
}
