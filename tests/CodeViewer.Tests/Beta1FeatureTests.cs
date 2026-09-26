using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta1FeatureTests
{
    [TestMethod]
    public void DiffService_IdenticalText_ReturnsAllUnchanged()
    {
        var diff = new DiffService();
        var text = "line 1\nline 2\nline 3";
        var lines = diff.ComputeDiff(text, text);

        Assert.AreEqual(3, lines.Count);
        Assert.IsTrue(lines.All(l => l.Type == DiffLineType.Unchanged));
    }

    [TestMethod]
    public void DiffService_AdditionsAndDeletions_DetectedCorrectly()
    {
        var diff = new DiffService();
        var oldText = "apple\nbanana\ncherry";
        var newText = "apple\nblueberry\ncherry\ndate";

        var lines = diff.ComputeDiff(oldText, newText);

        Assert.IsTrue(lines.Any(l => l.Type == DiffLineType.Removed && l.Text == "banana"));
        Assert.IsTrue(lines.Any(l => l.Type == DiffLineType.Added && l.Text == "blueberry"));
        Assert.IsTrue(lines.Any(l => l.Type == DiffLineType.Added && l.Text == "date"));
        Assert.IsTrue(lines.Any(l => l.Type == DiffLineType.Unchanged && l.Text == "apple"));
        Assert.IsTrue(lines.Any(l => l.Type == DiffLineType.Unchanged && l.Text == "cherry"));
    }

    [TestMethod]
    public void DiffViewModel_LoadDiff_ComputesSummaryStats()
    {
        var vm = new DiffViewModel(new DiffService());
        vm.LoadDiff("line1\nline2", "line1\nline2_mod\nline3", "fileA.txt", "fileB.txt");

        Assert.IsTrue(vm.IsActive);
        Assert.AreEqual("fileA.txt", vm.OldTitle);
        Assert.AreEqual("fileB.txt", vm.NewTitle);
        Assert.IsTrue(vm.AdditionsCount >= 1);
        Assert.IsFalse(string.IsNullOrEmpty(vm.SummaryText));

        vm.CloseDiff();
        Assert.IsFalse(vm.IsActive);
        Assert.AreEqual(0, vm.DiffLines.Count);
    }

    [TestMethod]
    public void FolderItem_ExtensionGlyphs_MatchCorrectLanguages()
    {
        var agent = new FolderItem("C:\\test\\workflow.agent", false);
        Assert.AreEqual("🤖", agent.IconGlyph);

        var ps2 = new FolderItem("C:\\test\\script.ps2", false);
        Assert.AreEqual("📜", ps2.IconGlyph);

        var dart = new FolderItem("C:\\test\\main.dart", false);
        Assert.AreEqual("🎯", dart.IconGlyph);

        var cs = new FolderItem("C:\\test\\Program.cs", false);
        Assert.AreEqual("🔷", cs.IconGlyph);

        var py = new FolderItem("C:\\test\\app.py", false);
        Assert.AreEqual("🐍", py.IconGlyph);

        var md = new FolderItem("C:\\test\\README.md", false);
        Assert.AreEqual("📝", md.IconGlyph);

        var dir = new FolderItem("C:\\test\\MyProject", true);
        Assert.AreEqual("📁", dir.IconGlyph);
        dir.IsExpanded = true;
        Assert.AreEqual("📂", dir.IconGlyph);
    }

    [TestMethod]
    public async Task FolderItem_LazyLoading_LoadsDirectoryContents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cv_test_tree_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.dart"), "void main() {}");
            File.WriteAllText(Path.Combine(tempDir, "script.agent"), "agent { }");
            Directory.CreateDirectory(Path.Combine(tempDir, "src"));

            var folder = new FolderItem(tempDir, true);
            // Has initial lazy loading placeholder
            Assert.AreEqual(1, folder.Children.Count);
            Assert.IsTrue(folder.Children[0].IsLoading);

            await folder.LoadChildrenAsync();

            // Children populated
            Assert.AreEqual(3, folder.Children.Count);
            Assert.IsTrue(folder.Children.Any(c => c.Name == "main.dart" && !c.IsDirectory && c.IconGlyph == "🎯"));
            Assert.IsTrue(folder.Children.Any(c => c.Name == "script.agent" && !c.IsDirectory && c.IconGlyph == "🤖"));
            Assert.IsTrue(folder.Children.Any(c => c.Name == "src" && c.IsDirectory));
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
    public void ScriptRunnerService_IsSupported_RecognizesExpectedExtensions()
    {
        var runner = new ScriptRunnerService();
        Assert.IsTrue(runner.IsSupported("test.agent"));
        Assert.IsTrue(runner.IsSupported("script.ps2"));
        Assert.IsTrue(runner.IsSupported("app.py"));
        Assert.IsTrue(runner.IsSupported("task.ps1"));
        Assert.IsTrue(runner.IsSupported("main.dart"));
        Assert.IsTrue(runner.IsSupported("index.js"));
        Assert.IsFalse(runner.IsSupported("data.csv"));
        Assert.IsFalse(runner.IsSupported("image.png"));
    }

    [TestMethod]
    public void CommandPalette_FilterCommands_MatchesQuery()
    {
        var vm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(),
            new MockDialogService(),
            new CodeViewer.Configuration.AppConfig());

        vm.InitializeCommands();

        vm.FilterCommands("agent");
        Assert.IsTrue(vm.FilteredCommands.Any(c => c.Title.Contains("AgentLang", StringComparison.OrdinalIgnoreCase)));

        vm.FilterCommands("diff");
        Assert.IsTrue(vm.FilteredCommands.Any(c => c.Category.Equals("Diff", StringComparison.OrdinalIgnoreCase) || c.Title.Contains("Diff", StringComparison.OrdinalIgnoreCase)));

        vm.FilterCommands("sidebar");
        Assert.IsTrue(vm.FilteredCommands.Any(c => c.Title.Contains("Sidebar", StringComparison.OrdinalIgnoreCase)));

        vm.FilterCommands("split");
        Assert.IsTrue(vm.FilteredCommands.Any(c => c.Title.Contains("Split", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void MainViewModel_ToggleSplitView_OperatesCorrectly()
    {
        var vm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(),
            new MockDialogService(),
            new CodeViewer.Configuration.AppConfig());

        vm.CreateNewDocument();
        Assert.IsFalse(vm.IsSplitViewActive);
        Assert.IsNull(vm.SecondaryDocument);

        vm.ToggleSplitView();
        Assert.IsTrue(vm.IsSplitViewActive);
        Assert.IsNotNull(vm.SecondaryDocument);

        vm.ToggleSplitView();
        Assert.IsFalse(vm.IsSplitViewActive);
        Assert.IsNull(vm.SecondaryDocument);
    }

    [TestMethod]
    public void KeyGestures_MainWindowGestures_CanBeParsed()
    {
        string[] gestures = {
            "Ctrl+N", "Ctrl+O", "Ctrl+Shift+O", "Ctrl+P", "Ctrl+S", "Ctrl+Shift+S",
            "Ctrl+Shift+C", "Ctrl+W", "Ctrl+F", "Ctrl+H", "Ctrl+G",
            "F3", "Shift+F3", "Ctrl+OemQuestion", "Ctrl+Divide",
            "Ctrl+B", "Ctrl+OemBackslash", "Ctrl+Shift+P", "F1", "F5",
            "Ctrl+Shift+M", "Ctrl+Shift+D", "Ctrl+OemPlus", "Ctrl+Add",
            "Ctrl+OemMinus", "Ctrl+Subtract", "Ctrl+D0", "Ctrl+NumPad0",
            "Alt+Z", "Ctrl+OemComma", "Ctrl+Z", "Ctrl+Y", "Ctrl+X",
            "Ctrl+C", "Ctrl+V", "Ctrl+A", "Ctrl+D", "Ctrl+Shift+K",
            "Alt+F4"
        };
        foreach (var g in gestures)
        {
            var parsed = Avalonia.Input.KeyGesture.Parse(g);
            Assert.IsNotNull(parsed, $"Failed on {g}");
        }
    }

    [TestMethod]
    public void DiffLine_BooleanProperties_ReflectTypeCorrectly()
    {
        var added = new DiffLine(DiffLineType.Added, null, 1, "test");
        Assert.IsTrue(added.IsAdded);
        Assert.IsFalse(added.IsRemoved);
        Assert.IsFalse(added.IsUnchanged);

        var removed = new DiffLine(DiffLineType.Removed, 1, null, "test");
        Assert.IsFalse(removed.IsAdded);
        Assert.IsTrue(removed.IsRemoved);
        Assert.IsFalse(removed.IsUnchanged);

        var unchanged = new DiffLine(DiffLineType.Unchanged, 1, 1, "test");
        Assert.IsFalse(unchanged.IsAdded);
        Assert.IsFalse(unchanged.IsRemoved);
        Assert.IsTrue(unchanged.IsUnchanged);
    }

    private class MockDialogService : IDialogService
    {
        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(ConfirmResult.Cancel);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(false);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    }
}
