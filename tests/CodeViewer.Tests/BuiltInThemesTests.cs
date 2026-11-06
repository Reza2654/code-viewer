using System;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class BuiltInThemesTests
{
    [TestMethod]
    public void BuiltInThemes_GetAll_Returns20Themes()
    {
        var themes = BuiltInThemes.GetAll();

        Assert.IsNotNull(themes);
        Assert.AreEqual(20, themes.Count);
    }

    [TestMethod]
    public void BuiltInThemes_AllThemesHaveUniqueIdsAndNames()
    {
        var themes = BuiltInThemes.GetAll();

        var ids = themes.Select(t => t.Id).ToList();
        var uniqueIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.AreEqual(ids.Count, uniqueIds.Count, "Theme IDs should be unique.");

        foreach (var theme in themes)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(theme.Id), "Theme ID should not be blank.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(theme.Name), "Theme Name should not be blank.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.WindowBackground), $"Theme {theme.Id} WindowBackground should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.Foreground), $"Theme {theme.Id} Foreground should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.EditorBackground), $"Theme {theme.Id} EditorBackground should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.EditorForeground), $"Theme {theme.Id} EditorForeground should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.CodeKeyword), $"Theme {theme.Id} CodeKeyword should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.CodeString), $"Theme {theme.Id} CodeString should be valid hex.");
            Assert.IsTrue(ThemeService.IsValidHexColor(theme.CodeComment), $"Theme {theme.Id} CodeComment should be valid hex.");
        }
    }

    [TestMethod]
    public void BuiltInThemes_ContainsBothDarkAndLightThemes()
    {
        var themes = BuiltInThemes.GetAll();

        var darkCount = themes.Count(t => t.IsDark);
        var lightCount = themes.Count(t => !t.IsDark);

        Assert.IsTrue(darkCount >= 10, "Should contain substantial dark themes.");
        Assert.IsTrue(lightCount >= 3, "Should contain light themes.");
    }

    [TestMethod]
    public void BuiltInThemes_CreateSampleCustomTheme_ReturnsValidNordicFrost()
    {
        var sample = BuiltInThemes.CreateSampleCustomTheme();

        Assert.IsNotNull(sample);
        Assert.AreEqual("nordic-frost", sample.Id);
        Assert.AreEqual("Nordic Frost", sample.Name);
        Assert.IsTrue(sample.IsDark);
        Assert.IsTrue(ThemeService.IsValidHexColor(sample.WindowBackground));
        Assert.IsTrue(ThemeService.IsValidHexColor(sample.AccentColor));
    }

    [TestMethod]
    public void HighlightingThemeApplicator_Apply_GracefullyHandlesNull()
    {
        // Should not throw
        HighlightingThemeApplicator.Apply(null, null);
        HighlightingThemeApplicator.Apply(null, new ColorTheme());
        HighlightingThemeApplicator.ApplyToAll(null);
    }

    [TestMethod]
    public void HighlightingThemeApplicator_Apply_NormalizesFontStyles()
    {
        var darkTheme = BuiltInThemes.GetAll().First(t => t.Id == "dark-plus");
        var ps = HighlightingManager.Instance.GetDefinition("PowerShell");
        Assert.IsNotNull(ps);

        HighlightingThemeApplicator.Apply(ps, darkTheme);

        foreach (var color in ps.NamedHighlightingColors)
        {
            Assert.AreEqual(FontWeight.Normal, color.FontWeight);
            Assert.AreEqual(FontStyle.Normal, color.FontStyle);
            Assert.IsNotNull(color.Foreground);
        }
    }
}
