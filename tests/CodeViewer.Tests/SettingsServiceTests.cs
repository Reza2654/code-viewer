using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class SettingsServiceTests
{
    private string _tempDir = null!;
    private string _tempSettingsFile = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Settings_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempSettingsFile = Path.Combine(_tempDir, "settings.json");
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
    public void SettingsService_DefaultSettings_AreSensible()
    {
        var service = new SettingsService(_tempSettingsFile);
        var settings = service.CurrentSettings;

        Assert.IsNotNull(settings);
        Assert.AreEqual("dark-plus", settings.ThemeId);
        Assert.AreEqual(14.0, settings.FontSize);
        Assert.IsTrue(settings.ShowLineNumbers);
        Assert.IsFalse(settings.WordWrap);
        Assert.IsTrue(settings.FontFamily.Contains("Cascadia Code") || settings.FontFamily.Contains("Consolas"));
    }

    [TestMethod]
    public async Task SettingsService_SaveAndLoad_PreservesSettings()
    {
        var service = new SettingsService(_tempSettingsFile);

        var newSettings = new AppSettings
        {
            ThemeId = "monokai",
            FontFamily = "Fira Code",
            FontSize = 18.0,
            WordWrap = true,
            ShowLineNumbers = false,
            TabSize = 2,
            MaxRecentFiles = 10
        };

        bool eventFired = false;
        service.SettingsChanged += s =>
        {
            if (s.FontFamily == "Fira Code") eventFired = true;
        };

        await service.SaveAsync(newSettings);

        Assert.IsTrue(File.Exists(_tempSettingsFile));
        Assert.IsTrue(eventFired);

        // Load in a fresh service instance
        var reloadedService = new SettingsService(_tempSettingsFile);
        Assert.AreEqual("monokai", reloadedService.CurrentSettings.ThemeId);
        Assert.AreEqual("Fira Code", reloadedService.CurrentSettings.FontFamily);
        Assert.AreEqual(18.0, reloadedService.CurrentSettings.FontSize);
        Assert.IsTrue(reloadedService.CurrentSettings.WordWrap);
        Assert.IsFalse(reloadedService.CurrentSettings.ShowLineNumbers);
        Assert.AreEqual(2, reloadedService.CurrentSettings.TabSize);
    }

    [TestMethod]
    public void ThemeService_BuiltInThemes_HaveCodeColors()
    {
        var themeService = new ThemeService();
        foreach (var theme in themeService.AvailableThemes)
        {
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeKeyword), $"Theme {theme.Name} is missing CodeKeyword");
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeComment), $"Theme {theme.Name} is missing CodeComment");
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeString), $"Theme {theme.Name} is missing CodeString");
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeNumber), $"Theme {theme.Name} is missing CodeNumber");
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeType), $"Theme {theme.Name} is missing CodeType");
            Assert.IsFalse(string.IsNullOrEmpty(theme.CodeMethod), $"Theme {theme.Name} is missing CodeMethod");
        }
    }

    [TestMethod]
    public void ThemeService_ApplyCodeColorsToHighlighting_ExecutesWithoutError()
    {
        var themeService = new ThemeService();
        var langService = new LanguageService();
        var def = langService.GetHighlightingDefinition("C#");

        Assert.IsNotNull(def);
        themeService.ApplyCodeColorsToHighlighting(def, themeService.CurrentTheme);

        var monokai = themeService.AvailableThemes.First(t => t.Id == "monokai");
        themeService.ApplyCodeColorsToHighlighting(def, monokai);
    }

    [TestMethod]
    public async Task SettingsViewModel_SaveAsync_UpdatesSettings()
    {
        var settingsService = new SettingsService(_tempSettingsFile);
        var themeService = new ThemeService();
        var vm = new SettingsViewModel(settingsService, themeService);

        var monokai = themeService.AvailableThemes.First(t => t.Id == "monokai");
        vm.SelectedTheme = monokai;
        vm.SelectedFont = "JetBrains Mono";
        vm.FontSize = 16.0;
        vm.WordWrap = true;

        await vm.SaveAsync();

        Assert.IsTrue(vm.IsSaved);
        Assert.AreEqual("monokai", settingsService.CurrentSettings.ThemeId);
        Assert.AreEqual("JetBrains Mono", settingsService.CurrentSettings.FontFamily);
        Assert.AreEqual(16.0, settingsService.CurrentSettings.FontSize);
        Assert.IsTrue(settingsService.CurrentSettings.WordWrap);
    }
}
