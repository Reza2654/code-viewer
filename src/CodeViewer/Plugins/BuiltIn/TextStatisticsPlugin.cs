using System;
using System.Text;
using System.Threading.Tasks;

namespace CodeViewer.Plugins.BuiltIn;

public class TextStatisticsPlugin : IPlugin
{
    public string Id => "builtin-text-statistics";
    public string Name => "Text Statistics";
    public string Category => "Text Utilities";
    public string Description => "Analyzes character, word, line count, and byte size of the document or selection.";
    public string Author => "Code Viewer Team";
    public string Version => "1.0.0";

    public async Task ExecuteAsync(IPluginContext context)
    {
        var text = context.HasSelection ? context.SelectedText : context.CurrentText;
        var scope = context.HasSelection ? "Selection" : "Document";

        if (string.IsNullOrEmpty(text))
        {
            await context.ShowMessageAsync("Text Statistics", $"{scope} is empty.");
            return;
        }

        var charCount = text.Length;
        var charsNoSpaces = 0;
        var wordCount = 0;
        var inWord = false;

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (!char.IsWhiteSpace(ch))
            {
                charsNoSpaces++;
                if (!inWord)
                {
                    inWord = true;
                    wordCount++;
                }
            }
            else
            {
                inWord = false;
            }
        }

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lineCount = lines.Length;
        var nonEmptyLines = 0;
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                nonEmptyLines++;
            }
        }

        var byteCount = Encoding.UTF8.GetByteCount(text);

        var sb = new StringBuilder();
        sb.AppendLine($"Scope: {scope}");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"• Total Characters: {charCount:N0}");
        sb.AppendLine($"• Characters (no spaces): {charsNoSpaces:N0}");
        sb.AppendLine($"• Total Words: {wordCount:N0}");
        sb.AppendLine($"• Total Lines: {lineCount:N0}");
        sb.AppendLine($"• Non-Empty Lines: {nonEmptyLines:N0}");
        sb.AppendLine($"• UTF-8 Size: {byteCount:N0} bytes");

        await context.ShowMessageAsync($"Text Statistics ({scope})", sb.ToString());
    }
}
