using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class WorkspaceCoordinatorTests
{
    private class FakeDialogService : IDialogService
    {
        public string? PromptResult { get; set; }
        public bool ConfirmationResult { get; set; } = true;
        public string? OpenFolderResult { get; set; }

        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult(OpenFolderResult);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(ConfirmResult.Cancel);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(ConfirmationResult);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "") => Task.FromResult(PromptResult);
        public Task ShowUpdateDialogAsync(AppUpdateInfo updateInfo, IAppUpdateService updateService) => Task.CompletedTask;
    }

    private class FakeRecentWorkspacesService : IRecentWorkspacesService
    {
        private readonly List<RecentWorkspaceItem> _items = [];

        public Task<IReadOnlyList<RecentWorkspaceItem>> GetRecentWorkspacesAsync() =>
            Task.FromResult<IReadOnlyList<RecentWorkspaceItem>>(_items.ToList());

        public Task AddRecentWorkspaceAsync(string folderPath)
        {
            _items.RemoveAll(i => string.Equals(i.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
            _items.Insert(0, new RecentWorkspaceItem { FolderPath = folderPath });
            return Task.CompletedTask;
        }

        public Task RemoveRecentWorkspaceAsync(string folderPath)
        {
            _items.RemoveAll(i => string.Equals(i.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _items.Clear();
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public void ToggleSidebar_FlipsSidebarState()
    {
        var coordinator = new WorkspaceCoordinator(new FakeDialogService(), new FakeRecentWorkspacesService());
        Assert.IsTrue(coordinator.IsSidebarOpen);

        coordinator.ToggleSidebar();
        Assert.IsFalse(coordinator.IsSidebarOpen);

        coordinator.ToggleSidebar();
        Assert.IsTrue(coordinator.IsSidebarOpen);
    }

    [TestMethod]
    public void OpenFolder_InitializesRootFolderAndTriggersEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var coordinator = new WorkspaceCoordinator(new FakeDialogService(), new FakeRecentWorkspacesService());
            var workspaceChangedCalled = false;
            string? statusReported = null;

            coordinator.WorkspaceChanged = () => workspaceChangedCalled = true;
            coordinator.RequestStatusMessage = msg => { statusReported = msg; return Task.CompletedTask; };

            coordinator.OpenFolder(tempDir);

            Assert.IsNotNull(coordinator.RootFolder);
            Assert.AreEqual(tempDir, coordinator.RootFolder.FullPath);
            Assert.IsTrue(coordinator.RootFolder.IsExpanded);
            Assert.IsTrue(coordinator.IsSidebarOpen);
            Assert.IsTrue(workspaceChangedCalled);
            Assert.IsNotNull(statusReported);
            StringAssert.Contains(statusReported, "Opened workspace");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public void CloseFolder_ResetsRootFolderAndNotifies()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var coordinator = new WorkspaceCoordinator(new FakeDialogService(), new FakeRecentWorkspacesService());
            coordinator.OpenFolder(tempDir);
            Assert.IsNotNull(coordinator.RootFolder);

            var changedCount = 0;
            coordinator.WorkspaceChanged = () => changedCount++;

            coordinator.CloseFolder();

            Assert.IsNull(coordinator.RootFolder);
            Assert.AreEqual(1, changedCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public async Task SelectFolderItemAsync_OpenFileForFile_TogglesFolderForDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "test.txt");
        File.WriteAllText(testFile, "hello");

        try
        {
            var coordinator = new WorkspaceCoordinator(new FakeDialogService(), new FakeRecentWorkspacesService());
            string? openedPath = null;
            coordinator.RequestOpenFile = path => { openedPath = path; return Task.CompletedTask; };

            var fileItem = new FolderItem(testFile, isDirectory: false);
            await coordinator.SelectFolderItemAsync(fileItem);

            Assert.AreEqual(fileItem, coordinator.SelectedFolderItem);
            Assert.AreEqual(testFile, openedPath);

            var dirItem = new FolderItem(tempDir, isDirectory: true) { IsExpanded = false };
            await coordinator.SelectFolderItemAsync(dirItem);
            Assert.IsTrue(dirItem.IsExpanded);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public async Task CreateFileAndFolder_OperatesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dialogService = new FakeDialogService();
        var coordinator = new WorkspaceCoordinator(dialogService, new FakeRecentWorkspacesService());
        coordinator.OpenFolder(tempDir);

        try
        {
            string? openedFile = null;
            coordinator.RequestOpenFile = path => { openedFile = path; return Task.CompletedTask; };

            // 1. Create file
            dialogService.PromptResult = "new_script.py";
            await coordinator.CreateFileInFolderAsync(null);

            var expectedFilePath = Path.Combine(tempDir, "new_script.py");
            Assert.IsTrue(File.Exists(expectedFilePath));
            Assert.AreEqual(expectedFilePath, openedFile);

            // 2. Create folder
            dialogService.PromptResult = "sub_modules";
            await coordinator.CreateFolderInFolderAsync(null);

            var expectedDirPath = Path.Combine(tempDir, "sub_modules");
            Assert.IsTrue(Directory.Exists(expectedDirPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public async Task DeleteFolderItemAsync_DeletesAndInvokesCloseCallback()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "to_delete.txt");
        File.WriteAllText(testFile, "content");

        try
        {
            var dialogService = new FakeDialogService { ConfirmationResult = true };
            var coordinator = new WorkspaceCoordinator(dialogService, new FakeRecentWorkspacesService());
            coordinator.OpenFolder(tempDir);

            string? closedFile = null;
            coordinator.RequestCloseFile = path => { closedFile = path; return Task.CompletedTask; };

            var item = new FolderItem(testFile, isDirectory: false);
            await coordinator.DeleteFolderItemAsync(item);

            Assert.IsFalse(File.Exists(testFile));
            Assert.AreEqual(testFile, closedFile);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
