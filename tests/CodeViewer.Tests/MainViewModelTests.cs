using System;
using System.IO;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class MainViewModelTests
{
    private MainViewModel _vm = null!;
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_VM_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig
        {
            DefaultFontSize = 14.0,
            MinFontSize = 8.0,
            MaxFontSize = 36.0,
            WordWrap = false,
            ShowLineNumbers = true
        };
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings.json"));
        var sessionService = new SessionService(Path.Combine(_tempDir, "session.json"));

        _vm = new MainViewModel(
            fileService,
            languageService,
            recentFilesService,
            dialogService,
            config,
            settingsService: settingsService,
            sessionService: sessionService);
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
    public async Task InitializeAsync_NoArgs_OpensDefaultUntitledDocument()
    {
        await _vm.InitializeAsync();

        Assert.AreEqual(1, _vm.Documents.Count);
        Assert.IsNotNull(_vm.ActiveDocument);
        Assert.AreEqual("Untitled-1", _vm.ActiveDocument.Title);
        Assert.IsTrue(_vm.ActiveDocument.Model.IsNewFile);
    }

    [TestMethod]
    public async Task InitializeAsync_WithFilePath_OpensFileDirectly()
    {
        var testFile = Path.Combine(_tempDir, "sample.cs");
        File.WriteAllText(testFile, "public class Hello {}");

        await _vm.InitializeAsync([testFile]);

        Assert.AreEqual(1, _vm.Documents.Count);
        Assert.IsNotNull(_vm.ActiveDocument);
        Assert.AreEqual("sample.cs", _vm.ActiveDocument.Title);
        Assert.AreEqual("C#", _vm.ActiveDocument.Language);
        Assert.AreEqual("public class Hello {}", _vm.ActiveDocument.Model.Text);
    }

    [TestMethod]
    public void CreateNewDocument_IncrementsUntitledCounter()
    {
        _vm.CreateNewDocument();
        _vm.CreateNewDocument();

        Assert.AreEqual(2, _vm.Documents.Count);
        Assert.AreEqual("Untitled-1", _vm.Documents[0].Title);
        Assert.AreEqual("Untitled-2", _vm.Documents[1].Title);
        Assert.AreEqual(_vm.Documents[1], _vm.ActiveDocument);
    }

    [TestMethod]
    public void ZoomCommands_AdjustFontSizeWithinBounds()
    {
        _vm.CreateNewDocument();
        var initialSize = _vm.ActiveDocument!.FontSize;

        _vm.ZoomIn();
        Assert.IsTrue(_vm.ActiveDocument.FontSize > initialSize);

        _vm.ResetZoom();
        Assert.AreEqual(initialSize, _vm.ActiveDocument.FontSize);

        _vm.ZoomOut();
        Assert.IsTrue(_vm.ActiveDocument.FontSize < initialSize);
    }

    [TestMethod]
    public void ToggleCommands_InvertSettings()
    {
        _vm.CreateNewDocument();
        var initialWrap = _vm.ActiveDocument!.WordWrap;
        var initialLineNumbers = _vm.ActiveDocument.ShowLineNumbers;

        _vm.ToggleWordWrap();
        Assert.AreNotEqual(initialWrap, _vm.ActiveDocument.WordWrap);

        _vm.ToggleLineNumbers();
        Assert.AreNotEqual(initialLineNumbers, _vm.ActiveDocument.ShowLineNumbers);
    }

    [TestMethod]
    public async Task RecentFiles_LoadAndClear_UpdatesHasRecentFiles()
    {
        var testFile = Path.Combine(_tempDir, "recent_test.cs");
        File.WriteAllText(testFile, "// test");

        await _vm.OpenFileInternalAsync(testFile);
        Assert.IsTrue(_vm.HasRecentFiles);
        Assert.IsTrue(_vm.RecentFiles.Count > 0);
        Assert.IsNotNull(_vm.RecentFiles[0].OpenCommand);

        await _vm.ClearRecentFilesAsync();
        Assert.IsFalse(_vm.HasRecentFiles);
        Assert.AreEqual(0, _vm.RecentFiles.Count);
    }
}
