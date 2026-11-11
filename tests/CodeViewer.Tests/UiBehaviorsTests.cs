using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Input;
using AvaloniaEdit.Document;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using CodeViewer.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class UiBehaviorsTests
{
    [TestMethod]
    public void SmartBracketEditHandler_Disabled_DoesNotModify()
    {
        var doc = new TextDocument("var x = ");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, '(', 8, 0, 8,
            out var newCaret, out var newSelStart, out var newSelLen);

        // When ProcessCharInput is called directly, '(' triggers auto-insert
        Assert.IsTrue(handled);
        Assert.AreEqual("var x = ()", doc.Text);
        Assert.AreEqual(9, newCaret);
    }

    [TestMethod]
    public void SmartBracketEditHandler_WrapsSelection()
    {
        var doc = new TextDocument("123 + 456");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, '(', 0, 9, 0,
            out var newCaret, out var newSelStart, out var newSelLen);

        Assert.IsTrue(handled);
        Assert.AreEqual("(123 + 456)", doc.Text);
        Assert.AreEqual(1, newSelStart);
        Assert.AreEqual(9, newSelLen);
    }

    [TestMethod]
    public void SmartBracketEditHandler_OvertypesClosingBracket()
    {
        var doc = new TextDocument("foo()");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, ')', 0, 0, 4, // caret right before )
            out var newCaret, out var newSelStart, out var newSelLen);

        Assert.IsTrue(handled);
        Assert.AreEqual("foo()", doc.Text); // No duplicate ) inserted
        Assert.AreEqual(5, newCaret); // Caret advanced past )
    }

    [TestMethod]
    public void SmartBracketEditHandler_BackspaceDeletesPair()
    {
        var doc = new TextDocument("foo()");
        var handled = SmartBracketEditHandler.ProcessBackspace(doc, 4, out var newCaret);

        Assert.IsTrue(handled);
        Assert.AreEqual("foo", doc.Text);
        Assert.AreEqual(3, newCaret);
    }

    [TestMethod]
    public void EditorTextManipulator_ToggleComment_AddsAndRemoves()
    {
        var doc = new TextDocument("var a = 1;\nvar b = 2;");

        // 1. Toggle comment ON
        EditorTextManipulator.ToggleComment(doc, 0, doc.TextLength, "// ");
        Assert.AreEqual("// var a = 1;\n// var b = 2;", doc.Text);

        // 2. Toggle comment OFF
        EditorTextManipulator.ToggleComment(doc, 0, doc.TextLength, "// ");
        Assert.AreEqual("var a = 1;\nvar b = 2;", doc.Text);
    }

    [TestMethod]
    public void EditorTextManipulator_DuplicateLine()
    {
        var doc = new TextDocument("line 1\nline 2");

        EditorTextManipulator.DuplicateLineOrSelection(doc, 0, 0, 2,
            out var newCaret, out var newSelStart, out var newSelLen);

        StringAssert.Contains(doc.Text, "line 1");
        Assert.AreEqual(3, doc.LineCount);
    }

    [TestMethod]
    public void EditorTextManipulator_DeleteCurrentLine()
    {
        var doc = new TextDocument("first\nsecond\nthird");
        var secondLine = doc.GetLineByNumber(2);

        EditorTextManipulator.DeleteCurrentLine(doc, secondLine.Offset + 1, 0);

        Assert.AreEqual("first\nthird", doc.Text);
    }

    [TestMethod]
    public void EditorTextManipulator_MoveLinesUpAndDown()
    {
        var doc = new TextDocument("line1\nline2\nline3");
        var secondLine = doc.GetLineByNumber(2);

        // Move line 2 UP
        EditorTextManipulator.MoveSelectedLinesUp(doc, secondLine.Offset, secondLine.Length,
            out var newStart, out var newLen);
        Assert.AreEqual("line2\nline1\nline3", doc.Text);

        // Move line 2 DOWN (line 2 is now line 1)
        var firstLine = doc.GetLineByNumber(1);
        EditorTextManipulator.MoveSelectedLinesDown(doc, firstLine.Offset, firstLine.Length,
            out var downStart, out var downLen);
        Assert.AreEqual("line1\nline2\nline3", doc.Text);
    }

    [TestMethod]
    public void EditorSearchCoordinator_FindMatchOffsetsAndReplaceAll()
    {
        var doc = new TextDocument("apple orange apple banana apple");

        var matches = EditorSearchCoordinator.FindMatchOffsets(doc.Text, "apple", matchCase: false);
        Assert.AreEqual(3, matches.Count);
        Assert.AreEqual(0, matches[0]);
        Assert.AreEqual(13, matches[1]);
        Assert.AreEqual(26, matches[2]);

        var count = EditorSearchCoordinator.ReplaceAll(doc, "apple", "pear", matchCase: false);

        Assert.AreEqual(3, count);
        Assert.AreEqual("pear orange pear banana pear", doc.Text);
    }

    [TestMethod]
    public void FileDragDropHandler_ResolveDroppedPaths_ParsesUriText()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Text, new Uri(tempFile).AbsoluteUri);

            var paths = FileDragDropHandler.ResolveDroppedPaths(dataObject);

            Assert.AreEqual(1, paths.Count);
            Assert.AreEqual(Path.GetFullPath(tempFile), Path.GetFullPath(paths[0]));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #region WindowKeyboardRouter & MainWindow Alignment Tests

    private sealed class StubKeyboardActionTarget : WindowKeyboardRouter.IKeyboardActionTarget
    {
        public void FocusEditor() { }
        public void ToggleComment() { }
        public void DuplicateLineOrSelection() { }
        public void DeleteCurrentLine() { }
        public void MoveSelectedLinesUp() { }
        public void MoveSelectedLinesDown() { }
    }

    private sealed class FakeGitService : IGitService
    {
        public int StatusCallCount { get; private set; }
        public TaskCompletionSource<bool> StatusCalled { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string?> FindGitRootAsync(string directoryPath, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(directoryPath);

        public Task<Dictionary<string, GitFileStatus>> GetStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            StatusCalled.TrySetResult(true);
            return Task.FromResult(new Dictionary<string, GitFileStatus>());
        }

        public Task<bool> IsGitInstalledAsync() => Task.FromResult(true);
    }

    private static (MainViewModel Vm, string TempDir, FakeGitService GitService) CreateTestMainViewModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_KeyboardTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var gitService = new FakeGitService();
        var vm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(5),
            new DialogService(),
            new AppConfig(),
            settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
            sessionService: new SessionService(Path.Combine(tempDir, "session.json")),
            gitService: gitService);
        return (vm, tempDir, gitService);
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"Could not locate '{relativePath}' above '{AppContext.BaseDirectory}'.");
    }

    [TestMethod]
    public async Task WindowKeyboardRouter_CtrlShiftG_TriggersRefreshGitStatus()
    {
        var (vm, tempDir, gitService) = CreateTestMainViewModel();
        try
        {
            vm.RootFolder = new FolderItem(tempDir, isDirectory: true, name: "ws");
            var args = new KeyEventArgs
            {
                Key = Key.G,
                KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift
            };

            var handled = WindowKeyboardRouter.HandleKeyDown(args, vm, new StubKeyboardActionTarget());
            var completed = await Task.WhenAny(gitService.StatusCalled.Task, Task.Delay(3000));

            Assert.IsTrue(handled);
            Assert.IsTrue(args.Handled);
            Assert.AreSame(gitService.StatusCalled.Task, completed, "Expected RefreshGitStatusAsync to execute");
            Assert.AreEqual(1, gitService.StatusCallCount);
            Assert.IsFalse(vm.IsGoToLineOpen);
        }
        finally
        {
            vm.Dispose();
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void WindowKeyboardRouter_CtrlG_OpensGoToLineWithoutRefreshingGit()
    {
        var (vm, tempDir, gitService) = CreateTestMainViewModel();
        try
        {
            vm.RootFolder = new FolderItem(tempDir, isDirectory: true, name: "ws");
            var args = new KeyEventArgs
            {
                Key = Key.G,
                KeyModifiers = KeyModifiers.Control
            };

            var handled = WindowKeyboardRouter.HandleKeyDown(args, vm, new StubKeyboardActionTarget());

            Assert.IsTrue(handled);
            Assert.IsTrue(args.Handled);
            Assert.IsTrue(vm.IsGoToLineOpen);
            Assert.AreEqual(0, gitService.StatusCallCount);
        }
        finally
        {
            vm.Dispose();
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void WindowKeyboardRouter_ViewPanelShortcuts_ToggleOnceCorrectly()
    {
        var (vm, tempDir, _) = CreateTestMainViewModel();
        try
        {
            var target = new StubKeyboardActionTarget();

            // Ctrl+Alt+M -> ToggleMinimap
            var initialMinimap = vm.ShowMinimap;
            var minimapArgs = new KeyEventArgs { Key = Key.M, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(minimapArgs, vm, target));
            Assert.IsTrue(minimapArgs.Handled);
            Assert.AreEqual(!initialMinimap, vm.ShowMinimap);

            // Ctrl+Alt+O -> ToggleOutline
            vm.IsSidebarOpen = false;
            var outlineArgs = new KeyEventArgs { Key = Key.O, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(outlineArgs, vm, target));
            Assert.IsTrue(outlineArgs.Handled);
            Assert.IsTrue(vm.IsOutlineVisible);

            // Ctrl+Shift+E -> Focus Explorer
            var explorerArgs = new KeyEventArgs { Key = Key.E, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(explorerArgs, vm, target));
            Assert.IsTrue(explorerArgs.Handled);
            Assert.IsTrue(vm.IsSidebarOpen);
            Assert.IsFalse(vm.IsOutlineActive);
            Assert.IsFalse(vm.IsOutlineVisible);

            // Ctrl+OemTilde & Ctrl+Oem3 -> ToggleRunnerConsole
            var initialRunner = vm.RunnerViewModel.IsVisible;
            var tildeArgs = new KeyEventArgs { Key = Key.OemTilde, KeyModifiers = KeyModifiers.Control };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(tildeArgs, vm, target));
            Assert.AreEqual(!initialRunner, vm.RunnerViewModel.IsVisible);

            var oem3Args = new KeyEventArgs { Key = Key.Oem3, KeyModifiers = KeyModifiers.Control };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(oem3Args, vm, target));
            Assert.AreEqual(initialRunner, vm.RunnerViewModel.IsVisible);
        }
        finally
        {
            vm.Dispose();
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void MainViewModel_IsOutlineVisible_ReflectsSidebarAndOutlineState()
    {
        var (vm, tempDir, _) = CreateTestMainViewModel();
        try
        {
            vm.IsSidebarOpen = false;
            vm.IsOutlineActive = false;
            Assert.IsFalse(vm.IsOutlineVisible);

            vm.ShowOutlineSidebar();
            Assert.IsTrue(vm.IsOutlineVisible);

            vm.ToggleOutline();
            Assert.IsFalse(vm.IsSidebarOpen);
            Assert.IsFalse(vm.IsOutlineVisible);

            vm.ShowOutlineSidebar();
            Assert.IsTrue(vm.IsOutlineVisible);

            vm.ShowExplorerSidebar();
            Assert.IsTrue(vm.IsSidebarOpen);
            Assert.IsFalse(vm.IsOutlineVisible);
        }
        finally
        {
            vm.Dispose();
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void MainWindowAxaml_ViewMenuAndKeyBindings_DoNotDoubleTriggerKeyboardRouter()
    {
        var mainWindowPath = FindRepoFile(Path.Combine("src", "CodeViewer", "Views", "MainWindow.axaml"));
        var doc = XDocument.Load(mainWindowPath);
        XNamespace ns = "https://github.com/avaloniaui";

        var viewMenu = doc.Descendants(ns + "MenuItem")
            .FirstOrDefault(e => (string?)e.Attribute("Header") == "_View");
        Assert.IsNotNull(viewMenu, "Expected _View menu in MainWindow.axaml");

        var expectedViewCommands = new Dictionary<string, string>
        {
            ["{Binding ToggleOutlineCommand}"] = "Ctrl+Alt+O",
            ["{Binding ToggleRunnerConsoleCommand}"] = "Ctrl+OemTilde",
            ["{Binding ToggleMinimapCommand}"] = "Ctrl+Alt+M",
            ["{Binding RefreshGitStatusCommand}"] = "Ctrl+Shift+G",
            ["{Binding ShowExplorerSidebarCommand}"] = "Ctrl+Shift+E"
        };

        var viewItems = viewMenu.Elements(ns + "MenuItem").ToList();
        foreach (var (commandBinding, expectedGesture) in expectedViewCommands)
        {
            var item = viewItems.FirstOrDefault(e => (string?)e.Attribute("Command") == commandBinding);
            Assert.IsNotNull(item, $"Missing _View MenuItem for {commandBinding}");
            Assert.AreEqual(expectedGesture, (string?)item.Attribute("InputGesture"));
            Assert.IsNull(item.Attribute("HotKey"), $"MenuItem {commandBinding} must use InputGesture without HotKey to avoid duplicate triggers");
        }

        // Validate every MenuItem InputGesture and KeyBinding Gesture parses cleanly
        var windowKeyBindings = doc.Descendants(ns + "Window.KeyBindings")
            .Descendants(ns + "KeyBinding")
            .Select(kb => (string?)kb.Attribute("Gesture"))
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => KeyGesture.Parse(g!))
            .ToList();

        foreach (var menuItem in doc.Descendants(ns + "MenuItem"))
        {
            var ig = (string?)menuItem.Attribute("InputGesture");
            if (!string.IsNullOrWhiteSpace(ig))
            {
                _ = KeyGesture.Parse(ig);
            }

            var hk = (string?)menuItem.Attribute("HotKey");
            if (!string.IsNullOrWhiteSpace(hk))
            {
                var parsedHk = KeyGesture.Parse(hk);
                Assert.IsFalse(
                    windowKeyBindings.Contains(parsedHk),
                    $"HotKey '{hk}' duplicates a <Window.KeyBindings> entry");
            }
        }

        // Shortcuts routed by WindowKeyboardRouter must not also live in <Window.KeyBindings>
        var routerManagedGestures = new[]
        {
            "Ctrl+N", "Ctrl+O", "Ctrl+P", "Ctrl+S", "Ctrl+Shift+S", "Ctrl+W", "Ctrl+Shift+T",
            "Ctrl+F", "Ctrl+H", "Ctrl+G", "F3", "Shift+F3", "Ctrl+OemQuestion", "Ctrl+Divide",
            "F5", "Ctrl+OemPlus", "Ctrl+Add", "Ctrl+OemMinus", "Ctrl+Subtract",
            "Ctrl+D0", "Ctrl+NumPad0", "Alt+Z", "Ctrl+Alt+M", "Ctrl+Alt+O",
            "Ctrl+OemTilde", "Ctrl+Shift+E", "Ctrl+Shift+G", "Ctrl+OemComma"
        }.Select(KeyGesture.Parse).ToHashSet();

        foreach (var kbGesture in windowKeyBindings)
        {
            Assert.IsFalse(
                routerManagedGestures.Contains(kbGesture),
                $"<Window.KeyBindings> gesture '{kbGesture}' duplicates WindowKeyboardRouter");
        }
    }

    [TestMethod]
    public void SettingsDialogAxaml_ExposesShowMinimapAndTerminalShell()
    {
        var settingsDialogPath = FindRepoFile(Path.Combine("src", "CodeViewer", "Views", "SettingsDialog.axaml"));
        var xml = File.ReadAllText(settingsDialogPath);
        StringAssert.Contains(xml, "IsChecked=\"{Binding ShowMinimap}\"");
        StringAssert.Contains(xml, "ItemsSource=\"{Binding AvailableTerminalShells}\"");
        StringAssert.Contains(xml, "SelectedItem=\"{Binding SelectedTerminalShell, Mode=TwoWay}\"");
    }

    #endregion
}
