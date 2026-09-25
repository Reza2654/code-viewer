using System;
using System.Collections.Generic;
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
public class FastLightweightEditorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_FastEditor_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void CommandLineParser_WindowsDriveLetter_DoesNotSplitDriveAsLineNumber()
    {
        // Path with drive letter and line number: C:\path\file.cs:42
        var parsed = CommandLineParser.ParseArgument(@"C:\Projects\MyApp\Program.cs:42");

        Assert.AreEqual(42, parsed.Line);
        Assert.AreEqual(1, parsed.Column);
        Assert.IsTrue(parsed.FilePath.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(parsed.FilePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CommandLineParser_WindowsDriveLetter_WithLineAndColumn()
    {
        // Path with drive letter, line and column: D:\folder\sub\code.cs:105:14
        var parsed = CommandLineParser.ParseArgument(@"D:\folder\sub\code.cs:105:14");

        Assert.AreEqual(105, parsed.Line);
        Assert.AreEqual(14, parsed.Column);
        Assert.IsTrue(parsed.FilePath.StartsWith("D:", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(parsed.FilePath.EndsWith("code.cs", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CommandLineParser_PlainFilePath_DefaultsToOne()
    {
        var parsed = CommandLineParser.ParseArgument(@"E:\data\documents\readme.md");

        Assert.AreEqual(1, parsed.Line);
        Assert.AreEqual(1, parsed.Column);
        Assert.IsTrue(parsed.FilePath.EndsWith("readme.md", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CommandLineParser_ExistingFile_ResolvedDirectly()
    {
        var filePath = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(filePath, "Hello World");

        var parsed = CommandLineParser.ParseArgument(filePath);

        Assert.AreEqual(Path.GetFullPath(filePath), parsed.FilePath);
        Assert.AreEqual(1, parsed.Line);
        Assert.AreEqual(1, parsed.Column);
    }

    [TestMethod]
    public void CommandLineParser_ParseArguments_ProcessesMultipleArgs()
    {
        var args = new[]
        {
            @"C:\app\Main.cs:50",
            @"D:\app\Utils.cs:12:4",
            "   ",
            @"readme.txt"
        };

        var results = CommandLineParser.ParseArguments(args).ToList();

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(50, results[0].Line);
        Assert.AreEqual(12, results[1].Line);
        Assert.AreEqual(4, results[1].Column);
        Assert.AreEqual(1, results[2].Line);
    }

    [TestMethod]
    public void LanguageService_GetCommentPrefix_ReturnsExpectedPrefix()
    {
        var service = new LanguageService();

        Assert.AreEqual("// ", service.GetCommentPrefix("C#"));
        Assert.AreEqual("// ", service.GetCommentPrefix("JavaScript"));
        Assert.AreEqual("// ", service.GetCommentPrefix("TypeScript"));
        Assert.AreEqual("// ", service.GetCommentPrefix("C++"));
        Assert.AreEqual("// ", service.GetCommentPrefix("Java"));
        Assert.AreEqual("// ", service.GetCommentPrefix("Rust"));
        Assert.AreEqual("# ", service.GetCommentPrefix("Python"));
        Assert.AreEqual("# ", service.GetCommentPrefix("Ruby"));
        Assert.AreEqual("-- ", service.GetCommentPrefix("SQL"));
        Assert.AreEqual("-- ", service.GetCommentPrefix("Lua"));
        Assert.IsNull(service.GetCommentPrefix("HTML"));
        Assert.IsNull(service.GetCommentPrefix("Plain Text"));
    }

    [TestMethod]
    public void QuickOpenItem_PropertiesAndAliases_MatchExpectedValues()
    {
        var item = new QuickOpenItem
        {
            Title = "Program.cs",
            Subtitle = @"src\Program.cs",
            FilePath = @"C:\MyApp\src\Program.cs",
            IsOpenTab = true,
            IsModified = true
        };

        Assert.AreEqual("Program.cs", item.DisplayName);
        Assert.AreEqual(@"src\Program.cs", item.RelativeOrFullPath);
        Assert.AreEqual("Open *", item.BadgeText);
        Assert.AreEqual("Open *", item.TabBadge);
    }

    [TestMethod]
    public void DocumentViewModel_CaretDisplay_ShowsSelectionDetails()
    {
        var model = DocumentModel.CreateNew("test.cs");
        var doc = new DocumentViewModel(model, new LanguageService());

        // Caret only
        doc.UpdateCaretPosition(10, 5);
        Assert.AreEqual("Ln 10, Col 5", doc.CaretDisplay);

        // Single line selection
        doc.UpdateCaretPosition(10, 20, selectionLength: 15, selectedLineCount: 1);
        Assert.AreEqual("Ln 10, Col 20 (15 selected)", doc.CaretDisplay);

        // Multi-line selection
        doc.UpdateCaretPosition(14, 5, selectionLength: 120, selectedLineCount: 4);
        Assert.AreEqual("Ln 14, Col 5 (4 lines, 120 chars selected)", doc.CaretDisplay);
    }

    [TestMethod]
    public void SearchViewModel_ReplaceCommands_TriggerEvents()
    {
        var vm = new SearchViewModel
        {
            SearchText = "foo",
            ReplaceText = "bar"
        };

        var replaceCalled = false;
        var replaceAllCalled = false;

        vm.RequestReplace += () => replaceCalled = true;
        vm.RequestReplaceAll += () => replaceAllCalled = true;

        vm.Replace();
        Assert.IsTrue(replaceCalled);

        vm.ReplaceAll();
        Assert.IsTrue(replaceAllCalled);
    }

    [TestMethod]
    public async Task TabManagement_CloseOtherTabs_ClosesAllExceptActive()
    {
        var vm = CreateTestMainViewModel();

        var p1 = Path.Combine(_tempDir, "file1.cs");
        var p2 = Path.Combine(_tempDir, "file2.cs");
        var p3 = Path.Combine(_tempDir, "file3.cs");
        File.WriteAllText(p1, "// 1");
        File.WriteAllText(p2, "// 2");
        File.WriteAllText(p3, "// 3");

        await vm.OpenFileInternalAsync(p1);
        await vm.OpenFileInternalAsync(p2);
        await vm.OpenFileInternalAsync(p3);

        Assert.AreEqual(3, vm.Documents.Count);
        var keepDoc = vm.Documents[1]; // file2.cs

        await vm.CloseOtherTabsCommand.ExecuteAsync(keepDoc);

        Assert.AreEqual(1, vm.Documents.Count);
        Assert.AreEqual(keepDoc, vm.Documents[0]);
    }

    [TestMethod]
    public async Task TabManagement_CloseTabsToTheRight_ClosesSubsequentTabs()
    {
        var vm = CreateTestMainViewModel();

        var p1 = Path.Combine(_tempDir, "tab1.cs");
        var p2 = Path.Combine(_tempDir, "tab2.cs");
        var p3 = Path.Combine(_tempDir, "tab3.cs");
        var p4 = Path.Combine(_tempDir, "tab4.cs");
        File.WriteAllText(p1, "// 1");
        File.WriteAllText(p2, "// 2");
        File.WriteAllText(p3, "// 3");
        File.WriteAllText(p4, "// 4");

        await vm.OpenFileInternalAsync(p1);
        await vm.OpenFileInternalAsync(p2);
        await vm.OpenFileInternalAsync(p3);
        await vm.OpenFileInternalAsync(p4);

        Assert.AreEqual(4, vm.Documents.Count);
        var middleDoc = vm.Documents[1]; // tab2.cs

        await vm.CloseTabsToTheRightCommand.ExecuteAsync(middleDoc);

        Assert.AreEqual(2, vm.Documents.Count);
        Assert.AreEqual(p1, vm.Documents[0].FilePath);
        Assert.AreEqual(p2, vm.Documents[1].FilePath);
    }

    [TestMethod]
    public async Task TabManagement_CloseSavedTabs_PreservesUnsavedChanges()
    {
        var vm = CreateTestMainViewModel();

        var p1 = Path.Combine(_tempDir, "saved1.cs");
        var p2 = Path.Combine(_tempDir, "unsaved.cs");
        var p3 = Path.Combine(_tempDir, "saved2.cs");
        File.WriteAllText(p1, "// 1");
        File.WriteAllText(p2, "// 2");
        File.WriteAllText(p3, "// 3");

        await vm.OpenFileInternalAsync(p1);
        await vm.OpenFileInternalAsync(p2);
        await vm.OpenFileInternalAsync(p3);

        // Modify tab 2
        vm.Documents[1].IsModified = true;
        Assert.IsTrue(vm.Documents[1].IsModified);

        await vm.CloseSavedTabsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vm.Documents.Count);
        Assert.AreEqual(p2, vm.Documents[0].FilePath);
    }

    private MainViewModel CreateTestMainViewModel()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new MockDialogService();
        var config = new AppConfig();
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));
        var fileWatcherService = new FileWatcherService();

        return new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService,
            fileWatcherService: fileWatcherService);
    }

    private class MockDialogService : IDialogService
    {
        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(ConfirmResult.Cancel);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(false);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    }
}
