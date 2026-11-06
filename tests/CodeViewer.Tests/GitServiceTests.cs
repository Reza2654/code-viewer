using System;
using System.IO;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class GitServiceTests
{
    [TestMethod]
    public void ParsePorcelainLine_ModifiedVariations()
    {
        var line1 = " M src/Program.cs";
        var res1 = GitService.ParsePorcelainLine(line1);
        Assert.IsNotNull(res1);
        Assert.AreEqual(Path.Combine("src", "Program.cs"), res1.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Modified, res1.Value.Status);

        var line2 = "M  src/Program.cs";
        var res2 = GitService.ParsePorcelainLine(line2);
        Assert.IsNotNull(res2);
        Assert.AreEqual(GitFileStatus.Modified, res2.Value.Status);

        var line3 = "MM src/Program.cs";
        var res3 = GitService.ParsePorcelainLine(line3);
        Assert.IsNotNull(res3);
        Assert.AreEqual(GitFileStatus.Modified, res3.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Untracked()
    {
        var line = "?? src/NewFile.cs";
        var res = GitService.ParsePorcelainLine(line);
        Assert.IsNotNull(res);
        Assert.AreEqual(Path.Combine("src", "NewFile.cs"), res.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Untracked, res.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Added()
    {
        var line1 = "A  src/Added.cs";
        var res1 = GitService.ParsePorcelainLine(line1);
        Assert.IsNotNull(res1);
        Assert.AreEqual(Path.Combine("src", "Added.cs"), res1.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Added, res1.Value.Status);

        var line2 = "AM src/AddedModified.cs";
        var res2 = GitService.ParsePorcelainLine(line2);
        Assert.IsNotNull(res2);
        Assert.AreEqual(GitFileStatus.Added, res2.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Deleted()
    {
        var line1 = " D src/Deleted.cs";
        var res1 = GitService.ParsePorcelainLine(line1);
        Assert.IsNotNull(res1);
        Assert.AreEqual(Path.Combine("src", "Deleted.cs"), res1.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Deleted, res1.Value.Status);

        var line2 = "D  src/Deleted.cs";
        var res2 = GitService.ParsePorcelainLine(line2);
        Assert.IsNotNull(res2);
        Assert.AreEqual(GitFileStatus.Deleted, res2.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Renamed()
    {
        var line = "R  old.txt -> new.txt";
        var res = GitService.ParsePorcelainLine(line);
        Assert.IsNotNull(res);
        Assert.AreEqual("new.txt", res.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Renamed, res.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_QuotedPathWithSpaces()
    {
        var line = "?? \"my folder/my file.txt\"";
        var res = GitService.ParsePorcelainLine(line);
        Assert.IsNotNull(res);
        Assert.AreEqual(Path.Combine("my folder", "my file.txt"), res.Value.RelativePath);
        Assert.AreEqual(GitFileStatus.Untracked, res.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Conflicted()
    {
        var line = "UU src/Conflict.cs";
        var res = GitService.ParsePorcelainLine(line);
        Assert.IsNotNull(res);
        Assert.AreEqual(GitFileStatus.Conflicted, res.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_Ignored()
    {
        var line = "!! logs/app.log";
        var res = GitService.ParsePorcelainLine(line);
        Assert.IsNotNull(res);
        Assert.AreEqual(GitFileStatus.Ignored, res.Value.Status);
    }

    [TestMethod]
    public void ParsePorcelainLine_InvalidOrHeadersReturnNull()
    {
        Assert.IsNull(GitService.ParsePorcelainLine("## main...origin/main"));
        Assert.IsNull(GitService.ParsePorcelainLine(""));
        Assert.IsNull(GitService.ParsePorcelainLine("   "));
        Assert.IsNull(GitService.ParsePorcelainLine("M "));
    }

    [TestMethod]
    public async Task GitService_IsGitInstalledAsync_ReturnsTrue()
    {
        var service = new GitService();
        var isInstalled = await service.IsGitInstalledAsync();
        Assert.IsTrue(isInstalled, "Git should be installed and on PATH in this environment.");
    }

    [TestMethod]
    public async Task GitService_FindGitRootAsync_ReturnsValidRootForCurrentRepo()
    {
        var service = new GitService();
        var currentDir = Directory.GetCurrentDirectory();
        var root = await service.FindGitRootAsync(currentDir);

        Assert.IsNotNull(root);
        Assert.IsTrue(Directory.Exists(root));
    }

    [TestMethod]
    public async Task GitService_FindGitRootAsync_NonGitDir_ReturnsNull()
    {
        var service = new GitService();
        var tempDir = Path.Combine(Path.GetTempPath(), "NonGitDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var root = await service.FindGitRootAsync(tempDir);
            Assert.IsNull(root);
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
    public async Task GitService_GetStatusAsync_NonGitDir_ReturnsEmptyDictionary()
    {
        var service = new GitService();
        var tempDir = Path.Combine(Path.GetTempPath(), "NonGitDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var statuses = await service.GetStatusAsync(tempDir);
            Assert.IsNotNull(statuses);
            Assert.AreEqual(0, statuses.Count);
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
    public async Task GitService_GetStatusAsync_CurrentRepo_ReturnsDictionary()
    {
        var service = new GitService();
        var currentDir = Directory.GetCurrentDirectory();
        var statuses = await service.GetStatusAsync(currentDir);

        Assert.IsNotNull(statuses);
        // Current repo has worktree changes, so statuses dictionary should not be null
        Assert.IsTrue(statuses is not null);
    }
}
