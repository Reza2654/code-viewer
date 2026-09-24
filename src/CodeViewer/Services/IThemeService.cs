using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Service managing app and editor color themes, dynamic loading, and theme persistence.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// All available built-in and user-imported color themes.
    /// </summary>
    IReadOnlyList<ColorTheme> AvailableThemes { get; }

    /// <summary>
    /// The currently active color theme.
    /// </summary>
    ColorTheme CurrentTheme { get; }

    /// <summary>
    /// Event fired whenever the theme changes.
    /// </summary>
    event Action<ColorTheme>? ThemeChanged;

    /// <summary>
    /// Applies a theme by its unique Id or Name.
    /// </summary>
    void ApplyTheme(string themeIdOrName);

    /// <summary>
    /// Applies a specific color theme instance.
    /// </summary>
    void ApplyTheme(ColorTheme theme);

    /// <summary>
    /// Imports a theme from a JSON file, saves it to user themes folder, and applies it.
    /// </summary>
    Task<ColorTheme> ImportThemeFromJsonAsync(string jsonFilePath);

    /// <summary>
    /// Imports a syntax highlighting definition (.xshd), saves it, and registers it with AvaloniaEdit.
    /// </summary>
    Task<string> ImportSyntaxDefinitionAsync(string xshdFilePath);

    /// <summary>
    /// Returns the directory where user themes are stored (%LocalAppData%/CodeViewer/Themes).
    /// </summary>
    string GetThemesDirectory();
}
