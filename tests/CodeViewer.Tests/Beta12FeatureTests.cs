using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvaloniaEdit;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta12FeatureTests
{
    [TestMethod]
    public void FontHelper_GoogleSansCode_PlacesGoogleSansCodeFirst()
    {
        var ff = FontHelper.GetFontFamily("Google Sans Code");
        Assert.IsNotNull(ff);
        Assert.AreEqual("Google Sans Code", ff.Name);
    }

    [TestMethod]
    public void FontHelper_RobotoMono_PlacesRobotoMonoFirst()
    {
        var ff = FontHelper.GetFontFamily("Roboto Mono");
        Assert.IsNotNull(ff);
        Assert.AreEqual("Roboto Mono", ff.Name);
    }

    [TestMethod]
    public void FontHelper_CustomFont_PlacesCustomFontFirst()
    {
        var ff = FontHelper.GetFontFamily("JetBrains Mono");
        Assert.IsNotNull(ff);
        Assert.AreEqual("JetBrains Mono", ff.Name);
    }

    [TestMethod]
    public void ThemeService_HasAll20BuiltInThemes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_TestThemes_" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ThemeService(tempDir);
            var themes = service.AvailableThemes;

            Assert.IsTrue(themes.Count >= 20, $"Expected at least 20 themes, but found {themes.Count}");

            // Verify the 6 newly added themes
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "cyberpunk"), "Missing cyberpunk theme");
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "gruvbox-dark"), "Missing gruvbox-dark theme");
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "monokai-pro"), "Missing monokai-pro theme");
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "rose-pine"), "Missing rose-pine theme");
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "aura-dark"), "Missing aura-dark theme");
            Assert.IsNotNull(themes.FirstOrDefault(t => t.Id == "one-light"), "Missing one-light theme");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void ThemeService_ResolvesCustomCodeSyntaxOverrides()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_TestSyntax_" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ThemeService(tempDir);
            var settings = new AppSettings
            {
                ThemeId = "dark-plus",
                ThemeMode = "Dark",
                CustomCodeKeyword = "#FF00AA",
                CustomCodeComment = "#00FFBB",
                CustomCodeString = "#FFEE00"
            };

            var effective = service.ResolveEffectiveTheme(settings);
            Assert.AreEqual("#FF00AA", effective.CodeKeyword);
            Assert.AreEqual("#00FFBB", effective.CodeComment);
            Assert.AreEqual("#FFEE00", effective.CodeString);
            // Non-overridden tokens keep base theme values
            Assert.IsFalse(string.IsNullOrWhiteSpace(effective.CodeNumber));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void ThemeService_LoadSingleUserTheme_ScansFolderIfFilenameDiffers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_TestScan_" + Guid.NewGuid().ToString("N"));
        try
        {
            var themesDir = Path.Combine(tempDir, "Themes");
            Directory.CreateDirectory(themesDir);

            // Create a theme with ID "awesome-sunset", but saved as "CustomThemeFile.json"
            var customTheme = new ColorTheme
            {
                Id = "awesome-sunset",
                Name = "Awesome Sunset",
                IsDark = true,
                WindowBackground = "#1A1A24",
                Foreground = "#F0F0F0",
                EditorBackground = "#1A1A24",
                EditorForeground = "#F0F0F0"
            };

            var json = JsonSerializer.Serialize(customTheme);
            File.WriteAllText(Path.Combine(themesDir, "CustomThemeFile.json"), json);

            // Also write current_theme.txt to point to "awesome-sunset"
            File.WriteAllText(Path.Combine(tempDir, "current_theme.txt"), "awesome-sunset");

            var service = new ThemeService(tempDir);
            Assert.AreEqual("awesome-sunset", service.CurrentTheme.Id);
            Assert.AreEqual("Awesome Sunset", service.CurrentTheme.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void AppSettings_Clone_PreservesBeta12Properties()
    {
        var original = new AppSettings
        {
            SmoothCursorBlink = false,
            ShowWhitespace = true,
            HighlightCurrentLine = false,
            EnableBracketMatching = false,
            CustomCodeKeyword = "#112233",
            CustomCodeComment = "#445566",
            CustomCodeString = "#778899",
            CustomCodeNumber = "#AABBCC",
            CustomCodeType = "#DDEEFF",
            CustomCodeMethod = "#123456",
            CustomCodePreprocessor = "#654321",
            CustomCodePunctuation = "#FEDCBA"
        };

        var clone = original.Clone();

        Assert.IsFalse(clone.SmoothCursorBlink);
        Assert.IsTrue(clone.ShowWhitespace);
        Assert.IsFalse(clone.HighlightCurrentLine);
        Assert.IsFalse(clone.EnableBracketMatching);
        Assert.AreEqual("#112233", clone.CustomCodeKeyword);
        Assert.AreEqual("#445566", clone.CustomCodeComment);
        Assert.AreEqual("#778899", clone.CustomCodeString);
        Assert.AreEqual("#AABBCC", clone.CustomCodeNumber);
        Assert.AreEqual("#DDEEFF", clone.CustomCodeType);
        Assert.AreEqual("#123456", clone.CustomCodeMethod);
        Assert.AreEqual("#654321", clone.CustomCodePreprocessor);
        Assert.AreEqual("#FEDCBA", clone.CustomCodePunctuation);
    }

    [TestMethod]
    public void SettingsViewModel_ThemeFilteringAndSelection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_TestVM_" + Guid.NewGuid().ToString("N"));
        try
        {
            var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
            var themeService = new ThemeService(tempDir);
            var vm = new SettingsViewModel(settingsService, themeService);

            // Filter Dark
            vm.SetThemeFilter("Dark");
            Assert.AreEqual("Dark", vm.ThemeFilterMode);
            Assert.IsTrue(vm.FilteredThemes.All(t => t.IsDark));

            // Filter Light
            vm.SetThemeFilter("Light");
            Assert.AreEqual("Light", vm.ThemeFilterMode);
            Assert.IsTrue(vm.FilteredThemes.All(t => !t.IsDark));

            // Select a theme from gallery
            var cyberpunk = vm.AvailableThemes.First(t => t.Id == "cyberpunk");
            vm.SelectThemeFromGallery(cyberpunk);
            Assert.AreEqual("cyberpunk", vm.SelectedTheme.Id);
            Assert.AreEqual("Dark", vm.ThemeMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void SettingsViewModel_ResetSyntaxColors_ClearsCustomOverrides()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_TestReset_" + Guid.NewGuid().ToString("N"));
        try
        {
            var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
            var themeService = new ThemeService(tempDir);
            var vm = new SettingsViewModel(settingsService, themeService)
            {
                CustomCodeKeyword = "#123456",
                CustomCodeComment = "#654321"
            };

            Assert.IsNotNull(vm.CustomCodeKeyword);
            vm.ResetSyntaxColors();

            Assert.IsNull(vm.CustomCodeKeyword);
            Assert.IsNull(vm.CustomCodeComment);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void Renderers_IsEnabled_TogglesGracefully()
    {
        var currentLine = new CurrentLineHighlighter(null!);
        var bracketMatching = new BracketMatchingRenderer(null!);

        Assert.IsTrue(currentLine.IsEnabled);
        Assert.IsTrue(bracketMatching.IsEnabled);

        currentLine.IsEnabled = false;
        bracketMatching.IsEnabled = false;

        Assert.IsFalse(currentLine.IsEnabled);
        Assert.IsFalse(bracketMatching.IsEnabled);
    }
}
