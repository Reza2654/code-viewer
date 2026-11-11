using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class MarkdownRendererTests
{
    [TestMethod]
    public void Render_EmptyOrNull_ReturnsItalicEmptyLabel()
    {
        var panelEmpty = MarkdownRenderer.Render("") as StackPanel;
        Assert.IsNotNull(panelEmpty);
        var tbEmpty = panelEmpty.Children.OfType<TextBlock>().FirstOrDefault();
        Assert.IsNotNull(tbEmpty);
        Assert.AreEqual(FontStyle.Italic, tbEmpty.FontStyle);
        StringAssert.Contains(tbEmpty.Text, "Empty Markdown Document");

        var panelSpaces = MarkdownRenderer.Render("   \n   \t  ") as StackPanel;
        Assert.IsNotNull(panelSpaces);
        var tbSpaces = panelSpaces.Children.OfType<TextBlock>().FirstOrDefault();
        Assert.IsNotNull(tbSpaces);
        Assert.AreEqual(FontStyle.Italic, tbSpaces.FontStyle);
    }

    [TestMethod]
    public void Render_Headings_CreatesHeadingsH1ThroughH6()
    {
        var md = @"# Heading 1
## Heading 2
### Heading 3
#### Heading 4
##### Heading 5
###### Heading 6";

        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var headings = panel.Children.OfType<TextBlock>().ToList();
        Assert.AreEqual(6, headings.Count);

        Assert.AreEqual(22, headings[0].FontSize);
        Assert.AreEqual(FontWeight.Bold, headings[0].FontWeight);

        Assert.AreEqual(18, headings[1].FontSize);
        Assert.AreEqual(FontWeight.Bold, headings[1].FontWeight);

        Assert.AreEqual(15, headings[2].FontSize);
        Assert.AreEqual(FontWeight.SemiBold, headings[2].FontWeight);

        Assert.AreEqual(13, headings[3].FontSize);
        Assert.AreEqual(FontWeight.SemiBold, headings[3].FontWeight);

        Assert.AreEqual(12, headings[4].FontSize);
        Assert.AreEqual(FontWeight.SemiBold, headings[4].FontWeight);

        Assert.AreEqual(11, headings[5].FontSize);
        Assert.AreEqual(FontWeight.SemiBold, headings[5].FontWeight);
    }

    [TestMethod]
    public void Render_FencedCodeBlocks_CreatesBorderWithMonospaceScrollViewer()
    {
        var md = "```csharp\nvar x = 42;\nConsole.WriteLine(x);\n```";
        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var border = panel.Children.OfType<Border>().FirstOrDefault();
        Assert.IsNotNull(border);

        var scroller = border.Child as ScrollViewer;
        Assert.IsNotNull(scroller);

        var codeBlock = scroller.Content as TextBlock;
        Assert.IsNotNull(codeBlock);
        StringAssert.Contains(codeBlock.Text, "var x = 42;");
        StringAssert.Contains(codeBlock.Text, "Console.WriteLine(x);");
        Assert.AreEqual(12, codeBlock.FontSize);
    }

    [TestMethod]
    public void Render_UnclosedCodeBlock_HandlesGracefully()
    {
        var md = "```\nUnclosed code block line 1\nUnclosed line 2";
        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var border = panel.Children.OfType<Border>().FirstOrDefault();
        Assert.IsNotNull(border);

        var scroller = border.Child as ScrollViewer;
        Assert.IsNotNull(scroller);

        var codeBlock = scroller.Content as TextBlock;
        Assert.IsNotNull(codeBlock);
        StringAssert.Contains(codeBlock.Text, "Unclosed code block line 1");
    }

    [TestMethod]
    public void Render_HorizontalRules_CreatesBorderSeparators()
    {
        var md = "Above\n---\nMiddle\n***\nBelow\n___";
        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var borders = panel.Children.OfType<Border>().Where(b => b.Height == 1).ToList();
        Assert.AreEqual(3, borders.Count);
    }

    [TestMethod]
    public void Render_Blockquote_CreatesItalicBorderWithLeftIndicator()
    {
        var md = "> Note: This is an important quote.";
        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var border = panel.Children.OfType<Border>().FirstOrDefault();
        Assert.IsNotNull(border);
        Assert.AreEqual(3, border.BorderThickness.Left);

        var quoteTb = border.Child as TextBlock;
        Assert.IsNotNull(quoteTb);
        Assert.AreEqual(FontStyle.Italic, quoteTb.FontStyle);
    }

    [TestMethod]
    public void Render_BulletAndNumberedLists_CreatesListItems()
    {
        var md = @"- Bullet A
* Bullet B
1. First item
2. Second item";

        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var listGrids = panel.Children.OfType<Grid>().ToList();
        Assert.AreEqual(4, listGrids.Count);

        // Check bullet marks
        var bullets = listGrids.Select(g => g.Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 0).Text).ToList();
        Assert.AreEqual("•", bullets[0]);
        Assert.AreEqual("•", bullets[1]);
        Assert.AreEqual("1.", bullets[2]);
        Assert.AreEqual("2.", bullets[3]);
    }

    [TestMethod]
    public void Render_TaskLists_RendersCheckedAndUncheckedBoxes()
    {
        var md = @"- [ ] Incomplete task
- [x] Completed task
* [X] Another completed task";

        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var taskGrids = panel.Children.OfType<Grid>().ToList();
        Assert.AreEqual(3, taskGrids.Count);

        var box0 = taskGrids[0].Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 0);
        Assert.AreEqual("☐", box0.Text);

        var box1 = taskGrids[1].Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 0);
        Assert.AreEqual("☑", box1.Text);
        Assert.AreEqual(Brushes.LimeGreen, box1.Foreground);

        var content1 = taskGrids[1].Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 1);
        Assert.AreEqual(TextDecorations.Strikethrough, content1.TextDecorations);

        var box2 = taskGrids[2].Children.OfType<TextBlock>().First(tb => Grid.GetColumn(tb) == 0);
        Assert.AreEqual("☑", box2.Text);
    }

    [TestMethod]
    public void Render_Tables_RendersGridWithHeadersAndCells()
    {
        var md = @"| Header 1 | Header 2 |
|----------|----------|
| Val 1    | Val 2    |
| Val 3    | Val 4    |";

        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var outerBorder = panel.Children.OfType<Border>().FirstOrDefault(b => b.Child is Grid);
        Assert.IsNotNull(outerBorder);

        var tableGrid = outerBorder.Child as Grid;
        Assert.IsNotNull(tableGrid);

        Assert.AreEqual(2, tableGrid.ColumnDefinitions.Count);
        Assert.AreEqual(3, tableGrid.RowDefinitions.Count); // Header + 2 data rows

        var cellBorders = tableGrid.Children.OfType<Border>().ToList();
        Assert.AreEqual(6, cellBorders.Count); // 3 rows * 2 columns
    }

    [TestMethod]
    public void Render_Inlines_ParsesBoldItalicCodeLinkAndStrikethrough()
    {
        var md = "Here is **bold**, *italic*, `code`, [link](https://github.com), and ~~strike~~.";
        var panel = MarkdownRenderer.Render(md) as StackPanel;
        Assert.IsNotNull(panel);

        var paragraph = panel.Children.OfType<TextBlock>().FirstOrDefault();
        Assert.IsNotNull(paragraph);
        Assert.IsNotNull(paragraph.Inlines);

        var runs = paragraph.Inlines.OfType<Run>().ToList();
        Assert.IsTrue(runs.Count >= 5);

        var boldRun = runs.FirstOrDefault(r => r.Text == "bold");
        Assert.IsNotNull(boldRun);
        Assert.AreEqual(FontWeight.Bold, boldRun.FontWeight);

        var italicRun = runs.FirstOrDefault(r => r.Text == "italic");
        Assert.IsNotNull(italicRun);
        Assert.AreEqual(FontStyle.Italic, italicRun.FontStyle);

        var codeRun = runs.FirstOrDefault(r => r.Text == "code");
        Assert.IsNotNull(codeRun);

        var linkRun = runs.FirstOrDefault(r => r.Text == "link");
        Assert.IsNotNull(linkRun);
        Assert.AreEqual(TextDecorations.Underline, linkRun.TextDecorations);

        var strikeRun = runs.FirstOrDefault(r => r.Text == "strike");
        Assert.IsNotNull(strikeRun);
        Assert.AreEqual(TextDecorations.Strikethrough, strikeRun.TextDecorations);
    }

    [TestMethod]
    public void IsRtlText_DetectsPersianArabicHebrewAndLatinCorrectly()
    {
        Assert.IsTrue(MarkdownRenderer.IsRtlText("سلام دنیا"));
        Assert.IsTrue(MarkdownRenderer.IsRtlText("مرحبا بك في العالم"));
        Assert.IsTrue(MarkdownRenderer.IsRtlText("שלום עולם"));
        Assert.IsTrue(MarkdownRenderer.IsRtlText("123 فارسی text"));

        Assert.IsFalse(MarkdownRenderer.IsRtlText("Hello world"));
        Assert.IsFalse(MarkdownRenderer.IsRtlText("# Heading 1 123"));
        Assert.IsFalse(MarkdownRenderer.IsRtlText(""));
        Assert.IsFalse(MarkdownRenderer.IsRtlText(null!));
    }

    [TestMethod]
    public void MainViewModel_MarkdownPreview_StatsCalculation()
    {
        var langService = new LanguageService();
        var model = new DocumentModel
        {
            FilePath = "README.md",
            Title = "README.md",
            Text = "# Title\nThis is a sample document with several words."
        };
        var docVm = new DocumentViewModel(model, langService);

        var tempDir = Path.Combine(Path.GetTempPath(), "CV_MD_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new MainViewModel(
                new FileService(),
                langService,
                new RecentFilesService(10),
                new DialogService(),
                new AppConfig(),
                settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
                sessionService: new SessionService(Path.Combine(tempDir, "session.json")));

            vm.Documents.Add(docVm);
            vm.ActiveDocument = docVm;

            Assert.AreEqual("10 words • 2 lines", vm.MarkdownStatsDisplay);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void MainViewModel_CommandPalette_RegistersMarkdownCommands()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_MD_Cmd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new MainViewModel(
                new FileService(),
                new LanguageService(),
                new RecentFilesService(10),
                new DialogService(),
                new AppConfig(),
                settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
                sessionService: new SessionService(Path.Combine(tempDir, "session.json")));

            Assert.IsTrue(vm.CommandPalette.AllCommands.Any(c => c.Id == "view.toggleMarkdown"));
            Assert.IsTrue(vm.CommandPalette.AllCommands.Any(c => c.Id == "markdown.toggleScrollSync"));
            Assert.IsTrue(vm.CommandPalette.AllCommands.Any(c => c.Id == "markdown.copyText"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void MainViewModel_ToggleMarkdownScrollSync_TogglesFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CV_MD_Sync_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new MainViewModel(
                new FileService(),
                new LanguageService(),
                new RecentFilesService(10),
                new DialogService(),
                new AppConfig(),
                settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
                sessionService: new SessionService(Path.Combine(tempDir, "session.json")));

            Assert.IsTrue(vm.IsMarkdownScrollSyncEnabled);

            vm.ToggleMarkdownScrollSync();
            Assert.IsFalse(vm.IsMarkdownScrollSyncEnabled);

            vm.ToggleMarkdownScrollSync();
            Assert.IsTrue(vm.IsMarkdownScrollSyncEnabled);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task MainViewModel_CopyMarkdownPreviewAsync_InvokesClipboard()
    {
        var langService = new LanguageService();
        var model = new DocumentModel
        {
            FilePath = "guide.md",
            Title = "guide.md",
            Text = "# Header\nContent line"
        };
        var docVm = new DocumentViewModel(model, langService);

        var tempDir = Path.Combine(Path.GetTempPath(), "CV_MD_Copy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new MainViewModel(
                new FileService(),
                langService,
                new RecentFilesService(10),
                new DialogService(),
                new AppConfig(),
                settingsService: new SettingsService(Path.Combine(tempDir, "settings.json")),
                sessionService: new SessionService(Path.Combine(tempDir, "session.json")));

            vm.Documents.Add(docVm);
            vm.ActiveDocument = docVm;

            string? copied = null;
            vm.RequestSetClipboardText = text =>
            {
                copied = text;
                return Task.CompletedTask;
            };

            await vm.CopyMarkdownPreviewAsync();

            Assert.IsNotNull(copied);
            StringAssert.Contains(copied, "# Header");
            StringAssert.Contains(copied, "Content line");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
