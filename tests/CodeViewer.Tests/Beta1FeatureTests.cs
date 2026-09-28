using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Rendering;
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
            "Ctrl+Shift+M", "Ctrl+Shift+D", "Ctrl+Alt+R", "Ctrl+OemPlus", "Ctrl+Add",
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

    [TestMethod]
    public void Avalonia_Grid_ColumnDefinitions_Test()
    {
        var grid = new Avalonia.Controls.Grid();
        var col1 = new Avalonia.Controls.ColumnDefinition(1, Avalonia.Controls.GridUnitType.Star);
        var col2 = new Avalonia.Controls.ColumnDefinition(0, Avalonia.Controls.GridUnitType.Pixel);
        var col3 = new Avalonia.Controls.ColumnDefinition(0, Avalonia.Controls.GridUnitType.Pixel);
        grid.ColumnDefinitions.Add(col1);
        grid.ColumnDefinitions.Add(col2);
        grid.ColumnDefinitions.Add(col3);

        // Initial: only primary visible
        grid.Measure(new Avalonia.Size(1000, 500));
        grid.Arrange(new Avalonia.Rect(0, 0, 1000, 500));
        Assert.AreEqual(1000, col1.ActualWidth);
        Assert.AreEqual(0, col2.ActualWidth);
        Assert.AreEqual(0, col3.ActualWidth);

        // Toggle Split View ON
        col2.Width = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star);
        grid.Measure(new Avalonia.Size(1000, 500));
        grid.Arrange(new Avalonia.Rect(0, 0, 1000, 500));
        Assert.AreEqual(500, col1.ActualWidth);
        Assert.AreEqual(500, col2.ActualWidth);
        Assert.AreEqual(0, col3.ActualWidth);

        // Toggle Split View OFF
        col2.Width = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        grid.Measure(new Avalonia.Size(1000, 500));
        grid.Arrange(new Avalonia.Rect(0, 0, 1000, 500));
        Assert.AreEqual(1000, col1.ActualWidth);
        Assert.AreEqual(0, col2.ActualWidth);

        // Toggle Markdown Preview ON
        col3.Width = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star);
        grid.Measure(new Avalonia.Size(1000, 500));
        grid.Arrange(new Avalonia.Rect(0, 0, 1000, 500));
        Assert.AreEqual(500, col1.ActualWidth);
        Assert.AreEqual(500, col3.ActualWidth);

        // Toggle Markdown Preview OFF (close panel)
        col3.Width = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        grid.Measure(new Avalonia.Size(1000, 500));
        grid.Arrange(new Avalonia.Rect(0, 0, 1000, 500));
        Assert.AreEqual(1000, col1.ActualWidth);
        Assert.AreEqual(0, col3.ActualWidth);
    }

    [TestMethod]
    public void FlowDirection_Control_Supported()
    {
        var control = new Avalonia.Controls.Border();
        control.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        Assert.AreEqual(Avalonia.Media.FlowDirection.RightToLeft, control.FlowDirection);
        control.FlowDirection = Avalonia.Media.FlowDirection.LeftToRight;
        Assert.AreEqual(Avalonia.Media.FlowDirection.LeftToRight, control.FlowDirection);
    }

    [TestMethod]
    public void Persian_Language_Detection_And_List()
    {
        var langService = new LanguageService();
        Assert.AreEqual("Persian", langService.DetectLanguage("test.fa"));
        Assert.AreEqual("Persian", langService.DetectLanguage("script.farsi"));
        Assert.AreEqual("Persian", langService.DetectLanguage("doc.persian"));
        CollectionAssert.Contains(langService.GetSupportedLanguages().ToList(), "Persian");
    }

    [TestMethod]
    public void DocumentViewModel_Rtl_Persian_AutoDetection_And_Toggle()
    {
        var langService = new LanguageService();
        var model = new DocumentModel
        {
            FilePath = "note.fa",
            Title = "note.fa",
            Text = "سلام دنیا! این یک متن فارسی است."
        };
        var docVm = new DocumentViewModel(model, langService);

        Assert.IsTrue(docVm.IsRtl);
        Assert.AreEqual(Avalonia.Media.FlowDirection.RightToLeft, docVm.FlowDirection);
        Assert.AreEqual("RTL", docVm.TextDirectionDisplay);

        docVm.IsRtl = false;
        Assert.IsFalse(docVm.IsRtl);
        Assert.AreEqual(Avalonia.Media.FlowDirection.LeftToRight, docVm.FlowDirection);
        Assert.AreEqual("LTR", docVm.TextDirectionDisplay);

        docVm.IsRtl = true;
        Assert.IsTrue(docVm.IsRtl);
        Assert.AreEqual("RTL", docVm.TextDirectionDisplay);
    }

    [TestMethod]
    public void MarkdownRenderer_Rtl_Detection_And_Rendering()
    {
        Assert.IsTrue(MarkdownRenderer.IsRtlText("سلام دنیا"));
        Assert.IsFalse(MarkdownRenderer.IsRtlText("Hello world"));

        var panel = MarkdownRenderer.Render("# تیتر فارسی\n\nاین یک پاراگراف فارسی است.") as Avalonia.Controls.StackPanel;
        Assert.IsNotNull(panel);

        var textBlocks = panel.Children.OfType<Avalonia.Controls.TextBlock>().ToList();
        Assert.IsTrue(textBlocks.Count >= 2);

        var heading = textBlocks[0];
        Assert.AreEqual(Avalonia.Media.FlowDirection.RightToLeft, heading.FlowDirection);
        Assert.AreEqual(Avalonia.Media.TextAlignment.Right, heading.TextAlignment);

        var paragraph = textBlocks[1];
        Assert.AreEqual(Avalonia.Media.FlowDirection.RightToLeft, paragraph.FlowDirection);
        Assert.AreEqual(Avalonia.Media.TextAlignment.Right, paragraph.TextAlignment);
    }

    [TestMethod]
    public void MainViewModel_MarkdownPreview_And_RtlOperations()
    {
        var vm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(),
            new MockDialogService(),
            new CodeViewer.Configuration.AppConfig());

        vm.CreateNewDocument();
        Assert.IsNotNull(vm.ActiveDocument);
        Assert.IsFalse(vm.IsActiveDocumentMarkdown);

        // Open a markdown file
        var mdDoc = new DocumentViewModel(new DocumentModel { FilePath = "README.md", Title = "README.md", Text = "# Hello" }, new LanguageService());
        vm.Documents.Add(mdDoc);
        vm.ActiveDocument = mdDoc;

        Assert.IsTrue(vm.IsActiveDocumentMarkdown);

        // Toggle Markdown Preview ON
        vm.ToggleMarkdownPreview();
        Assert.IsTrue(vm.IsMarkdownPreviewActive);

        // Switch to non-markdown document -> preview should automatically close!
        vm.ActiveDocument = vm.Documents[0];
        Assert.IsFalse(vm.IsActiveDocumentMarkdown);
        Assert.IsFalse(vm.IsMarkdownPreviewActive);

        // Toggle RTL Command
        Assert.IsFalse(vm.ActiveDocument.IsRtl);
        vm.ToggleRtl();
        Assert.IsTrue(vm.ActiveDocument.IsRtl);
        Assert.AreEqual(Avalonia.Media.FlowDirection.RightToLeft, vm.ActiveDocument.FlowDirection);
        vm.ToggleRtl();
        Assert.IsFalse(vm.ActiveDocument.IsRtl);
        Assert.AreEqual(Avalonia.Media.FlowDirection.LeftToRight, vm.ActiveDocument.FlowDirection);
    }

    [TestMethod]
    public void ScriptRunnerService_CreatesStartInfo_WithUtf8Encoding()
    {
        var runner = new ScriptRunnerService();
        Assert.IsTrue(runner.IsSupported("test.agent"));
        Assert.IsTrue(runner.IsSupported("script.ps2"));
        Assert.IsTrue(runner.IsSupported("app.py"));
        Assert.IsTrue(runner.IsSupported("deploy.ps1"));

        var agentStartInfo = runner.CreateStartInfo("test.agent");
        Assert.AreEqual(System.Text.Encoding.UTF8, agentStartInfo.StandardOutputEncoding);
        Assert.AreEqual(System.Text.Encoding.UTF8, agentStartInfo.StandardErrorEncoding);
        Assert.AreEqual("utf-8", agentStartInfo.Environment["PYTHONIOENCODING"]);
        Assert.AreEqual("1", agentStartInfo.Environment["PYTHONUTF8"]);
        Assert.AreEqual("en_US.UTF-8", agentStartInfo.Environment["LANG"]);

        var pyStartInfo = runner.CreateStartInfo("test.py");
        Assert.AreEqual(System.Text.Encoding.UTF8, pyStartInfo.StandardOutputEncoding);
        Assert.AreEqual(System.Text.Encoding.UTF8, pyStartInfo.StandardErrorEncoding);

        var ps1StartInfo = runner.CreateStartInfo("test.ps1");
        Assert.AreEqual(System.Text.Encoding.UTF8, ps1StartInfo.StandardOutputEncoding);
        Assert.AreEqual(System.Text.Encoding.UTF8, ps1StartInfo.StandardErrorEncoding);
        Assert.IsTrue(ps1StartInfo.Arguments.Contains("OutputEncoding"));
    }

    [TestMethod]
    public void ThemeService_ResolvesLightAndDarkThemesWithCustomOverrides()
    {
        var themeService = new ThemeService();

        var lightSettings = new AppSettings
        {
            ThemeMode = "Light",
            ContrastMode = "Default",
            LightPresetId = "default-light",
            LightBackgroundHex = "#FAFAFA",
            LightForegroundHex = "#1A1A1A",
            LightAccentHex = "#0088FF"
        };

        var resolvedLight = themeService.ResolveEffectiveTheme(lightSettings);
        Assert.IsFalse(resolvedLight.IsDark);
        Assert.AreEqual("#FAFAFA", resolvedLight.WindowBackground);
        Assert.AreEqual("#1A1A1A", resolvedLight.Foreground);
        Assert.AreEqual("#0088FF", resolvedLight.AccentColor);

        var darkSettings = new AppSettings
        {
            ThemeMode = "Dark",
            ContrastMode = "Default",
            DarkPresetId = "default-dark",
            DarkBackgroundHex = "#121212",
            DarkForegroundHex = "#EEEEEE",
            DarkAccentHex = "#00AAFF"
        };

        var resolvedDark = themeService.ResolveEffectiveTheme(darkSettings);
        Assert.IsTrue(resolvedDark.IsDark);
        Assert.AreEqual("#121212", resolvedDark.WindowBackground);
        Assert.AreEqual("#EEEEEE", resolvedDark.Foreground);
        Assert.AreEqual("#00AAFF", resolvedDark.AccentColor);
    }

    [TestMethod]
    public void ThemeService_ResolvesStrongContrastModes()
    {
        var themeService = new ThemeService();

        var lightStrong = new AppSettings
        {
            ThemeMode = "Light",
            ContrastMode = "Strong"
        };
        var resolvedLight = themeService.ResolveEffectiveTheme(lightStrong);
        Assert.AreEqual("#FFFFFF", resolvedLight.WindowBackground);
        Assert.AreEqual("#000000", resolvedLight.Foreground);

        var darkStrong = new AppSettings
        {
            ThemeMode = "Dark",
            ContrastMode = "Strong"
        };
        var resolvedDark = themeService.ResolveEffectiveTheme(darkStrong);
        Assert.AreEqual("#000000", resolvedDark.WindowBackground);
        Assert.AreEqual("#FFFFFF", resolvedDark.Foreground);
    }

    [TestMethod]
    public void SettingsViewModel_ThemeModeAndPresetSelection()
    {
        var settingsService = new SettingsService();
        var themeService = new ThemeService();
        var vm = new SettingsViewModel(settingsService, themeService);

        vm.SetThemeMode("Light");
        Assert.AreEqual("Light", vm.ThemeMode);
        Assert.IsTrue(vm.IsLightTheme);
        Assert.IsFalse(vm.IsDarkTheme);

        vm.SetThemeMode("Dark");
        Assert.AreEqual("Dark", vm.ThemeMode);
        Assert.IsTrue(vm.IsDarkTheme);
        Assert.IsFalse(vm.IsLightTheme);

        vm.SetContrastMode("Strong");
        Assert.AreEqual("Strong", vm.ContrastMode);
        Assert.IsTrue(vm.IsStrongContrast);
        Assert.IsFalse(vm.IsDefaultContrast);

        // Selecting a preset updates the color hexes
        var solarized = themeService.AvailableThemes.First(t => t.Id == "solarized-dark");
        vm.SelectedDarkPreset = solarized;
        Assert.AreEqual(solarized.WindowBackground, vm.DarkBackgroundHex);
        Assert.AreEqual(solarized.Foreground, vm.DarkForegroundHex);
        Assert.AreEqual(solarized.AccentColor, vm.DarkAccentHex);
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
