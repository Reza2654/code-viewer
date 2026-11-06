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

    private class FakeGitService : IGitService
    {
        public string? RootToReturn { get; set; }
        public Dictionary<string, GitFileStatus> StatusesToReturn { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> IsGitInstalledAsync() => Task.FromResult(true);

        public Task<string?> FindGitRootAsync(string directoryPath, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(RootToReturn);

        public Task<Dictionary<string, GitFileStatus>> GetStatusAsync(string workingDirectory, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(StatusesToReturn);
    }

    [TestMethod]
    public async Task RefreshGitStatusAsync_AppliesStatusesAndAggregatesToParentFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_WsTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(subDir, "file1.cs");
        var file2 = Path.Combine(subDir, "file2.cs");
        var file3 = Path.Combine(tempDir, "file3.md");
        File.WriteAllText(file1, "1");
        File.WriteAllText(file2, "2");
        File.WriteAllText(file3, "3");

        try
        {
            var fakeGit = new FakeGitService
            {
                RootToReturn = tempDir,
                StatusesToReturn = new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase)
                {
                    [file1] = GitFileStatus.Modified,
                    [file3] = GitFileStatus.Untracked
                }
            };

            var coordinator = new WorkspaceCoordinator(new FakeDialogService(), new FakeRecentWorkspacesService(), fakeGit);
            coordinator.OpenFolder(tempDir);

            // Wait for refresh to complete
            await coordinator.RefreshGitStatusAsync();

            var root = coordinator.RootFolder!;
            Assert.IsNotNull(root);
            Assert.AreEqual(tempDir, coordinator.GitRepoRoot);
            Assert.AreEqual(2, coordinator.CachedGitStatuses.Count);

            // Check file node statuses
            var subFolderItem = new FolderItem(subDir, isDirectory: true);
            var item1 = new FolderItem(file1, isDirectory: false);
            var item2 = new FolderItem(file2, isDirectory: false);
            var item3 = new FolderItem(file3, isDirectory: false);

            subFolderItem.Children.Clear();
            subFolderItem.Children.Add(item1);
            subFolderItem.Children.Add(item2);

            root.Children.Clear();
            root.Children.Add(subFolderItem);
            root.Children.Add(item3);

            coordinator.ApplyGitStatusToSubtree(root);

            Assert.AreEqual(GitFileStatus.Modified, item1.GitStatus);
            Assert.AreEqual("M", item1.GitStatusLetter);
            Assert.IsTrue(item1.HasGitStatus);
            Assert.IsNotNull(item1.GitStatusBrush);

            Assert.AreEqual(GitFileStatus.Clean, item2.GitStatus);
            Assert.AreEqual(string.Empty, item2.GitStatusLetter);
            Assert.IsFalse(item2.HasGitStatus);

            Assert.AreEqual(GitFileStatus.Untracked, item3.GitStatus);
            Assert.AreEqual("U", item3.GitStatusLetter);
            Assert.IsTrue(item3.HasGitStatus);

            // Subfolder should have HasModifiedChild = true because file1 is modified
            Assert.IsTrue(subFolderItem.HasModifiedChild);
            Assert.AreEqual("●", subFolderItem.GitStatusLetter);
            Assert.IsTrue(subFolderItem.HasGitStatus);

            // Clear git status should reset everything
            coordinator.ClearGitStatusSubtree(root);
            Assert.AreEqual(GitFileStatus.Clean, item1.GitStatus);
            Assert.IsFalse(subFolderItem.HasModifiedChild);
            Assert.IsFalse(subFolderItem.HasGitStatus);
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
    public void FolderItem_GitStatusProperties_ReflectAllStatusesCorrectly()
    {
        var item = new FolderItem("test.cs", isDirectory: false);

        item.GitStatus = GitFileStatus.Clean;
        Assert.AreEqual(string.Empty, item.GitStatusLetter);
        Assert.IsFalse(item.HasGitStatus);

        item.GitStatus = GitFileStatus.Modified;
        Assert.AreEqual("M", item.GitStatusLetter);
        Assert.AreEqual("#E2C08D", item.GitStatusColor);
        Assert.AreEqual("Modified", item.GitStatusToolTip);
        Assert.IsTrue(item.HasGitStatus);
        Assert.IsNotNull(item.GitStatusBrush);

        item.GitStatus = GitFileStatus.Untracked;
        Assert.AreEqual("U", item.GitStatusLetter);
        Assert.AreEqual("#73C991", item.GitStatusColor);
        Assert.AreEqual("Untracked", item.GitStatusToolTip);

        item.GitStatus = GitFileStatus.Added;
        Assert.AreEqual("A", item.GitStatusLetter);
        Assert.AreEqual("#A3BE8C", item.GitStatusColor);

        item.GitStatus = GitFileStatus.Deleted;
        Assert.AreEqual("D", item.GitStatusLetter);
        Assert.AreEqual("#E06C75", item.GitStatusColor);

        item.GitStatus = GitFileStatus.Renamed;
        Assert.AreEqual("R", item.GitStatusLetter);
        Assert.AreEqual("#61AFEF", item.GitStatusColor);

        item.GitStatus = GitFileStatus.Conflicted;
        Assert.AreEqual("!", item.GitStatusLetter);
        Assert.AreEqual("#E5C07B", item.GitStatusColor);

        // Parent folder with clean status but HasModifiedChild = true
        var folder = new FolderItem("folder", isDirectory: true);
        folder.HasModifiedChild = true;
        Assert.AreEqual("●", folder.GitStatusLetter);
        Assert.AreEqual("#E2C08D", folder.GitStatusColor);
        Assert.IsTrue(folder.HasGitStatus);
        Assert.AreEqual("Contains modified files", folder.GitStatusToolTip);
    }
}
