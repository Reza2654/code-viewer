using System;
using System.Text.Json.Serialization;

namespace CodeViewer.Models;

/// <summary>
/// Persistent user configuration settings.
/// </summary>
public class AppSettings
{
    [JsonPropertyName("themeId")]
    public string ThemeId { get; set; } = "dark-plus";

    private string _fontFamily = "Cascadia Code";

    [JsonPropertyName("fontFamily")]
    public string FontFamily
    {
        get => _fontFamily;
        set => _fontFamily = SanitizeFontFamily(value);
    }

    public static string SanitizeFontFamily(string? font)
    {
        if (string.IsNullOrWhiteSpace(font))
        {
            return "Cascadia Code";
        }

        var primary = font.Contains(',') ? font.Split(',')[0] : font;
        primary = primary.Trim().Trim('"', '\'');
        return string.IsNullOrWhiteSpace(primary) ? "Cascadia Code" : primary;
    }

    [JsonPropertyName("fontSize")]
    public double FontSize { get; set; } = 14.0;

    [JsonPropertyName("wordWrap")]
    public bool WordWrap { get; set; } = false;

    [JsonPropertyName("showLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;

    [JsonPropertyName("tabSize")]
    public int TabSize { get; set; } = 4;

    [JsonPropertyName("maxRecentFiles")]
    public int MaxRecentFiles { get; set; } = 20;

    [JsonPropertyName("restorePreviousSession")]
    public bool RestorePreviousSession { get; set; } = false;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            ThemeId = ThemeId,
            FontFamily = FontFamily,
            FontSize = FontSize,
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            TabSize = TabSize,
            MaxRecentFiles = MaxRecentFiles,
            RestorePreviousSession = RestorePreviousSession
        };
    }
}
