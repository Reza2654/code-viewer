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
public class Rc1HardeningTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc1_Tests_" + Guid.NewGuid().ToString("N"));
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
    public async Task SessionService_ConcurrentSaves_DoNotThrowOrCorrupt()
    {
        var sessionFile = Path.Combine(_tempDir, "session.json");
        var service = new SessionService(sessionFile);

        var filePaths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var p = Path.Combine(_tempDir, $"file_{i}.cs");
            File.WriteAllText(p, $"// test {i}");
            filePaths.Add(p);
        }

        // Fire 10 parallel SaveSessionAsync tasks
        var tasks = Enumerable.Range(0, 10).Select(i =>
            service.SaveSessionAsync(filePaths.Take(i % 5 + 1), filePaths[0])
        ).ToArray();

        await Task.WhenAll(tasks);

        var loaded = await service.LoadSessionAsync();
        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.OpenFiles.Count > 0);
    }

    [TestMethod]
    public async Task MainViewModel_ShowTemporaryStatus_SetsAndClearsMessage()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        using var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);

        var statusTask = vm.ShowTemporaryStatusAsync("Testing RC1", 100);
        Assert.AreEqual("Testing RC1", vm.StatusMessage);

        await statusTask;
        Assert.IsNull(vm.StatusMessage);
    }

    [TestMethod]
    public async Task MainViewModel_OpenNonExistentFile_ShowsStatusAndDoesNotAddTab()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        using var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);
        vm.CreateNewDocument();
        var initialCount = vm.Documents.Count;

        var deadPath = Path.Combine(_tempDir, "dead_file.txt");
        await vm.OpenFileInternalAsync(deadPath);

        // Should not have added a document
        Assert.AreEqual(initialCount, vm.Documents.Count);
        Assert.IsNotNull(vm.StatusMessage);
        Assert.IsTrue(vm.StatusMessage.Contains("File not found"));
    }

    [TestMethod]
    public async Task MainViewModel_OpenRecentFile_NonExistent_RemovesFromRecentFiles()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        // Add a real file to recent files first
        var realFile = Path.Combine(_tempDir, "real_file.cs");
        File.WriteAllText(realFile, "class Real {}");
        await recentFilesService.AddRecentFileAsync(realFile);

        using var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);
        await vm.LoadRecentFilesAsync();
        Assert.AreEqual(1, vm.RecentFiles.Count);

        // Delete the file from disk
        File.Delete(realFile);

        // Attempt to open the recent file that is now gone
        await vm.OpenRecentFileAsync(realFile);

        // Recent files list should now have pruned the dead item
        Assert.AreEqual(0, vm.RecentFiles.Count);
    }

    [TestMethod]
    public void MainViewModel_Dispose_DoesNotThrow()
    {
        var fileService = new FileService();
        var languageService = new LanguageService();
        var recentFilesService = new RecentFilesService(10);
        var dialogService = new DialogService();
        var config = new AppConfig();

        var vm = new MainViewModel(fileService, languageService, recentFilesService, dialogService, config);
        vm.Dispose();
        // Second dispose call should be a no-op and safe
        vm.Dispose();
    }
}
