using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta8FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta8_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void ThemeService_IsValidHexColor_ValidatesCorrectly()
    {
        Assert.IsTrue(ThemeService.IsValidHexColor("#FFF"));
        Assert.IsTrue(ThemeService.IsValidHexColor("fff"));
        Assert.IsTrue(ThemeService.IsValidHexColor("#101010"));
        Assert.IsTrue(ThemeService.IsValidHexColor("101010"));
        Assert.IsTrue(ThemeService.IsValidHexColor("#ABCDEF"));
        Assert.IsTrue(ThemeService.IsValidHexColor("###101010"));

        Assert.IsFalse(ThemeService.IsValidHexColor(null));
        Assert.IsFalse(ThemeService.IsValidHexColor(""));
        Assert.IsFalse(ThemeService.IsValidHexColor("   "));
        Assert.IsFalse(ThemeService.IsValidHexColor("#GG0000"));
        Assert.IsFalse(ThemeService.IsValidHexColor("#12345"));
        Assert.IsFalse(ThemeService.IsValidHexColor("#1234567"));
    }

    [TestMethod]
    public void ThemeService_NormalizeHex_NormalizesCorrectly()
    {
        Assert.AreEqual("#AABBCC", ThemeService.NormalizeHex("#abc"));
        Assert.AreEqual("#FFFFFF", ThemeService.NormalizeHex("fff"));
        Assert.AreEqual("#101010", ThemeService.NormalizeHex("101010"));
        Assert.AreEqual("#007ACC", ThemeService.NormalizeHex("###007ACC"));
    }

    [TestMethod]
    public void ThemeService_ResolveEffectiveTheme_WithPresetAndCustomHex()
    {
        var service = new ThemeService();
        var settings = new AppSettings
        {
            ThemeMode = "dark",
            DarkPresetId = "monokai",
            DarkBackgroundHex = "#1C1C1C",
            DarkForegroundHex = "#F8F8F2",
            DarkAccentHex = "#A6E22E"
        };

        var resolved = service.ResolveEffectiveTheme(settings);
        Assert.IsNotNull(resolved);
        Assert.IsTrue(resolved.IsDark);
        Assert.AreEqual("#1C1C1C", resolved.WindowBackground);
        Assert.AreEqual("#F8F8F2", resolved.Foreground);
        Assert.AreEqual("#A6E22E", resolved.AccentColor);
    }

    [TestMethod]
    public void AppSettings_HighlightWordOccurrences_DefaultsAndClones()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.HighlightWordOccurrences);

        var clone = settings.Clone();
        Assert.IsTrue(clone.HighlightWordOccurrences);

        clone.HighlightWordOccurrences = false;
        var clone2 = clone.Clone();
        Assert.IsFalse(clone2.HighlightWordOccurrences);
    }

    [TestMethod]
    public async Task MainViewModel_SelectTheme_SynchronizesPresetAndColors()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));

        var vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService);

        await vm.InitializeAsync();

        var monokai = vm.AvailableThemes.FirstOrDefault(t => t.Id == "monokai");
        Assert.IsNotNull(monokai);

        vm.SelectTheme(monokai);

        var currentSettings = vm.SettingsService.CurrentSettings;
        Assert.AreEqual("monokai", currentSettings.ThemeId);
        Assert.AreEqual("Dark", currentSettings.ThemeMode);
        Assert.AreEqual("monokai", currentSettings.DarkPresetId);
        Assert.AreEqual(monokai.WindowBackground, currentSettings.DarkBackgroundHex);
        Assert.AreEqual(monokai.Foreground, currentSettings.DarkForegroundHex);
        Assert.AreEqual(monokai.AccentColor, currentSettings.DarkAccentHex);

        // Verify that re-resolving effective theme preserves monokai
        var effective = vm.ThemeService.ResolveEffectiveTheme(currentSettings);
        Assert.AreEqual("monokai", effective.Id);
    }

    [TestMethod]
    public async Task MainViewModel_ReopenClosedTab_RestoresDocument()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));

        var vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService);

        await vm.InitializeAsync();

        // Open first and second docs
        var testFilePath = Path.Combine(_tempDir, "TestFile.cs");
        File.WriteAllText(testFilePath, "class Test { int x = 1; }");
        await vm.OpenFileInternalAsync(testFilePath);

        var doc2Path = Path.Combine(_tempDir, "Doc2.cs");
        File.WriteAllText(doc2Path, "class Doc2 { }");
        await vm.OpenFileInternalAsync(doc2Path);

        Assert.AreEqual(2, vm.Documents.Count);
        var doc1 = vm.Documents.First(d => d.FilePath == testFilePath);

        // Close doc1
        Assert.IsFalse(vm.CanReopenClosedTab);
        await vm.CloseTabCommand.ExecuteAsync(doc1);
        Assert.AreEqual(1, vm.Documents.Count);
        Assert.IsTrue(vm.CanReopenClosedTab);

        // Reopen closed tab
        await vm.ReopenClosedTabCommand.ExecuteAsync(null);
        Assert.AreEqual(2, vm.Documents.Count);
        Assert.AreEqual(testFilePath, vm.ActiveDocument?.FilePath);
        Assert.AreEqual("class Test { int x = 1; }", vm.ActiveDocument?.Model.Text);
        Assert.IsFalse(vm.CanReopenClosedTab);
    }

    [TestMethod]
    public async Task MainViewModel_CopyDocumentRelativePathAsync_CalculatesCorrectPath()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));

        var vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService);

        await vm.InitializeAsync();

        string? copiedText = null;
        vm.RequestSetClipboardText = (text) =>
        {
            copiedText = text;
            return Task.CompletedTask;
        };

        var rootFolder = Path.Combine(_tempDir, "ProjectRoot");
        Directory.CreateDirectory(rootFolder);
        var subDir = Path.Combine(rootFolder, "src", "Sub");
        Directory.CreateDirectory(subDir);
        var subFile = Path.Combine(subDir, "Program.cs");
        File.WriteAllText(subFile, "// code");

        vm.OpenFolder(rootFolder);

        var doc = vm.ActiveDocument!;
        doc.FilePath = subFile;

        await vm.CopyDocumentRelativePathCommand.ExecuteAsync(doc);

        Assert.IsNotNull(copiedText);
        var expected = Path.Combine("src", "Sub", "Program.cs");
        Assert.AreEqual(expected, copiedText);
    }

    [TestMethod]
    public void SettingsViewModel_PresetChange_UpdatesSelectedThemePreviewImmediately()
    {
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var themeService = new ThemeService();
        var vm = new SettingsViewModel(settingsService, themeService);

        vm.ThemeMode = "Dark";
        var monokai = vm.AvailableDarkThemes.FirstOrDefault(t => t.Id == "monokai");
        Assert.IsNotNull(monokai);
        vm.SelectedDarkPreset = monokai;

        Assert.IsNotNull(vm.SelectedTheme);
        Assert.AreEqual("monokai", vm.SelectedTheme.Id);
        Assert.IsTrue(vm.SelectedTheme.IsDark);

        // Change custom background
        vm.DarkBackgroundHex = "#222222";
        Assert.AreEqual("#222222", vm.SelectedTheme.WindowBackground);
        Assert.AreEqual("#222222", vm.SelectedTheme.EditorBackground);
    }

    [TestMethod]
    public void WordOccurrenceRenderer_IsWordChar_IdentifiesCorrectly()
    {
        Assert.IsTrue(WordOccurrenceRenderer.IsWordChar('a'));
        Assert.IsTrue(WordOccurrenceRenderer.IsWordChar('Z'));
        Assert.IsTrue(WordOccurrenceRenderer.IsWordChar('9'));
        Assert.IsTrue(WordOccurrenceRenderer.IsWordChar('_'));

        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar(' '));
        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar('.'));
        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar(';'));
        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar('+'));
        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar('('));
        Assert.IsFalse(WordOccurrenceRenderer.IsWordChar('\n'));
    }
}
