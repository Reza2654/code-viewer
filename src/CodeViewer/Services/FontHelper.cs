using System;
using Avalonia.Media;
using AvaloniaEdit;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Helper for resolving and applying coding fonts to AvaloniaEdit controls.
/// Guarantees that FontFamily and FontSize are properly propagated to TextEditor,
/// TextArea, and TextView, invalidating text metrics and forcing line recreation.
/// </summary>
public static class FontHelper
{
    public const string DefaultFontName = "Google Sans Code";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, FontFamily> FontCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates or returns a cached composite FontFamily with embedded font fallbacks and system font fallbacks.
    /// </summary>
    public static FontFamily GetFontFamily(string? fontName)
    {
        var primary = AppSettings.SanitizeFontFamily(fontName);
        return FontCache.GetOrAdd(primary, p =>
        {
            string composite;
            if (string.Equals(p, AppSettings.RobotoMono, StringComparison.OrdinalIgnoreCase))
            {
                // When Roboto Mono is active: system font first, embedded font asset fallback, then general monospace fallbacks
                composite = "Roboto Mono, avares://CodeViewer/Assets/Fonts#Roboto Mono, Cascadia Code, Consolas, Segoe UI, Tahoma, Vazirmatn, Arial, sans-serif, monospace";
            }
            else if (string.Equals(p, AppSettings.GoogleSansCode, StringComparison.OrdinalIgnoreCase))
            {
                // When Google Sans Code is active: system font first, embedded font asset fallback, then general monospace fallbacks
                composite = "Google Sans Code, avares://CodeViewer/Assets/Fonts#Google Sans Code, Cascadia Code, Consolas, Segoe UI, Tahoma, Vazirmatn, Arial, sans-serif, monospace";
            }
            else
            {
                // Custom font: chosen font first, then embedded Google Sans Code & Roboto Mono, then general monospace fallbacks
                composite = $"{p}, Google Sans Code, avares://CodeViewer/Assets/Fonts#Google Sans Code, Roboto Mono, avares://CodeViewer/Assets/Fonts#Roboto Mono, Cascadia Code, Consolas, Segoe UI, Tahoma, Vazirmatn, Arial, sans-serif, monospace";
            }

            return new FontFamily(composite);
        });
    }

    /// <summary>
    /// Applies font family and font size to an AvaloniaEdit TextEditor and all of its inner controls (TextArea and TextView).
    /// Also keeps Application.Current.Resources["CodeFontFamily"] synchronized for all XAML dynamic bindings.
    /// </summary>
    public static void ApplyToEditor(TextEditor? editor, string? fontName, double fontSize)
    {
        if (editor == null) return;

        try
        {
            var ff = GetFontFamily(fontName);

            editor.FontFamily = ff;
            editor.FontSize = fontSize;

            if (editor.TextArea != null)
            {
                editor.TextArea.FontFamily = ff;
                editor.TextArea.FontSize = fontSize;

                if (editor.TextArea.TextView != null)
                {
                    editor.TextArea.TextView.Redraw();
                    editor.TextArea.TextView.InvalidateVisual();
                }
            }

            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.Resources["CodeFontFamily"] = ff;
            }
        }
        catch
        {
            // Fallback safely
        }
    }
}
