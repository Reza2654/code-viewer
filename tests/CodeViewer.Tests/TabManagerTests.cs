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
public class TabManagerTests
{
    private class FakeDialogService : IDialogService
    {
        public ConfirmResult SaveConfirmationResult { get; set; } = ConfirmResult.DontSave;
        public string? SaveFileDialogResult { get; set; }

        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult(SaveFileDialogResult);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(SaveConfirmationResult);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(false);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
        public Task ShowUpdateDialogAsync(AppUpdateInfo updateInfo, IAppUpdateService updateService) => Task.CompletedTask;
    }

    private static TabManager CreateTabManager(IDialogService? dialogService = null)
    {
        return new TabManager(
            new FileService(),
            new LanguageService(),
            dialogService ?? new FakeDialogService(),
            new ThemeService(),
            new SettingsService(),
            new FileWatcherService(),
            new RecentFilesService(10),
            new AppConfig());
    }

    [TestMethod]
    public void CreateNewDocument_AddsDocumentAndSetsActive()
    {
        using var manager = CreateTabManager();
        Assert.IsFalse(manager.HasDocuments);
        Assert.IsTrue(manager.HasNoDocuments);

        manager.CreateNewDocument();

        Assert.IsTrue(manager.HasDocuments);
        Assert.IsFalse(manager.HasNoDocuments);
        Assert.AreEqual(1, manager.Documents.Count);
        Assert.IsNotNull(manager.ActiveDocument);
        Assert.IsTrue(manager.ActiveDocument.IsActive);
        Assert.AreEqual("Untitled-1", manager.ActiveDocument.Title);
    }

    [TestMethod]
    public async Task CloseTabAsync_RemovesTab_AndTracksRecentlyClosed()
    {
        using var manager = CreateTabManager();
        manager.CreateNewDocument();
        manager.CreateNewDocument();
        Assert.AreEqual(2, manager.Documents.Count);

        var first = manager.Documents[0];
        first.TextDocument.Text = "console.log('test');";

        await manager.CloseTabAsync(first);

        Assert.AreEqual(1, manager.Documents.Count);
        Assert.IsTrue(manager.CanReopenClosedTab);
        Assert.AreEqual(1, manager.RecentlyClosedTabs.Count);
    }

    [TestMethod]
    public async Task ReopenClosedTabAsync_RestoresContentAndTab()
    {
        using var manager = CreateTabManager();
        manager.CreateNewDocument();
        var doc = manager.ActiveDocument!;
        doc.TextDocument.Text = "important script";
        doc.Title = "Script-1";

        await manager.CloseTabAsync(doc);
        Assert.AreEqual(0, manager.Documents.Count);
        Assert.IsTrue(manager.CanReopenClosedTab);

        await manager.ReopenClosedTabAsync();

        Assert.AreEqual(1, manager.Documents.Count);
        Assert.AreEqual("important script", manager.ActiveDocument?.TextDocument.Text);
        Assert.IsFalse(manager.CanReopenClosedTab);
    }

    [TestMethod]
    public async Task CloseOtherTabsAsync_KeepsOnlyActiveTab()
    {
        using var manager = CreateTabManager();
        manager.CreateNewDocument();
        manager.CreateNewDocument();
        manager.CreateNewDocument();
        Assert.AreEqual(3, manager.Documents.Count);

        var middleDoc = manager.Documents[1];
        manager.ActiveDocument = middleDoc;

        await manager.CloseOtherTabsAsync(null);

        Assert.AreEqual(1, manager.Documents.Count);
        Assert.AreEqual(middleDoc, manager.ActiveDocument);
    }

    [TestMethod]
    public async Task CloseTabsToTheRightAsync_ClosesOnlyTrailingTabs()
    {
        using var manager = CreateTabManager();
        manager.CreateNewDocument(); // index 0
        manager.CreateNewDocument(); // index 1
        manager.CreateNewDocument(); // index 2
        Assert.AreEqual(3, manager.Documents.Count);

        var targetDoc = manager.Documents[1];

        await manager.CloseTabsToTheRightAsync(targetDoc);

        Assert.AreEqual(2, manager.Documents.Count);
        Assert.IsTrue(manager.Documents.Contains(targetDoc));
    }

    [TestMethod]
    public async Task CloseSavedTabsAsync_ClosesOnlyUnmodifiedTabs()
    {
        using var manager = CreateTabManager();
        manager.CreateNewDocument(); // unmodified
        manager.CreateNewDocument(); // will modify
        manager.Documents[1].IsModified = true;

        Assert.AreEqual(2, manager.Documents.Count);

        await manager.CloseSavedTabsAsync();

        Assert.AreEqual(1, manager.Documents.Count);
        Assert.IsTrue(manager.Documents[0].IsModified);
    }

    [TestMethod]
    public async Task CloseTabAsync_WithUnsavedChanges_CancelsWhenDialogCancelled()
    {
        var fakeDialog = new FakeDialogService { SaveConfirmationResult = ConfirmResult.Cancel };
        using var manager = CreateTabManager(fakeDialog);

        manager.CreateNewDocument();
        manager.ActiveDocument!.IsModified = true;

        await manager.CloseTabAsync(manager.ActiveDocument);

        // Tab should still exist because user canceled
        Assert.AreEqual(1, manager.Documents.Count);
    }
}
