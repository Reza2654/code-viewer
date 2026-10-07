using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta7FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta7_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void FontHelper_DefaultFontName_IsGoogleSansCode()
    {
        Assert.AreEqual("Google Sans Code", FontHelper.DefaultFontName);
    }

    [TestMethod]
    public void FontHelper_GetFontFamily_ResolvesCompositeWithGoogleSansCodeAndFallbacks()
    {
        var ff = FontHelper.GetFontFamily("Google Sans Code");
        Assert.IsNotNull(ff);
        Assert.IsTrue(ff.FamilyNames.Contains("Google Sans Code"));
        Assert.IsTrue(ff.FamilyNames.Contains("Cascadia Code"));
        Assert.IsTrue(ff.FamilyNames.Contains("Consolas"));
        Assert.IsTrue(ff.FamilyNames.Contains("Segoe UI"));
        Assert.IsTrue(ff.FamilyNames.Contains("sans-serif"));
    }

    [TestMethod]
    public void FontHelper_GetFontFamily_WithCustomFont_PreservesCustomFontPrimary()
    {
        var ff = FontHelper.GetFontFamily("Fira Code");
        Assert.IsNotNull(ff);
        Assert.AreEqual("Fira Code", ff.Name);
        Assert.IsTrue(ff.FamilyNames.Contains("Fira Code"));
        Assert.IsTrue(ff.FamilyNames.Contains("Google Sans Code"));
        Assert.IsTrue(ff.FamilyNames.Contains("Cascadia Code"));
    }

    [TestMethod]
    public void AppSettings_DefaultFontFamily_IsGoogleSansCode()
    {
        var settings = new AppSettings();
        Assert.AreEqual("Google Sans Code", settings.FontFamily);
    }

    [TestMethod]
    public void AppConfig_DefaultFontFamily_IsGoogleSansCode()
    {
        var config = new AppConfig();
        Assert.AreEqual("Google Sans Code", config.DefaultFontFamily);
    }

    [TestMethod]
    public void SettingsViewModel_AvailableFonts_IncludesGoogleSansCode()
    {
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var themeService = new ThemeService();

        var vm = new SettingsViewModel(settingsService, themeService);
        Assert.IsTrue(vm.AvailableFonts.Contains("Google Sans Code"));
        Assert.AreEqual("Google Sans Code", vm.SelectedFont);
    }

    [TestMethod]
    public void FontHelper_ApplyToEditor_HandlesNullSafely()
    {
        // Must handle null editor gracefully without throwing
        FontHelper.ApplyToEditor(null, "Google Sans Code", 16.0);
        FontHelper.ApplyToEditor(null, null, 14.0);
    }

    [TestMethod]
    public void FontHelper_GetFontFamily_HandlesNullAndEmptyString()
    {
        var ffNull = FontHelper.GetFontFamily(null);
        Assert.IsNotNull(ffNull);
        Assert.IsTrue(ffNull.FamilyNames.Contains("Google Sans Code"));

        var ffEmpty = FontHelper.GetFontFamily("");
        Assert.IsNotNull(ffEmpty);
        Assert.IsTrue(ffEmpty.FamilyNames.Contains("Google Sans Code"));

        var ffWhitespace = FontHelper.GetFontFamily("   ");
        Assert.IsNotNull(ffWhitespace);
        Assert.IsTrue(ffWhitespace.FamilyNames.Contains("Google Sans Code"));
    }

    [TestMethod]
    public void FontAssets_EmbeddedAndInstallerFontsExist()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        // Search upwards for project root containing src and fonts
        var root = appDir;
        while (!string.IsNullOrEmpty(root) && !File.Exists(Path.Combine(root, "CodeViewer.sln")))
        {
            var parent = Directory.GetParent(root);
            if (parent == null) break;
            root = parent.FullName;
        }

        if (File.Exists(Path.Combine(root, "CodeViewer.sln")))
        {
            var fontDir = Path.Combine(root, "fonts");
            Assert.IsTrue(Directory.Exists(fontDir), "fonts folder should exist");
            var fontFiles = Directory.GetFiles(fontDir, "GoogleSansCode*.ttf");
            Assert.IsTrue(fontFiles.Length >= 4, "Should contain Google Sans Code TTF files");

            var assetDir = Path.Combine(root, "src", "CodeViewer", "Assets", "Fonts");
            Assert.IsTrue(Directory.Exists(assetDir), "src/CodeViewer/Assets/Fonts should exist");
            var assetFiles = Directory.GetFiles(assetDir, "GoogleSansCode*.ttf");
            Assert.IsTrue(assetFiles.Length >= 4, "Should contain Google Sans Code asset TTF files");
        }
    }

    [TestMethod]
    public async Task SettingsService_SavesAndLoadsFontFamilyCorrectly()
    {
        var settingsFile = Path.Combine(_tempDir, "test_settings.json");
        var service = new SettingsService(settingsFile);

        var initial = service.CurrentSettings;
        Assert.AreEqual("Google Sans Code", initial.FontFamily);

        initial.FontFamily = "Consolas";
        await service.SaveAsync(initial);

        var loadedService = new SettingsService(settingsFile);
        Assert.AreEqual("Consolas", loadedService.CurrentSettings.FontFamily);

        loadedService.CurrentSettings.FontFamily = "Google Sans Code";
        await loadedService.SaveAsync(loadedService.CurrentSettings);

        var reloaded = new SettingsService(settingsFile);
        Assert.AreEqual("Google Sans Code", reloaded.CurrentSettings.FontFamily);
    }
}
