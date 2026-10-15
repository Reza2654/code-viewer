using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvaloniaEdit.Highlighting;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta9FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta9_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void AppSettings_SmoothScrolling_DefaultsToTrueAndClones()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.SmoothScrolling, "SmoothScrolling should default to true.");

        var clone = settings.Clone();
        Assert.IsTrue(clone.SmoothScrolling);

        clone.SmoothScrolling = false;
        var clone2 = clone.Clone();
        Assert.IsFalse(clone2.SmoothScrolling);
    }

    [TestMethod]
    public void ThemeService_NewBuiltInThemes_AreRegistered()
    {
        var themeService = new ThemeService(_tempDir);
        var themes = themeService.AvailableThemes;

        var tokyoNight = themes.FirstOrDefault(t => t.Id == "tokyo-night");
        Assert.IsNotNull(tokyoNight, "Tokyo Night theme should be registered.");
        Assert.IsTrue(tokyoNight.IsDark);
        Assert.AreEqual("#1A1B26", tokyoNight.WindowBackground);
        Assert.AreEqual("#7AA2F7", tokyoNight.AccentColor);

        var nord = themes.FirstOrDefault(t => t.Id == "nord");
        Assert.IsNotNull(nord, "Nord theme should be registered.");
        Assert.IsTrue(nord.IsDark);
        Assert.AreEqual("#2E3440", nord.WindowBackground);

        var catppuccinMocha = themes.FirstOrDefault(t => t.Id == "catppuccin-mocha");
        Assert.IsNotNull(catppuccinMocha, "Catppuccin Mocha theme should be registered.");
        Assert.IsTrue(catppuccinMocha.IsDark);
        Assert.AreEqual("#1E1E2E", catppuccinMocha.WindowBackground);

        var gitHubDark = themes.FirstOrDefault(t => t.Id == "github-dark");
        Assert.IsNotNull(gitHubDark, "GitHub Dark theme should be registered.");
        Assert.IsTrue(gitHubDark.IsDark);
        Assert.AreEqual("#0D1117", gitHubDark.WindowBackground);

        var solarizedLight = themes.FirstOrDefault(t => t.Id == "solarized-light");
        Assert.IsNotNull(solarizedLight, "Solarized Light theme should be registered.");
        Assert.IsFalse(solarizedLight.IsDark);
        Assert.AreEqual("#FDF6E3", solarizedLight.WindowBackground);

        var catppuccinLatte = themes.FirstOrDefault(t => t.Id == "catppuccin-latte");
        Assert.IsNotNull(catppuccinLatte, "Catppuccin Latte theme should be registered.");
        Assert.IsFalse(catppuccinLatte.IsDark);
        Assert.AreEqual("#EFF1F5", catppuccinLatte.WindowBackground);
    }

    [TestMethod]
    public async Task ThemeService_ImportThemeFromJsonAsync_PersistsAcrossRestart()
    {
        // 1. Create a raw JSON theme missing Id and Name
        var customTheme = new ColorTheme
        {
            WindowBackground = "#222233",
            Foreground = "#EEEEFF",
            AccentColor = "#55AAFF",
            IsDark = true
        };
        var rawJson = JsonSerializer.Serialize(customTheme);
        var sourceFilePath = Path.Combine(_tempDir, "Aura Glow.json");
        await File.WriteAllTextAsync(sourceFilePath, rawJson);

        // 2. Import into ThemeService
        var appDataDir = Path.Combine(_tempDir, "AppData");
        var service1 = new ThemeService(appDataDir);
        var imported = await service1.ImportThemeFromJsonAsync(sourceFilePath);

        Assert.IsNotNull(imported);
        Assert.AreEqual("aura-glow", imported.Id);
        Assert.AreEqual("Aura Glow", imported.Name);
        Assert.IsTrue(service1.AvailableThemes.Any(t => t.Id == "aura-glow"));

        // 3. Simulate App Restart: create fresh ThemeService instance pointing to same AppData
        var service2 = new ThemeService(appDataDir);
        var reloadedTheme = service2.AvailableThemes.FirstOrDefault(t => t.Id == "aura-glow");
        Assert.IsNotNull(reloadedTheme, "Imported theme should persist and reload on app restart.");
        Assert.AreEqual("#222233", reloadedTheme.WindowBackground);
        Assert.AreEqual("#EEEEFF", reloadedTheme.Foreground);
        Assert.AreEqual("#55AAFF", reloadedTheme.AccentColor);
    }

    [TestMethod]
    public void ThemeService_ResolveEffectiveTheme_PrioritizesThemeId_AndPreservesColors()
    {
        var themeService = new ThemeService(_tempDir);
        var settings = new AppSettings
        {
            ThemeMode = "Dark",
            ThemeId = "nord",
            DarkPresetId = "default-dark", // Old preset lingering in settings
            DarkBackgroundHex = "#101010",  // Default dark hexes
            DarkForegroundHex = "#CCCCCC",
            DarkAccentHex = "#007ACC"
        };

        var effective = themeService.ResolveEffectiveTheme(settings);
        Assert.IsNotNull(effective);
        Assert.AreEqual("nord", effective.Id);
        Assert.AreEqual("#2E3440", effective.WindowBackground, "Nord background should be preserved, not overwritten by default #101010");
        Assert.AreEqual("#D8DEE9", effective.Foreground, "Nord foreground should be preserved, not overwritten by default #CCCCCC");
    }

    [TestMethod]
    public async Task MainViewModel_SelectTheme_PersistsImportedTheme()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var themeService = new ThemeService(_tempDir);
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));

        var vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            themeService: themeService,
            settingsService: settingsService,
            sessionService: sessionService);

        await vm.InitializeAsync();

        var nordTheme = vm.AvailableThemes.First(t => t.Id == "nord");
        vm.SelectTheme(nordTheme);

        Assert.AreEqual("nord", vm.SettingsService.CurrentSettings.ThemeId);
        Assert.AreEqual("nord", vm.SettingsService.CurrentSettings.DarkPresetId);
        Assert.AreEqual("Dark", vm.SettingsService.CurrentSettings.ThemeMode);
        Assert.AreEqual(nordTheme.WindowBackground, vm.SettingsService.CurrentSettings.DarkBackgroundHex);

        // Verify settings persisted to disk
        await settingsService.LoadAsync();
        var reloadedSettings = settingsService.CurrentSettings;
        Assert.AreEqual("nord", reloadedSettings.ThemeId);
        Assert.AreEqual("nord", reloadedSettings.DarkPresetId);
    }

    [TestMethod]
    public void ThemeService_ApplyCodeColorsToHighlighting_MapsExpandedTokens()
    {
        var themeService = new ThemeService(_tempDir);
        var theme = themeService.AvailableThemes.First(t => t.Id == "tokyo-night");

        // Create a mock IHighlightingDefinition with various token names
        var definition = new MockHighlightingDefinition(new[]
        {
            "Comment",
            "String",
            "AttributeValue",
            "Keyword",
            "Digits",
            "Type",
            "Tag",
            "Attribute",
            "MethodCall",
            "Preprocessor",
            "Heading",
            "Bold",
            "Italic",
            "Link",
            "PropertyKey",
            "Punctuation",
            "Variable"
        });

        themeService.ApplyCodeColorsToHighlighting(definition, theme);

        // Verify color assignments
        var kwColor = definition.GetNamedColor("Keyword");
        Assert.IsNotNull(kwColor?.Foreground);

        var tagColor = definition.GetNamedColor("Tag");
        Assert.IsNotNull(tagColor?.Foreground);

        var attrValColor = definition.GetNamedColor("AttributeValue");
        Assert.IsNotNull(attrValColor?.Foreground);

        var headingColor = definition.GetNamedColor("Heading");
        Assert.IsNotNull(headingColor?.Foreground);

        var keyColor = definition.GetNamedColor("PropertyKey");
        Assert.IsNotNull(keyColor?.Foreground);

        var punctColor = definition.GetNamedColor("Punctuation");
        Assert.IsNotNull(punctColor?.Foreground);
    }

    private sealed class MockHighlightingDefinition : IHighlightingDefinition
    {
        private readonly System.Collections.Generic.Dictionary<string, HighlightingColor> _colors = new(StringComparer.OrdinalIgnoreCase);

        public MockHighlightingDefinition(string[] colorNames)
        {
            foreach (var name in colorNames)
            {
                _colors[name] = new HighlightingColor { Name = name };
            }
        }

        public string Name => "Mock";
        public HighlightingRuleSet MainRuleSet => new();
        public HighlightingColor? GetNamedColor(string name) => _colors.TryGetValue(name, out var color) ? color : null;
        public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
        public System.Collections.Generic.IEnumerable<HighlightingColor> NamedHighlightingColors => _colors.Values;
        public System.Collections.Generic.IDictionary<string, string> Properties => new System.Collections.Generic.Dictionary<string, string>();
    }
}
