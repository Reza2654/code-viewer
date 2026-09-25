using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta6FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta6_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void LanguageService_GetSupportedLanguages_ReturnsExpectedLanguages()
    {
        var service = new LanguageService();
        var langs = service.GetSupportedLanguages();

        Assert.IsNotNull(langs);
        Assert.IsTrue(langs.Count > 10);
        Assert.IsTrue(langs.Contains("C#"));
        Assert.IsTrue(langs.Contains("Dart"));
        Assert.IsTrue(langs.Contains("Python"));
        Assert.IsTrue(langs.Contains("Plain Text"));
        Assert.IsTrue(langs.Contains("JSON"));
    }

    [TestMethod]
    public void MainViewModel_SetLanguage_UpdatesActiveDocumentLanguageAndSyntax()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);
        vm.CreateNewDocument();

        Assert.AreEqual("Plain Text", vm.ActiveDocument!.Language);

        vm.SetLanguage("Python");
        Assert.AreEqual("Python", vm.ActiveDocument.Language);
        Assert.IsNotNull(vm.ActiveDocument.HighlightingDefinition);

        vm.SetLanguage("C#");
        Assert.AreEqual("C#", vm.ActiveDocument.Language);
        Assert.IsNotNull(vm.ActiveDocument.HighlightingDefinition);
    }

    [TestMethod]
    public async Task MainViewModel_CopyAllAsync_InvokesClipboardCallback()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);
        vm.CreateNewDocument();
        vm.ActiveDocument!.TextDocument.Text = "Hello Beta 6!";

        string? copiedText = null;
        vm.RequestSetClipboardText = (txt) =>
        {
            copiedText = txt;
            return Task.CompletedTask;
        };

        await vm.CopyAllCommand.ExecuteAsync(null);

        Assert.AreEqual("Hello Beta 6!", copiedText);
    }

    [TestMethod]
    public async Task SessionService_SaveAndLoad_RoundtripsSuccessfully()
    {
        var sessionFile = Path.Combine(_tempDir, "session.json");
        var service = new SessionService(sessionFile);

        var file1 = Path.Combine(_tempDir, "file1.cs");
        var file2 = Path.Combine(_tempDir, "file2.py");
        File.WriteAllText(file1, "// c#");
        File.WriteAllText(file2, "# python");

        await service.SaveSessionAsync(new[] { file1, file2 }, file2);

        var loaded = await service.LoadSessionAsync();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, loaded.OpenFiles.Count);
        Assert.AreEqual(file1, loaded.OpenFiles[0]);
        Assert.AreEqual(file2, loaded.OpenFiles[1]);
        Assert.AreEqual(file2, loaded.ActiveFile);

        await service.ClearSessionAsync();
        var cleared = await service.LoadSessionAsync();
        Assert.IsNull(cleared);
    }

    [TestMethod]
    public async Task MainViewModel_InitializeAsync_RestoresPreviousSessionWhenEnabled()
    {
        var sessionFile = Path.Combine(_tempDir, "session.json");
        var sessionService = new SessionService(sessionFile);

        var file1 = Path.Combine(_tempDir, "session_test.cs");
        File.WriteAllText(file1, "public class SessionTest {}");
        await sessionService.SaveSessionAsync(new[] { file1 }, file1);

        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsFile);

        var settings = settingsService.CurrentSettings;
        settings.RestorePreviousSession = true;
        await settingsService.SaveAsync(settings);

        var config = new AppConfig();
        var vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService);

        await vm.InitializeAsync();

        Assert.AreEqual(1, vm.Documents.Count);
        Assert.AreEqual("session_test.cs", vm.ActiveDocument?.Title);
        Assert.AreEqual(file1, vm.ActiveDocument?.FilePath);
    }

    [TestMethod]
    public void FileWatcherService_WatchAndIgnore_DoesNotThrow()
    {
        using var watcher = new FileWatcherService();
        var testFile = Path.Combine(_tempDir, "watch_test.txt");
        File.WriteAllText(testFile, "hello");

        watcher.WatchFile(testFile);
        watcher.TemporarilyIgnore(testFile, 500);
        watcher.UnwatchFile(testFile);
        watcher.UnwatchAll();
    }
}
