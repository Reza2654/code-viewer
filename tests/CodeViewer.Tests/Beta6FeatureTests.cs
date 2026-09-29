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

    [TestMethod]
    public async Task FolderItem_HierarchyAndRefresh_LoadsChildrenWithParentReference()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        var fileA = Path.Combine(subDir, "test.txt");
        File.WriteAllText(fileA, "content");

        var rootItem = new FolderItem(_tempDir, true);
        await rootItem.LoadChildrenAsync();

        Assert.IsTrue(rootItem.Children.Count >= 1);
        var subItem = rootItem.Children.FirstOrDefault(c => c.Name == "sub");
        Assert.IsNotNull(subItem);
        Assert.AreEqual(rootItem, subItem.Parent);

        await subItem.LoadChildrenAsync();
        var childFile = subItem.Children.FirstOrDefault(c => c.Name == "test.txt");
        Assert.IsNotNull(childFile);
        Assert.AreEqual(subItem, childFile.Parent);

        // Add another file and test refresh
        var fileB = Path.Combine(subDir, "another.txt");
        File.WriteAllText(fileB, "more");
        await subItem.RefreshAsync();

        Assert.AreEqual(2, subItem.Children.Count);
        Assert.IsTrue(subItem.Children.Any(c => c.Name == "another.txt"));
    }

    [TestMethod]
    public async Task WorkspaceSearchService_SearchAsync_FindsMatchesInFiles()
    {
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        var f1 = Path.Combine(srcDir, "App.cs");
        File.WriteAllText(f1, "namespace MyApp;\npublic class App {\n    public void Run() { }\n}");
        var f2 = Path.Combine(srcDir, "Helper.cs");
        File.WriteAllText(f2, "namespace MyApp;\npublic static class Helper {\n    public static void Run() { }\n}");

        var searchService = new WorkspaceSearchService();
        var result = await searchService.SearchAsync(_tempDir, "public void Run");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Matches.Count);
        Assert.AreEqual("App.cs", result.Matches[0].FileName);
        Assert.AreEqual(3, result.Matches[0].LineNumber);
        Assert.IsTrue(result.Matches[0].LineContent.Contains("public void Run()"));
    }

    [TestMethod]
    public async Task WorkspaceSearchService_SearchAsync_RespectsCaseAndWholeWord()
    {
        var f = Path.Combine(_tempDir, "Sample.dart");
        File.WriteAllText(f, "void run() {}\nvoid runner() {}\nvoid RUN() {}");

        var searchService = new WorkspaceSearchService();

        // Case-sensitive exact
        var resCase = await searchService.SearchAsync(_tempDir, "RUN", matchCase: true);
        Assert.AreEqual(1, resCase.Matches.Count);
        Assert.AreEqual(3, resCase.Matches[0].LineNumber);

        // Whole word
        var resWholeWord = await searchService.SearchAsync(_tempDir, "run", matchCase: false, wholeWord: true);
        Assert.AreEqual(2, resWholeWord.Matches.Count); // "run" and "RUN" (matches whole word, case insensitive)
        Assert.IsFalse(resWholeWord.Matches.Any(m => m.LineNumber == 2)); // "runner" excluded
    }

    [TestMethod]
    public void ScriptRunnerService_CreateStartInfo_RedirectsStandardInputAndOutput()
    {
        var runner = new ScriptRunnerService();
        var pyFile = Path.Combine(_tempDir, "script.py");
        File.WriteAllText(pyFile, "print('hi')");

        var startInfo = runner.CreateStartInfo(pyFile);
        Assert.IsTrue(startInfo.RedirectStandardInput);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
        Assert.AreEqual(System.Text.Encoding.UTF8, startInfo.StandardInputEncoding);
    }

    [TestMethod]
    public void RunnerViewModel_SendInput_EchosInputAndSendsToService()
    {
        var mockRunner = new TestMockScriptRunnerService();
        var vm = new RunnerViewModel(mockRunner);

        vm.CurrentInput = "hello stdin";
        // When not running, send input should no-op
        vm.SendInput();
        Assert.IsNull(mockRunner.LastInput);

        // Set running state and run
        vm.IsRunning = true;
        vm.CurrentInput = "user_input_data";
        vm.SendInput();

        Assert.AreEqual("user_input_data", mockRunner.LastInput);
        Assert.AreEqual(string.Empty, vm.CurrentInput);
        Assert.IsTrue(vm.OutputText.Contains("> user_input_data"));
    }

    [TestMethod]
    public async Task MainViewModel_FolderOperations_CreateFileAndFolder()
    {
        var mockDialog = new TestConfigurableDialogService();
        var fileService = new FileService();
        var langService = new LanguageService();
        var recentService = new RecentFilesService(10);
        var config = new AppConfig();

        var vm = new MainViewModel(fileService, langService, recentService, mockDialog, config);
        vm.OpenFolder(_tempDir);

        // Test New File
        mockDialog.PromptResponse = "main.dart";
        await vm.CreateFileInFolderAsync(vm.RootFolder);

        var createdFile = Path.Combine(_tempDir, "main.dart");
        Assert.IsTrue(File.Exists(createdFile));
        Assert.AreEqual("main.dart", vm.ActiveDocument?.Title);

        // Test New Folder
        mockDialog.PromptResponse = "assets";
        await vm.CreateFolderInFolderAsync(vm.RootFolder);

        var createdFolder = Path.Combine(_tempDir, "assets");
        Assert.IsTrue(Directory.Exists(createdFolder));
    }

    [TestMethod]
    public async Task MainViewModel_GlobalSearch_SelectSearchResult_OpensDocumentAndJumpsToLine()
    {
        var mockDialog = new TestConfigurableDialogService();
        var fileService = new FileService();
        var langService = new LanguageService();
        var recentService = new RecentFilesService(10);
        var config = new AppConfig();

        var testFile = Path.Combine(_tempDir, "Finder.cs");
        File.WriteAllText(testFile, "line 1\nline 2\ntarget line\nline 4");

        var vm = new MainViewModel(fileService, langService, recentService, mockDialog, config);
        vm.OpenFolder(_tempDir);

        int jumpLine = 0;
        int jumpCol = 0;
        vm.RequestGoToLine = (l, c) =>
        {
            jumpLine = l;
            jumpCol = c;
        };

        vm.GlobalSearchQuery = "target line";
        await vm.ExecuteGlobalSearchAsync();

        Assert.AreEqual(1, vm.GlobalSearchResults.Count);
        var match = vm.GlobalSearchResults[0];
        Assert.AreEqual(3, match.LineNumber);

        await vm.SelectGlobalSearchResultAsync(match);

        Assert.IsFalse(vm.IsGlobalSearchOpen);
        Assert.AreEqual("Finder.cs", vm.ActiveDocument?.Title);
        Assert.AreEqual(3, jumpLine);
    }

    private class TestMockScriptRunnerService : IScriptRunnerService
    {
        public string? LastInput { get; private set; }

        public bool IsSupported(string filePath) => true;

        public Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, System.Threading.CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void SendInput(string text)
        {
            LastInput = text;
        }
    }

    private class TestConfigurableDialogService : IDialogService
    {
        public string? PromptResponse { get; set; }
        public bool ConfirmationResponse { get; set; } = true;

        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(ConfirmResult.Cancel);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(ConfirmationResponse);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "") => Task.FromResult(PromptResponse);
    }
}

