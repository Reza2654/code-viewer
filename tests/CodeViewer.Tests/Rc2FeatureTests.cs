using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using CodeViewer.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Rc2FeatureTests
{
    private sealed class StubDialogService : IDialogService
    {
        public ConfirmResult NextSaveConfirmation { get; set; } = ConfirmResult.Cancel;
        public string? NextSaveFilePath { get; set; }

        public void Initialize(Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult(NextSaveFilePath);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string documentTitle) => Task.FromResult(NextSaveConfirmation);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(true);
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
        public Task ShowUpdateDialogAsync(AppUpdateInfo updateInfo, IAppUpdateService updateService) => Task.CompletedTask;
    }

    private sealed class StubKeyboardTarget : WindowKeyboardRouter.IKeyboardActionTarget
    {
        public int FocusCount { get; private set; }
        public void FocusEditor() => FocusCount++;
        public void ToggleComment() { }
        public void DuplicateLineOrSelection() { }
        public void DeleteCurrentLine() { }
        public void MoveSelectedLinesUp() { }
        public void MoveSelectedLinesDown() { }
    }

    private static double RelativeLuminance(Color c)
    {
        static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(c.R / 255.0) + 0.7152 * Linear(c.G / 255.0) + 0.0722 * Linear(c.B / 255.0);
    }

    private static double ContrastRatio(string fgHex, string bgHex)
    {
        var fg = Color.Parse(fgHex);
        var bg = Color.Parse(bgHex);
        var l1 = RelativeLuminance(fg);
        var l2 = RelativeLuminance(bg);
        if (l1 < l2) (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }

    private static MainViewModel CreateMainViewModel(string tempDir, IDialogService? dialogService = null, AppConfig? config = null)
    {
        return new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(10),
            dialogService ?? new StubDialogService(),
            config ?? new AppConfig(),
            settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
            sessionService: new SessionService(Path.Combine(tempDir, "session.json")));
    }

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsRc2AndOrdersAfterRc1()
    {
        Assert.AreEqual("1.2.0-rc.2", AppUpdateService.GetCurrentAppVersion());
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.2", "1.2.0-rc.1") > 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0", "1.2.0-rc.2") > 0);
    }

    [TestMethod]
    public void BuiltInThemes_AllThemes_MeetWCAGContrastStandards()
    {
        var themes = BuiltInThemes.GetAll().Append(BuiltInThemes.CreateSampleCustomTheme()).ToList();

        foreach (var t in themes)
        {
            Assert.IsTrue(ContrastRatio(t.Foreground, t.WindowBackground) >= 4.5, $"{t.Id} Foreground/WindowBackground failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.EditorForeground, t.EditorBackground) >= 4.5, $"{t.Id} EditorForeground/EditorBackground failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.MenuForeground, t.MenuBackground) >= 4.5, $"{t.Id} MenuForeground/MenuBackground failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.TabItemActiveForeground, t.TabItemActiveBackground) >= 4.5, $"{t.Id} TabActive failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.TabItemInactiveForeground, t.TabBarBackground) >= 4.5, $"{t.Id} TabInactive failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.StatusBarForeground, t.StatusBarBackground) >= 4.5, $"{t.Id} StatusBar failed WCAG AA.");
            Assert.IsTrue(ContrastRatio(t.LineNumbersForeground, t.EditorBackground) >= 3.0, $"{t.Id} LineNumbers failed 3.0.");
            Assert.IsTrue(ContrastRatio(t.CodeComment, t.EditorBackground) >= 3.0, $"{t.Id} CodeComment failed 3.0.");
            Assert.IsTrue(ContrastRatio(t.SelectionBackground, t.EditorBackground) >= 1.3, $"{t.Id} Selection contrast too subtle.");

            var syntaxTokens = new[]
            {
                t.CodeKeyword, t.CodeString, t.CodeNumber, t.CodeType,
                t.CodeMethod, t.CodePreprocessor, t.CodePunctuation, t.CodeVariable
            };

            foreach (var tokenHex in syntaxTokens)
            {
                var ratio = ContrastRatio(tokenHex, t.EditorBackground);
                Assert.IsTrue(ratio >= 4.5, $"{t.Id} token {tokenHex} on {t.EditorBackground} contrast {ratio:F2} < 4.5.");
            }
        }
    }

    [TestMethod]
    public void ColorTheme_LightTheme_AvoidsDarkDefaultVariableCyan()
    {
        var bareLight = new ColorTheme { IsDark = false };
        Assert.AreEqual("#001080", bareLight.GetEffectiveCodeVariable());

        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Var_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var themeService = new ThemeService(tempDir);
            var settings = new AppSettings { ThemeMode = "Light", LightPresetId = "github-light" };
            var effective = themeService.ResolveEffectiveTheme(settings);

            Assert.IsFalse(effective.IsDark);
            Assert.AreNotEqual("#9CDCFE", effective.CodeVariable);
            Assert.IsTrue(ContrastRatio(effective.CodeVariable, effective.EditorBackground) >= 4.5);

            var psDef = HighlightingManager.Instance.GetDefinition("PowerShell")!;
            HighlightingThemeApplicator.Apply(psDef, bareLight);
            var varBrush = (ISolidColorBrush)psDef.NamedHighlightingColors.First(c => c.Name == "Variable").Foreground!.GetBrush(null)!;
            Assert.AreEqual(Color.Parse("#001080"), varBrush.Color);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void ColorTheme_ModeBadge_FormatsReadableLabel()
    {
        Assert.AreEqual("DARK", new ColorTheme { IsDark = true }.ModeBadge);
        Assert.AreEqual("LIGHT", new ColorTheme { IsDark = false }.ModeBadge);
    }

    [TestMethod]
    public void ThemeService_GetContrastingForegroundHex_AdaptsToBrightAndDarkAccents()
    {
        Assert.AreEqual("#FFFFFF", ThemeService.GetContrastingForegroundHex("#007ACC"));
        Assert.AreEqual("#FFFFFF", ThemeService.GetContrastingForegroundHex("#0366D6"));
        Assert.AreEqual("#101010", ThemeService.GetContrastingForegroundHex("#61FFCA"));
        Assert.AreEqual("#101010", ThemeService.GetContrastingForegroundHex("#A6E22E"));
        Assert.AreEqual("#101010", ThemeService.GetContrastingForegroundHex("#FABD2F"));
        Assert.AreEqual("#101010", ThemeService.GetContrastingForegroundHex("#88C0D0"));

        foreach (var t in BuiltInThemes.GetAll())
        {
            var fg = ThemeService.GetContrastingForegroundHex(t.AccentColor);
            Assert.IsTrue(ContrastRatio(fg, t.AccentColor) >= 4.5, $"Accent foreground {fg} on {t.AccentColor} ({t.Id}) < 4.5.");
        }
    }

    [TestMethod]
    public void ThemeService_StrongContrastMode_SetsMenuAndTabChromeColors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Strong_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var themeService = new ThemeService(tempDir);
            var lightStrong = themeService.ResolveEffectiveTheme(new AppSettings
            {
                ThemeMode = "Light",
                ContrastMode = "Strong",
                LightPresetId = "solarized-light"
            });

            Assert.AreEqual("#F0F0F0", lightStrong.MenuBackground);
            Assert.AreEqual("#000000", lightStrong.MenuForeground);
            Assert.AreEqual("#FFFFFF", lightStrong.TabItemActiveBackground);
            Assert.AreEqual("#222222", lightStrong.TabItemInactiveForeground);

            var darkStrong = themeService.ResolveEffectiveTheme(new AppSettings
            {
                ThemeMode = "Dark",
                ContrastMode = "Strong",
                DarkPresetId = "nord"
            });

            Assert.AreEqual("#0A0A0A", darkStrong.MenuBackground);
            Assert.AreEqual("#FFFFFF", darkStrong.MenuForeground);
            Assert.AreEqual("#000000", darkStrong.TabItemActiveBackground);
            Assert.AreEqual("#DCDCDC", darkStrong.TabItemInactiveForeground);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void SettingsViewModel_PreviewsCustomVariable_AndResetDefaultsPreservesSystemMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Settings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
            var themeService = new ThemeService(tempDir);
            var vm = new SettingsViewModel(settingsService, themeService);

            vm.CustomCodeVariable = "#FF5500";
            Assert.AreEqual("#FF5500", vm.SelectedTheme.CodeVariable);
            Assert.AreEqual(Color.Parse("#FF5500"), ((ISolidColorBrush)vm.VariableBrush).Color);

            vm.ThemeMode = "Light";
            vm.ResetDefaultsCommand.Execute(null);
            Assert.AreEqual("System", vm.ThemeMode);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void SearchViewModel_ReplaceToggleGlyph_TracksIsReplaceOpen()
    {
        var vm = new SearchViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.AreEqual("▶", vm.ReplaceToggleGlyph);
        vm.ToggleReplace();
        Assert.AreEqual("▼", vm.ReplaceToggleGlyph);
        CollectionAssert.Contains(changed, nameof(SearchViewModel.ReplaceToggleGlyph));
    }

    [TestMethod]
    public void WindowKeyboardRouter_Escape_ClosesCommandPaletteAndDiffView()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Keys_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = CreateMainViewModel(tempDir);
            var target = new StubKeyboardTarget();

            vm.ShowCommandPalette();
            vm.DiffViewModel.LoadDiff("old", "new");
            Assert.IsTrue(vm.IsCommandPaletteOpen);
            Assert.IsTrue(vm.DiffViewModel.IsActive);

            // First Esc closes Command Palette without closing Diff View underneath
            var esc1 = new KeyEventArgs { Key = Key.Escape };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(esc1, vm, target));
            Assert.IsFalse(vm.IsCommandPaletteOpen);
            Assert.IsTrue(vm.DiffViewModel.IsActive);

            // Second Esc closes Diff View
            var esc2 = new KeyEventArgs { Key = Key.Escape };
            Assert.IsTrue(WindowKeyboardRouter.HandleKeyDown(esc2, vm, target));
            Assert.IsFalse(vm.DiffViewModel.IsActive);
            Assert.AreEqual(2, target.FocusCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void MarkdownRenderer_LightForeground_UsesHighContrastInlinePalette()
    {
        var md = "- [x] Done with `inline` and [link](https://example.com)";
        var lightFg = new SolidColorBrush(Color.Parse("#101010"));

        var lightPanel = MarkdownRenderer.Render(md, foreground: lightFg) as StackPanel;
        Assert.IsNotNull(lightPanel);

        var grid = lightPanel.Children.OfType<Grid>().First();
        var checkIcon = grid.Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 0);
        var content = grid.Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 1);
        var runs = content.Inlines!.OfType<Run>().ToList();

        var inlineCode = runs.First(r => r.Text == "inline");
        var link = runs.First(r => r.Text == "link");

        var codeColor = ((ISolidColorBrush)inlineCode.Foreground!).Color;
        var linkColor = ((ISolidColorBrush)link.Foreground!).Color;
        var checkColor = ((ISolidColorBrush)checkIcon.Foreground!).Color;

        Assert.IsTrue(ContrastRatio($"#{codeColor.R:X2}{codeColor.G:X2}{codeColor.B:X2}", "#FFFFFF") >= 4.5);
        Assert.IsTrue(ContrastRatio($"#{linkColor.R:X2}{linkColor.G:X2}{linkColor.B:X2}", "#FFFFFF") >= 4.5);
        Assert.IsTrue(ContrastRatio($"#{checkColor.R:X2}{checkColor.G:X2}{checkColor.B:X2}", "#FFFFFF") >= 4.5);
    }

    [TestMethod]
    public async Task TabManager_AppliesSavedFontSize_AndUpdatesSavedPathBeforeReopen()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Tabs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dialog = new StubDialogService();
            var config = new AppConfig { DefaultFontSize = 14.0 };
            var vm = CreateMainViewModel(tempDir, dialog, config);

            await vm.SettingsService.SaveAsync(new AppSettings { FontSize = 19.0, WordWrap = true });
            vm.CreateNewDocument();
            Assert.IsNotNull(vm.ActiveDocument);
            Assert.AreEqual(19.0, vm.ActiveDocument.FontSize);

            var sampleFile = Path.Combine(tempDir, "sample.cs");
            await File.WriteAllTextAsync(sampleFile, "class C {}");
            await vm.OpenFileInternalAsync(sampleFile);
            Assert.AreEqual(19.0, vm.ActiveDocument!.FontSize);

            // Create modified untitled doc, close with Save -> saved target path must be stored for ReopenClosedTab
            vm.CreateNewDocument();
            var untitled = vm.ActiveDocument!;
            untitled.IsModified = true;
            var saveTarget = Path.Combine(tempDir, "saved_on_close.txt");
            dialog.NextSaveConfirmation = ConfirmResult.Save;
            dialog.NextSaveFilePath = saveTarget;

            await vm.CloseTabAsync(untitled);
            Assert.IsTrue(File.Exists(saveTarget));
            Assert.IsFalse(vm.Documents.Contains(untitled));

            await vm.TabManager.ReopenClosedTabAsync();
            Assert.IsTrue(vm.Documents.Any(d => string.Equals(d.FilePath, saveTarget, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task MainViewModel_ResolveModifiedDocumentBeforeCloseAsync_SavesWithoutClosingTab()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Exit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dialog = new StubDialogService();
            var vm = CreateMainViewModel(tempDir, dialog);
            var existing = Path.Combine(tempDir, "open.txt");
            await File.WriteAllTextAsync(existing, "initial");
            await vm.OpenFileInternalAsync(existing);

            var doc = vm.ActiveDocument!;
            doc.Model.Text = "updated";
            doc.IsModified = true;
            dialog.NextSaveConfirmation = ConfirmResult.Save;

            var ok = await vm.ResolveModifiedDocumentBeforeCloseAsync(doc);
            Assert.IsTrue(ok);
            Assert.IsFalse(doc.IsModified);
            Assert.IsTrue(vm.Documents.Contains(doc), "Saving before app close must keep the open tab in Documents for session persistence.");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task MainViewModel_ClosingSecondaryDocument_ClosesSplitView()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_Rc2_Split_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = CreateMainViewModel(tempDir);
            vm.CreateNewDocument();
            vm.CreateNewDocument();

            vm.ToggleSplitView();
            Assert.IsTrue(vm.IsSplitViewActive);
            var secondary = vm.SecondaryDocument;
            Assert.IsNotNull(secondary);

            await vm.CloseTabAsync(secondary);
            Assert.IsFalse(vm.IsSplitViewActive);
            Assert.IsNull(vm.SecondaryDocument);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
