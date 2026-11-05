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

    public const string GoogleSansCode = "Google Sans Code";
    public const string RobotoMono = "Roboto Mono";

    private string _fontFamily = GoogleSansCode;

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
            return GoogleSansCode;
        }

        var primary = font.Contains(',') ? font.Split(',')[0] : font;
        primary = primary.Trim().Trim('"', '\'');

        if (string.Equals(primary, RobotoMono, StringComparison.OrdinalIgnoreCase))
        {
            return RobotoMono;
        }
        if (string.Equals(primary, GoogleSansCode, StringComparison.OrdinalIgnoreCase))
        {
            return GoogleSansCode;
        }

        return string.IsNullOrWhiteSpace(primary) ? GoogleSansCode : primary;
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

    [JsonPropertyName("highlightWordOccurrences")]
    public bool HighlightWordOccurrences { get; set; } = true;

    [JsonPropertyName("smoothScrolling")]
    public bool SmoothScrolling { get; set; } = true;

    [JsonPropertyName("smoothCursorBlink")]
    public bool SmoothCursorBlink { get; set; } = true;

    [JsonPropertyName("showWhitespace")]
    public bool ShowWhitespace { get; set; } = false;

    [JsonPropertyName("highlightCurrentLine")]
    public bool HighlightCurrentLine { get; set; } = true;

    [JsonPropertyName("enableBracketMatching")]
    public bool EnableBracketMatching { get; set; } = true;

    [JsonPropertyName("autoCloseBrackets")]
    public bool AutoCloseBrackets { get; set; } = true;

    [JsonPropertyName("enableRainbowBrackets")]
    public bool EnableRainbowBrackets { get; set; } = true;

    [JsonPropertyName("showBreadcrumbs")]
    public bool ShowBreadcrumbs { get; set; } = true;

    // Custom Syntax Highlighting Color Overrides (hex format: #RRGGBB, empty or null means use theme default)
    [JsonPropertyName("customCodeKeyword")]
    public string? CustomCodeKeyword { get; set; }

    [JsonPropertyName("customCodeComment")]
    public string? CustomCodeComment { get; set; }

    [JsonPropertyName("customCodeString")]
    public string? CustomCodeString { get; set; }

    [JsonPropertyName("customCodeNumber")]
    public string? CustomCodeNumber { get; set; }

    [JsonPropertyName("customCodeType")]
    public string? CustomCodeType { get; set; }

    [JsonPropertyName("customCodeMethod")]
    public string? CustomCodeMethod { get; set; }

    [JsonPropertyName("customCodePreprocessor")]
    public string? CustomCodePreprocessor { get; set; }

    [JsonPropertyName("customCodePunctuation")]
    public string? CustomCodePunctuation { get; set; }

    [JsonPropertyName("customCodeVariable")]
    public string? CustomCodeVariable { get; set; }

    [JsonPropertyName("autoCheckForUpdates")]
    public bool AutoCheckForUpdates { get; set; } = true;

    [JsonPropertyName("enableAnimations")]
    public bool EnableAnimations { get; set; } = true;

    [JsonPropertyName("runInBackgroundOnStartup")]
    public bool RunInBackgroundOnStartup { get; set; } = false;

    [JsonPropertyName("minimizeToTrayOnClose")]
    public bool MinimizeToTrayOnClose { get; set; } = false;

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
            RestorePreviousSession = RestorePreviousSession,
            HighlightWordOccurrences = HighlightWordOccurrences,
            SmoothScrolling = SmoothScrolling,
            SmoothCursorBlink = SmoothCursorBlink,
            ShowWhitespace = ShowWhitespace,
            HighlightCurrentLine = HighlightCurrentLine,
            EnableBracketMatching = EnableBracketMatching,
            AutoCloseBrackets = AutoCloseBrackets,
            EnableRainbowBrackets = EnableRainbowBrackets,
            ShowBreadcrumbs = ShowBreadcrumbs,
            CustomCodeKeyword = CustomCodeKeyword,
            CustomCodeComment = CustomCodeComment,
            CustomCodeString = CustomCodeString,
            CustomCodeNumber = CustomCodeNumber,
            CustomCodeType = CustomCodeType,
            CustomCodeMethod = CustomCodeMethod,
            CustomCodePreprocessor = CustomCodePreprocessor,
            CustomCodePunctuation = CustomCodePunctuation,
            CustomCodeVariable = CustomCodeVariable,
            AutoCheckForUpdates = AutoCheckForUpdates,
            EnableAnimations = EnableAnimations,
            RunInBackgroundOnStartup = RunInBackgroundOnStartup,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose
        };
    }
}
