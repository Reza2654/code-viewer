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
    public bool RestorePreviousSession { get; set; } = true;

    [JsonPropertyName("themeMode")]
    public string ThemeMode { get; set; } = "System";

    [JsonPropertyName("contrastMode")]
    public string ContrastMode { get; set; } = "Default";

    [JsonPropertyName("lightPresetId")]
    public string LightPresetId { get; set; } = "default-light";

    [JsonPropertyName("lightBackgroundHex")]
    public string LightBackgroundHex { get; set; } = "#F9F9F9";

    [JsonPropertyName("lightForegroundHex")]
    public string LightForegroundHex { get; set; } = "#101010";

    [JsonPropertyName("lightAccentHex")]
    public string LightAccentHex { get; set; } = "#007ACC";

    [JsonPropertyName("darkPresetId")]
    public string DarkPresetId { get; set; } = "default-dark";

    [JsonPropertyName("darkBackgroundHex")]
    public string DarkBackgroundHex { get; set; } = "#101010";

    [JsonPropertyName("darkForegroundHex")]
    public string DarkForegroundHex { get; set; } = "#CCCCCC";

    [JsonPropertyName("darkAccentHex")]
    public string DarkAccentHex { get; set; } = "#007ACC";

    public AppSettings Clone()
    {
        return new AppSettings
        {
            ThemeId = ThemeId,
            ThemeMode = ThemeMode,
            ContrastMode = ContrastMode,
            LightPresetId = LightPresetId,
            LightBackgroundHex = LightBackgroundHex,
            LightForegroundHex = LightForegroundHex,
            LightAccentHex = LightAccentHex,
            DarkPresetId = DarkPresetId,
            DarkBackgroundHex = DarkBackgroundHex,
            DarkForegroundHex = DarkForegroundHex,
            DarkAccentHex = DarkAccentHex,
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
