using System;
using System.IO;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta15FeatureTests
{
    private ThemeService _themeService = null!;

    [TestInitialize]
    public void Setup()
    {
        _themeService = new ThemeService();
    }

    [TestMethod]
    public void PowerShell_SyntaxHighlighting_HasAccessibleColorsAndNoBold()
    {
        var darkPlus = _themeService.AvailableThemes.First(t => t.Id == "dark-plus");
        _themeService.ApplyTheme(darkPlus);

        var psDef = HighlightingManager.Instance.GetDefinition("PowerShell");
        Assert.IsNotNull(psDef, "PowerShell highlighting definition must be registered.");

        // All tokens must have Normal font weight (no bold text)
        foreach (var color in psDef.NamedHighlightingColors)
        {
            Assert.AreEqual(FontWeight.Normal, color.FontWeight,
                $"PowerShell color '{color.Name}' must have Normal font weight.");
        }

        // PowerShell cmdlets (e.g. Write-Host, ConvertTo-Json) use the 'Command' color rule
        var commandColor = psDef.NamedHighlightingColors.First(c => c.Name == "Command");
        var cmdBrush = (ISolidColorBrush)commandColor.Foreground!.GetBrush(null)!;
        Assert.AreEqual(Color.Parse(darkPlus.CodeMethod), cmdBrush.Color,
            "PowerShell cmdlets must use CodeMethod color for high contrast.");

        // Keywords (e.g. foreach, if, in)
        var kwColor = psDef.NamedHighlightingColors.First(c => c.Name == "Keywords");
        var kwBrush = (ISolidColorBrush)kwColor.Foreground!.GetBrush(null)!;
        Assert.AreEqual(Color.Parse(darkPlus.CodeKeyword), kwBrush.Color,
            "PowerShell keywords must use CodeKeyword color.");

        // Variables (e.g. $psi, $p, $file)
        var varColor = psDef.NamedHighlightingColors.First(c => c.Name == "Variable");
        var varBrush = (ISolidColorBrush)varColor.Foreground!.GetBrush(null)!;
        Assert.AreEqual(Color.Parse(darkPlus.CodeVariable), varBrush.Color,
            "PowerShell variables must use CodeVariable color.");
    }

    [TestMethod]
    public void AllMajorLanguages_HaveBoldRemovedAcrossTokens()
    {
        var darkPlus = _themeService.AvailableThemes.First(t => t.Id == "dark-plus");
        _themeService.ApplyTheme(darkPlus);

        var languages = new[] { "C#", "PowerShell", "Python", "JavaScript", "Java", "C++", "CSS", "Json", "XML" };
        foreach (var lang in languages)
        {
            var def = HighlightingManager.Instance.GetDefinition(lang);
            if (def == null) continue;

            foreach (var color in def.NamedHighlightingColors)
            {
                Assert.AreEqual(FontWeight.Normal, color.FontWeight,
                    $"Language '{lang}' color '{color.Name}' must have FontWeight.Normal.");
            }
        }
    }

    [TestMethod]
    public void SyntaxColors_DynamicallyUpdate_WhenThemeSwitchesBetweenDarkAndLight()
    {
        var darkPlus = _themeService.AvailableThemes.First(t => t.Id == "dark-plus");
        var lightTheme = _themeService.AvailableThemes.First(t => t.Id == "github-light");

        var psDef = HighlightingManager.Instance.GetDefinition("PowerShell")!;
        var csDef = HighlightingManager.Instance.GetDefinition("C#")!;

        // 1. Apply Dark Theme
        _themeService.ApplyTheme(darkPlus);

        var psCmdDark = (ISolidColorBrush)psDef.NamedHighlightingColors.First(c => c.Name == "Command").Foreground!.GetBrush(null)!;
        var csMethodDark = (ISolidColorBrush)csDef.NamedHighlightingColors.First(c => c.Name == "MethodCall").Foreground!.GetBrush(null)!;

        Assert.AreEqual(Color.Parse(darkPlus.CodeMethod), psCmdDark.Color);
        Assert.AreEqual(Color.Parse(darkPlus.CodeMethod), csMethodDark.Color);

        // 2. Switch to Light Theme
        _themeService.ApplyTheme(lightTheme);

        var psCmdLight = (ISolidColorBrush)psDef.NamedHighlightingColors.First(c => c.Name == "Command").Foreground!.GetBrush(null)!;
        var csMethodLight = (ISolidColorBrush)csDef.NamedHighlightingColors.First(c => c.Name == "MethodCall").Foreground!.GetBrush(null)!;

        Assert.AreEqual(Color.Parse(lightTheme.CodeMethod), psCmdLight.Color);
        Assert.AreEqual(Color.Parse(lightTheme.CodeMethod), csMethodLight.Color);
        Assert.AreNotEqual(psCmdDark.Color, psCmdLight.Color, "Code colors must update to match the active theme.");
    }

    [TestMethod]
    public void DarkTheme_SyntaxColors_MeetWCAGContrastRatio()
    {
        var darkPlus = _themeService.AvailableThemes.First(t => t.Id == "dark-plus");
        var bg = Color.Parse(darkPlus.EditorBackground);

        // Test contrast of all standard code syntax colors
        var colors = new[]
        {
            Color.Parse(darkPlus.CodeKeyword),
            Color.Parse(darkPlus.CodeMethod),
            Color.Parse(darkPlus.CodeString),
            Color.Parse(darkPlus.CodeNumber),
            Color.Parse(darkPlus.CodeType),
            Color.Parse(darkPlus.CodeVariable),
            Color.Parse(darkPlus.CodePunctuation)
        };

        foreach (var fg in colors)
        {
            var ratio = CalculateContrastRatio(fg, bg);
            Assert.IsTrue(ratio >= 4.5, $"Contrast ratio for {fg} against {bg} is {ratio:F2}, must be >= 4.5 (WCAG AA).");
        }
    }

    [TestMethod]
    public void RainbowBrackets_AdaptBrushesToDarkAndLightThemes()
    {
        var colorizer = new RainbowBracketColorizer();

        // Dark theme: should use DarkRainbowBrushes
        colorizer.UpdateTheme(isDark: true);
        var darkFirst = RainbowBracketColorizer.DarkRainbowBrushes[0];

        // Light theme: should use LightRainbowBrushes (higher contrast against white)
        colorizer.UpdateTheme(isDark: false);
        var lightFirst = RainbowBracketColorizer.LightRainbowBrushes[0];

        var darkColor = ((ISolidColorBrush)darkFirst).Color;
        var lightColor = ((ISolidColorBrush)lightFirst).Color;

        Assert.AreNotEqual(darkColor, lightColor, "Rainbow bracket colors must adapt to light/dark themes.");

        // Verify light rainbow brackets have high contrast against white
        var white = Color.Parse("#FFFFFF");
        foreach (var brush in RainbowBracketColorizer.LightRainbowBrushes)
        {
            var c = ((ISolidColorBrush)brush).Color;
            var ratio = CalculateContrastRatio(c, white);
            Assert.IsTrue(ratio >= 3.0, $"Light rainbow bracket {c} must have accessible contrast >= 3.0 on white, got {ratio:F2}.");
        }
    }

    [TestMethod]
    public void CustomCodeVariable_OverridesDefaultVariableColor()
    {
        var settings = new AppSettings
        {
            ThemeId = "dark-plus",
            CustomCodeVariable = "#FF00FF" // Custom magenta variable override
        };

        _themeService.ApplyTheme(settings);
        var psDef = HighlightingManager.Instance.GetDefinition("PowerShell")!;
        var varColor = psDef.NamedHighlightingColors.First(c => c.Name == "Variable");
        var brush = (ISolidColorBrush)varColor.Foreground!.GetBrush(null)!;

        Assert.AreEqual(Color.Parse("#FF00FF"), brush.Color, "CustomCodeVariable setting must override syntax variable color.");
    }

    [TestMethod]
    public void CustomSyntaxHighlighters_Dart_AgentLang_PS2_HaveBoldRemoved()
    {
        LanguageService.EnsureCustomHighlighterLoaded("Dart");
        LanguageService.EnsureCustomHighlighterLoaded("AgentLang");
        LanguageService.EnsureCustomHighlighterLoaded("PS2");

        var darkPlus = _themeService.AvailableThemes.First(t => t.Id == "dark-plus");
        _themeService.ApplyTheme(darkPlus);

        foreach (var lang in new[] { "Dart", "AgentLang", "PS2" })
        {
            var def = HighlightingManager.Instance.GetDefinition(lang);
            Assert.IsNotNull(def, $"Definition for {lang} should be loaded.");
            foreach (var color in def.NamedHighlightingColors)
            {
                Assert.AreEqual(FontWeight.Normal, color.FontWeight,
                    $"Color '{color.Name}' in custom highlighter '{lang}' must have Normal font weight.");
            }
        }
    }

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBeta15()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.AreEqual("1.2.0-beta.15", version);

        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.15", "1.2.0-beta.14");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.15 must be recognized as newer than v1.2.0-beta.14");
    }

    private static double CalculateContrastRatio(Color c1, Color c2)
    {
        var l1 = RelativeLuminance(c1);
        var l2 = RelativeLuminance(c2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;

        r = (r <= 0.03928) ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = (g <= 0.03928) ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = (b <= 0.03928) ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }
}
