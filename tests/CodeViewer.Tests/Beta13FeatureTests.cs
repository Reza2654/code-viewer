using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta13FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_Beta13Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    #region Recent Workspaces Service Tests

    [TestMethod]
    public async Task RecentWorkspacesService_AddAndRetrieve_ReturnsMRUOrder()
    {
        var jsonPath = Path.Combine(_tempDir, "recent_workspaces.json");
        var service = new RecentWorkspacesService(jsonPath, 5);

        var folderA = Path.Combine(_tempDir, "ProjectA");
        var folderB = Path.Combine(_tempDir, "ProjectB");
        var folderC = Path.Combine(_tempDir, "ProjectC");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        Directory.CreateDirectory(folderC);

        await service.AddRecentWorkspaceAsync(folderA);
        await service.AddRecentWorkspaceAsync(folderB);
        await service.AddRecentWorkspaceAsync(folderC);

        var list = await service.GetRecentWorkspacesAsync();
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual("ProjectC", list[0].FolderName);
        Assert.AreEqual("ProjectB", list[1].FolderName);
        Assert.AreEqual("ProjectA", list[2].FolderName);
        Assert.IsTrue(list[0].Exists);
    }

    [TestMethod]
    public async Task RecentWorkspacesService_Deduplication_MovesExistingToFront()
    {
        var jsonPath = Path.Combine(_tempDir, "recent_workspaces.json");
        var service = new RecentWorkspacesService(jsonPath, 5);

        var folderA = Path.Combine(_tempDir, "ProjectA");
        var folderB = Path.Combine(_tempDir, "ProjectB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        await service.AddRecentWorkspaceAsync(folderA);
        await service.AddRecentWorkspaceAsync(folderB);
        await service.AddRecentWorkspaceAsync(folderA); // Re-add folderA

        var list = await service.GetRecentWorkspacesAsync();
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("ProjectA", list[0].FolderName);
        Assert.AreEqual("ProjectB", list[1].FolderName);
    }

    [TestMethod]
    public async Task RecentWorkspacesService_RemoveAndClear_RemovesEntries()
    {
        var jsonPath = Path.Combine(_tempDir, "recent_workspaces.json");
        var service = new RecentWorkspacesService(jsonPath, 5);

        var folderA = Path.Combine(_tempDir, "ProjectA");
        var folderB = Path.Combine(_tempDir, "ProjectB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        await service.AddRecentWorkspaceAsync(folderA);
        await service.AddRecentWorkspaceAsync(folderB);

        await service.RemoveRecentWorkspaceAsync(folderA);
        var list = await service.GetRecentWorkspacesAsync();
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("ProjectB", list[0].FolderName);

        await service.ClearAsync();
        var emptyList = await service.GetRecentWorkspacesAsync();
        Assert.AreEqual(0, emptyList.Count);
    }

    [TestMethod]
    public async Task RecentWorkspacesService_PersistsToJsonFile()
    {
        var jsonPath = Path.Combine(_tempDir, "recent_workspaces.json");
        var service1 = new RecentWorkspacesService(jsonPath, 5);

        var folderA = Path.Combine(_tempDir, "SavedWorkspace");
        Directory.CreateDirectory(folderA);
        await service1.AddRecentWorkspaceAsync(folderA);

        Assert.IsTrue(File.Exists(jsonPath));

        // Load in a fresh instance
        var service2 = new RecentWorkspacesService(jsonPath, 5);
        var list = await service2.GetRecentWorkspacesAsync();
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("SavedWorkspace", list[0].FolderName);
    }

    #endregion

    #region Rainbow Bracket Colorizer Tests

    [TestMethod]
    public void RainbowBracketColorizer_CycleColors_HasSixDistinctColors()
    {
        var colorizer = new RainbowBracketColorizer();
        Assert.AreEqual(6, RainbowBracketColorizer.RainbowBrushes.Length);
        Assert.IsTrue(colorizer.IsEnabled);

        for (int i = 0; i < RainbowBracketColorizer.RainbowBrushes.Length; i++)
        {
            Assert.IsNotNull(RainbowBracketColorizer.RainbowBrushes[i]);
        }
    }

    [TestMethod]
    public void RainbowBracketColorizer_ToggleEnabled_DisablesColorizer()
    {
        var colorizer = new RainbowBracketColorizer();
        Assert.IsTrue(colorizer.IsEnabled);

        colorizer.IsEnabled = false;
        Assert.IsFalse(colorizer.IsEnabled);
    }

    #endregion

    #region DocumentViewModel Line Endings, Encodings & TabSize Tests

    [TestMethod]
    public void DocumentViewModel_SetLineEnding_CRLF_ConvertsNewlines()
    {
        var langService = new LanguageService();
        var model = DocumentModel.CreateNew("test.cs");
        model.Text = "line1\nline2\nline3";

        var vm = new DocumentViewModel(model, langService);
        vm.SetLineEnding(LineEndingType.CRLF);

        Assert.AreEqual("line1\r\nline2\r\nline3", vm.TextDocument.Text);
        Assert.AreEqual("CRLF", vm.LineEndingDisplay);
        Assert.IsTrue(vm.IsModified);
    }

    [TestMethod]
    public void DocumentViewModel_SetLineEnding_LF_ConvertsNewlines()
    {
        var langService = new LanguageService();
        var model = DocumentModel.CreateNew("test.cs");
        model.Text = "line1\r\nline2\r\nline3";

        var vm = new DocumentViewModel(model, langService);
        vm.SetLineEnding(LineEndingType.LF);

        Assert.AreEqual("line1\nline2\nline3", vm.TextDocument.Text);
        Assert.AreEqual("LF", vm.LineEndingDisplay);
        Assert.IsTrue(vm.IsModified);
    }

    [TestMethod]
    public void DocumentViewModel_SetEncoding_UpdatesEncodingAndDisplayName()
    {
        var langService = new LanguageService();
        var model = DocumentModel.CreateNew("test.txt");
        var vm = new DocumentViewModel(model, langService);

        var utf16 = new FileEncodingInfo(Encoding.Unicode, true);
        vm.SetEncoding(utf16);

        Assert.AreEqual(utf16.DisplayName, vm.EncodingName);
        Assert.IsTrue(vm.IsModified);
    }

    [TestMethod]
    public void DocumentViewModel_TabSize_UpdatesTabSizeDisplay()
    {
        var langService = new LanguageService();
        var model = DocumentModel.CreateNew("test.py");
        var vm = new DocumentViewModel(model, langService);

        Assert.AreEqual(4, vm.TabSize);
        Assert.AreEqual("Spaces: 4", vm.TabSizeDisplay);

        vm.TabSize = 2;
        Assert.AreEqual(2, vm.TabSize);
        Assert.AreEqual("Spaces: 2", vm.TabSizeDisplay);

        vm.TabSize = 8;
        Assert.AreEqual(8, vm.TabSize);
        Assert.AreEqual("Spaces: 8", vm.TabSizeDisplay);
    }

    #endregion

    #region AppSettings Beta 13 Tests

    [TestMethod]
    public void AppSettings_Beta13Properties_DefaultsAndClone()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.AutoCloseBrackets);
        Assert.IsTrue(settings.EnableRainbowBrackets);
        Assert.IsTrue(settings.ShowBreadcrumbs);

        settings.AutoCloseBrackets = false;
        settings.EnableRainbowBrackets = false;
        settings.ShowBreadcrumbs = false;

        var clone = settings.Clone();
        Assert.IsFalse(clone.AutoCloseBrackets);
        Assert.IsFalse(clone.EnableRainbowBrackets);
        Assert.IsFalse(clone.ShowBreadcrumbs);
    }

    #endregion

    #region MainViewModel HasDocuments & Breadcrumbs Tests

    [TestMethod]
    public async Task MainViewModel_HasDocuments_TracksDocumentCount()
    {
        var fileService = new FileService();
        var langService = new LanguageService();
        var recentFilesService = new RecentFilesService();
        var recentWorkspacesService = new RecentWorkspacesService(Path.Combine(_tempDir, "workspaces.json"), 15);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(
            fileService,
            langService,
            recentFilesService,
            dialogService,
            config,
            recentWorkspacesService: recentWorkspacesService);

        Assert.IsFalse(vm.HasDocuments);
        Assert.IsTrue(vm.HasNoDocuments);

        vm.CreateNewDocument();
        Assert.IsTrue(vm.HasDocuments);
        Assert.IsFalse(vm.HasNoDocuments);

        await vm.CloseTabAsync(vm.ActiveDocument);
        Assert.IsFalse(vm.HasDocuments);
        Assert.IsTrue(vm.HasNoDocuments);
    }

    [TestMethod]
    public void MainViewModel_Breadcrumbs_PopulatesForActiveWorkspaceDocument()
    {
        var fileService = new FileService();
        var langService = new LanguageService();
        var recentFilesService = new RecentFilesService();
        var recentWorkspacesService = new RecentWorkspacesService(Path.Combine(_tempDir, "workspaces.json"), 15);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(
            fileService,
            langService,
            recentFilesService,
            dialogService,
            config,
            recentWorkspacesService: recentWorkspacesService);

        var workspacePath = Path.Combine(_tempDir, "WorkspaceRoot");
        var subDir = Path.Combine(workspacePath, "src", "Controllers");
        Directory.CreateDirectory(subDir);
        var testFilePath = Path.Combine(subDir, "HomeController.cs");
        File.WriteAllText(testFilePath, "class HomeController {}");

        vm.OpenFolder(workspacePath);
        vm.CreateNewDocument(); // Untitled

        vm.ActiveDocument!.FilePath = testFilePath;
        vm.UpdateBreadcrumbs();

        Assert.IsTrue(vm.Breadcrumbs.Count >= 3);
        Assert.AreEqual("WorkspaceRoot", vm.Breadcrumbs[0].Name);
        Assert.IsTrue(vm.Breadcrumbs[0].IsFolder);

        var last = vm.Breadcrumbs.Last();
        Assert.AreEqual("HomeController.cs", last.Name);
        Assert.IsFalse(last.IsFolder);
        Assert.IsTrue(last.IsLast);
    }

    [TestMethod]
    public void MainViewModel_InteractiveSelectors_ChangeEncodingAndLineEndings()
    {
        var fileService = new FileService();
        var langService = new LanguageService();
        var recentFilesService = new RecentFilesService();
        var recentWorkspacesService = new RecentWorkspacesService(Path.Combine(_tempDir, "workspaces.json"), 15);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(
            fileService,
            langService,
            recentFilesService,
            dialogService,
            config,
            recentWorkspacesService: recentWorkspacesService);

        vm.CreateNewDocument();
        vm.ActiveDocument!.TextDocument.Text = "hello\nworld";

        // Change Line Endings
        vm.ChangeLineEnding("CRLF");
        Assert.AreEqual(LineEndingType.CRLF, vm.ActiveDocument.Model.LineEnding);
        Assert.AreEqual("CRLF", vm.ActiveDocument.LineEndingDisplay);

        vm.ToggleLineEnding();
        Assert.AreEqual(LineEndingType.LF, vm.ActiveDocument.Model.LineEnding);
        Assert.AreEqual("LF", vm.ActiveDocument.LineEndingDisplay);

        // Change Encoding
        vm.ChangeEncoding("UTF-8 with BOM");
        Assert.IsTrue(vm.ActiveDocument.Model.EncodingInfo.HasBom);

        // Tab Size
        vm.SetTabSize("2");
        Assert.AreEqual(2, vm.ActiveDocument.TabSize);
        Assert.AreEqual("Spaces: 2", vm.ActiveDocument.TabSizeDisplay);
    }

    #endregion
}
