using System;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// Applies ColorTheme colors and typography styling to AvaloniaEdit syntax highlighters.
/// </summary>
public static class HighlightingThemeApplicator
{
    public static void Apply(IHighlightingDefinition? definition, ColorTheme? theme)
    {
        if (definition == null || theme == null) return;
        var t = theme;

        try
        {
            var kwBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeKeyword));
            var commentBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeComment));
            var stringBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeString));
            var numberBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeNumber));
            var typeBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeType));
            var methodBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeMethod));
            var preprocBrush = new SimpleHighlightingBrush(Color.Parse(t.CodePreprocessor));
            var punctBrush = new SimpleHighlightingBrush(Color.Parse(t.CodePunctuation));

            var varHex = !string.IsNullOrWhiteSpace(t.CodeVariable)
                ? t.CodeVariable
                : (t.IsDark ? "#9CDCFE" : "#001080");
            var varBrush = new SimpleHighlightingBrush(Color.Parse(varHex));

            var addedBrush = new SimpleHighlightingBrush(Color.Parse(t.IsDark ? "#89D185" : "#22863A"));
            var removedBrush = new SimpleHighlightingBrush(Color.Parse(t.IsDark ? "#F48771" : "#D73A49"));

            foreach (var color in definition.NamedHighlightingColors)
            {
                // CRITICAL FOR readability: Remove bold/italic weights that cause illegibility and visual harshness
                color.FontWeight = FontWeight.Normal;
                color.FontStyle = FontStyle.Normal;

                var name = color.Name?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("comment") || (name.Contains("xml") && name.Contains("doc")) || name.Contains("javadoc") || name.Contains("doctags"))
                {
                    color.Foreground = commentBrush;
                }
                else if (name.Contains("string") || name.Contains("char") || name.Contains("regex") || (name.Contains("literal") && !name.Contains("number")))
                {
                    color.Foreground = stringBrush;
                }
                else if (name.Contains("attributevalue") || name.Contains("attrvalue"))
                {
                    color.Foreground = stringBrush;
                }
                else if (name.Contains("digit") || name.Contains("number") || name.Contains("float") || name.Contains("hex") || name.Contains("integer"))
                {
                    color.Foreground = numberBrush;
                }
                else if (name.Contains("method") || name.Contains("function") || name.Contains("call") || name.Contains("invoc") || name.Contains("command") || name.Contains("globalfunctions") || name.Contains("intrinsics"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface") || name.Contains("enum") || name.Contains("typedef") || name.Contains("reference") || name.Contains("void"))
                {
                    color.Foreground = typeBrush;
                }
                else if (name.Contains("keyword") || name.Contains("statement") || name.Contains("controlflow") || name.Contains("loop") || name.Contains("jump") || name.Contains("exception") || name.Contains("checked") || name.Contains("unsafe") || name.Contains("modifier") || name.Contains("visibility") || name.Contains("access") || name.Contains("this") || name.Contains("base") || name.Contains("package") || name.Contains("namespace") || name.Contains("truefalse") || name.Contains("boolean") || name.Contains("bool") || name.Contains("null") || name.Contains("getsetaddremove") || name.Contains("friend") || name.Contains("compound"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("tag") || name.Contains("element") || name.Contains("slash") || name.Contains("cdata") || name.Contains("doctype") || name.Contains("xmldeclaration"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("attribute") || name.Contains("annotation"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("preprocessor") || name.Contains("directive") || name.Contains("include") || name.Contains("define") || name.Contains("macro") || name.Contains("header") || name.Contains("position"))
                {
                    color.Foreground = preprocBrush;
                }
                else if (name.Contains("added"))
                {
                    color.Foreground = addedBrush;
                }
                else if (name.Contains("removed"))
                {
                    color.Foreground = removedBrush;
                }
                else if (name.Contains("propertykey") || name.Contains("key"))
                {
                    color.Foreground = typeBrush;
                }
                else if (name.Contains("punctuation") || name.Contains("delimiter") || name.Contains("operator") || name.Contains("parenthes") || name.Contains("bracket") || name.Contains("brace") || name.Contains("curly") || name.Contains("colon") || name.Contains("semicolon") || name.Contains("assignment"))
                {
                    color.Foreground = punctBrush;
                }
                else if (name.Contains("variable") || name.Contains("property") || name.Contains("field") || name.Contains("parameter") || name.Contains("selector"))
                {
                    color.Foreground = varBrush;
                }
                else
                {
                    // Fallback contrast check for unmapped tokens to prevent invisible text
                    if (color.Foreground != null)
                    {
                        var brush = color.Foreground.GetBrush(null);
                        if (brush is SolidColorBrush scb)
                        {
                            var lum = 0.299 * scb.Color.R + 0.587 * scb.Color.G + 0.114 * scb.Color.B;
                            if (t.IsDark && lum < 60)
                            {
                                color.Foreground = varBrush;
                            }
                            else if (!t.IsDark && lum > 220)
                            {
                                color.Foreground = punctBrush;
                            }
                        }
                    }
                    else
                    {
                        color.Foreground = punctBrush;
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback
        }
    }

    public static void ApplyToAll(ColorTheme? theme)
    {
        if (theme == null) return;

        try
        {
            var manager = HighlightingManager.Instance;
            foreach (var def in manager.HighlightingDefinitions)
            {
                try
                {
                    Apply(def, theme);
                }
                catch
                {
                    // Ignore broken delay-loaded highlighters (e.g. AvaloniaEdit TeX)
                }
            }
        }
        catch
        {
            // Defensive
        }
    }
}
