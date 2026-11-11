using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class GitAndEditorBenchmarks
{
    public static async Task<List<BenchmarkResult>> RunAsync(string repoCandidateDir, bool quick)
    {
        var results = new List<BenchmarkResult>();
        var langService = new LanguageService();
        var gitService = new GitService();

        // Batch parsing 1,000 porcelain lines
        var porcelainLines = new string[]
        {
            " M src/CodeViewer/ViewModels/MainViewModel.cs",
            "M  src/CodeViewer/Views/MainWindow.axaml",
            "A  src/CodeViewer/Services/GitService.cs",
            "?? tests/CodeViewer.Tests/MarkdownRendererTests.cs",
            " D old/obsolete_file.txt",
            "R  src/OldName.cs -> src/NewName.cs",
            "?? \"docs/path with spaces/readme.md\""
        };

        results.Add(BenchmarkRunner.Measure(
            "6. Git & Editor Pipeline",
            "GitService.ParsePorcelainLine (1k lines)",
            quick ? 50 : 200,
            () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    _ = GitService.ParsePorcelainLine(porcelainLines[i % porcelainLines.Length]);
                }
            },
            warmupCount: 10,
            notes: "Zero-process porcelain parser throughput"));

        if (Directory.Exists(repoCandidateDir))
        {
            results.Add(await BenchmarkRunner.MeasureAsync(
                "6. Git & Editor Pipeline",
                "GitService.GetStatusAsync (Live Repo)",
                quick ? 2 : 5,
                async () => _ = await gitService.GetStatusAsync(repoCandidateDir),
                warmupCount: 1,
                notes: "Async git status --porcelain=v1 process"));
        }

        // DocumentViewModel creation (TextDocument + Language + Metadata)
        var sampleText = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sampleText.AppendLine($"var item_{i} = ComputeValue({i});");
        }
        var sampleStr = sampleText.ToString();

        results.Add(BenchmarkRunner.Measure(
            "6. Git & Editor Pipeline",
            "DocumentViewModel Init (1,000 lines C#)",
            quick ? 10 : 40,
            () =>
            {
                var model = new DocumentModel
                {
                    FilePath = "TestFile.cs",
                    Title = "TestFile.cs",
                    Text = sampleStr
                };
                _ = new DocumentViewModel(model, langService);
            },
            warmupCount: 3,
            notes: "TextDocument allocation + syntax binding"));

        // EditorTextManipulator.ToggleComment on 1,000 lines
        var doc = new TextDocument(sampleStr);
        results.Add(BenchmarkRunner.Measure(
            "6. Git & Editor Pipeline",
            "ToggleComment (1,000 lines)",
            quick ? 5 : 20,
            () => EditorTextManipulator.ToggleComment(doc, 0, doc.TextLength, "// "),
            warmupCount: 2,
            notes: "UndoGroup batch line prefix toggle"));

        // SmartBracketEditHandler ProcessCharInput
        var bracketDoc = new TextDocument("var fn = ");
        results.Add(BenchmarkRunner.Measure(
            "6. Git & Editor Pipeline",
            "SmartBracketEditHandler (1k keystrokes)",
            quick ? 50 : 200,
            () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    bracketDoc.Text = "var fn = ";
                    SmartBracketEditHandler.ProcessCharInput(bracketDoc, '(', 0, 0, 9, out _, out _, out _);
                }
            },
            warmupCount: 10,
            notes: "Auto-closing pair insertion latency"));

        return results;
    }
}
