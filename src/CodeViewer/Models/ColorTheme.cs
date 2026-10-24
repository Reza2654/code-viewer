using System;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace CodeViewer.Models;

/// <summary>
/// Defines color palette configuration for application UI and code editor.
/// </summary>
public class ColorTheme
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isDark")]
    public bool IsDark { get; set; } = true;

    // Window & Chrome
    [JsonPropertyName("windowBackground")]
    public string WindowBackground { get; set; } = "#1E1E1E";

    [JsonPropertyName("foreground")]
    public string Foreground { get; set; } = "#D4D4D4";

    [JsonPropertyName("menuBackground")]
    public string MenuBackground { get; set; } = "#252526";

    [JsonPropertyName("menuForeground")]
    public string MenuForeground { get; set; } = "#CCCCCC";

    // Tab Strip
    [JsonPropertyName("tabBarBackground")]
    public string TabBarBackground { get; set; } = "#252526";

    [JsonPropertyName("tabItemActiveBackground")]
    public string TabItemActiveBackground { get; set; } = "#1E1E1E";

    [JsonPropertyName("tabItemInactiveBackground")]
    public string TabItemInactiveBackground { get; set; } = "#2D2D2D";

    [JsonPropertyName("tabItemActiveForeground")]
    public string TabItemActiveForeground { get; set; } = "#FFFFFF";

    [JsonPropertyName("tabItemInactiveForeground")]
    public string TabItemInactiveForeground { get; set; } = "#969696";

    // Editor Area
    [JsonPropertyName("editorBackground")]
    public string EditorBackground { get; set; } = "#1E1E1E";

    [JsonPropertyName("editorForeground")]
    public string EditorForeground { get; set; } = "#D4D4D4";

    [JsonPropertyName("lineNumbersForeground")]
    public string LineNumbersForeground { get; set; } = "#5A5A5A";

    [JsonPropertyName("selectionBackground")]
    public string SelectionBackground { get; set; } = "#264F78";

    // Status Bar & Accents
    [JsonPropertyName("statusBarBackground")]
    public string StatusBarBackground { get; set; } = "#007ACC";

    [JsonPropertyName("statusBarForeground")]
    public string StatusBarForeground { get; set; } = "#FFFFFF";

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "#007ACC";

    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; } = "#181818";

    // Code Syntax Highlighting Colors
    [JsonPropertyName("codeKeyword")]
    public string CodeKeyword { get; set; } = "#569CD6";

    [JsonPropertyName("codeComment")]
    public string CodeComment { get; set; } = "#6A9955";

    [JsonPropertyName("codeString")]
    public string CodeString { get; set; } = "#CE9178";

    [JsonPropertyName("codeNumber")]
    public string CodeNumber { get; set; } = "#B5CEA8";

    [JsonPropertyName("codeType")]
    public string CodeType { get; set; } = "#4EC9B0";

    [JsonPropertyName("codeMethod")]
    public string CodeMethod { get; set; } = "#DCDCAA";

    [JsonPropertyName("codePreprocessor")]
    public string CodePreprocessor { get; set; } = "#9B9B9B";

    [JsonPropertyName("codePunctuation")]
    public string CodePunctuation { get; set; } = "#D4D4D4";

    [JsonPropertyName("codeVariable")]
    public string CodeVariable { get; set; } = "#9CDCFE";

    [JsonIgnore]
    public IBrush EditorBackgroundBrush => new SolidColorBrush(Color.TryParse(EditorBackground, out var c) ? c : Colors.Transparent);

    [JsonIgnore]
    public IBrush AccentBrush => new SolidColorBrush(Color.TryParse(AccentColor, out var c) ? c : Colors.DodgerBlue);

    [JsonIgnore]
    public IBrush KeywordBrush => new SolidColorBrush(Color.TryParse(CodeKeyword, out var c) ? c : Colors.DodgerBlue);

    [JsonIgnore]
    public IBrush StringBrush => new SolidColorBrush(Color.TryParse(CodeString, out var c) ? c : Colors.Orange);

    [JsonIgnore]
    public IBrush CommentBrush => new SolidColorBrush(Color.TryParse(CodeComment, out var c) ? c : Colors.Green);

    [JsonIgnore]
    public IBrush NumberBrush => new SolidColorBrush(Color.TryParse(CodeNumber, out var c) ? c : Colors.LightGreen);

    [JsonIgnore]
    public IBrush TypeBrush => new SolidColorBrush(Color.TryParse(CodeType, out var c) ? c : Colors.Teal);

    [JsonIgnore]
    public IBrush MethodBrush => new SolidColorBrush(Color.TryParse(CodeMethod, out var c) ? c : Colors.Yellow);

    [JsonIgnore]
    public IBrush VariableBrush => new SolidColorBrush(Color.TryParse(CodeVariable, out var c) ? c : (IsDark ? Color.Parse("#9CDCFE") : Color.Parse("#001080")));

    public ColorTheme Clone()
    {
        return new ColorTheme
        {
            Id = Id,
            Name = Name,
            IsDark = IsDark,
            WindowBackground = WindowBackground,
            Foreground = Foreground,
            MenuBackground = MenuBackground,
            MenuForeground = MenuForeground,
            TabBarBackground = TabBarBackground,
            TabItemActiveBackground = TabItemActiveBackground,
            TabItemInactiveBackground = TabItemInactiveBackground,
            TabItemActiveForeground = TabItemActiveForeground,
            TabItemInactiveForeground = TabItemInactiveForeground,
            EditorBackground = EditorBackground,
            EditorForeground = EditorForeground,
            LineNumbersForeground = LineNumbersForeground,
            SelectionBackground = SelectionBackground,
            StatusBarBackground = StatusBarBackground,
            StatusBarForeground = StatusBarForeground,
            AccentColor = AccentColor,
            BorderColor = BorderColor,
            CodeKeyword = CodeKeyword,
            CodeComment = CodeComment,
            CodeString = CodeString,
            CodeNumber = CodeNumber,
            CodeType = CodeType,
            CodeMethod = CodeMethod,
            CodePreprocessor = CodePreprocessor,
            CodePunctuation = CodePunctuation,
            CodeVariable = CodeVariable
        };
    }
}
