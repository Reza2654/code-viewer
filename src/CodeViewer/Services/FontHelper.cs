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
            var composite = $"{p}, Roboto Mono, avares://CodeViewer/Assets/Fonts#Roboto Mono, Google Sans Code, avares://CodeViewer/Assets/Fonts#Google Sans Code, Cascadia Code, Consolas, Segoe UI, Tahoma, Vazirmatn, Arial, sans-serif, monospace";
            return new FontFamily(composite);
        });
    }

    /// <summary>
    /// Applies font family and font size to an AvaloniaEdit TextEditor and all of its inner controls (TextArea and TextView).
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
                    editor.TextArea.TextView.SetValue(Avalonia.Controls.Primitives.TemplatedControl.FontFamilyProperty, ff);
                    editor.TextArea.TextView.SetValue(Avalonia.Controls.Primitives.TemplatedControl.FontSizeProperty, fontSize);
                    editor.TextArea.TextView.Redraw();
                }
            }
        }
        catch
        {
            // Fallback safely
        }
    }
}
