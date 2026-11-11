using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class DiffAndSearchBenchmarks
{
    public static async Task<List<BenchmarkResult>> RunAsync(string tempRoot, bool quick)
    {
        var results = new List<BenchmarkResult>();
        var diffService = new DiffService();

        var (oldSmall, newSmall) = GenerateDiffPair(400);
        var (oldLarge, newLarge) = GenerateDiffPair(3500);

        results.Add(BenchmarkRunner.Measure(
            "5. Diff Engine & Workspace Search",
            "DiffService LCS (400 lines, 5% diff)",
            quick ? 10 : 40,
            () => _ = diffService.ComputeDiff(oldSmall, newSmall),
            warmupCount: 3,
            notes: "Dynamic programming LCS path"));

        results.Add(BenchmarkRunner.Measure(
            "5. Diff Engine & Workspace Search",
            "DiffService Myers Fallback (3,500 lines)",
            quick ? 3 : 10,
            () => _ = diffService.ComputeDiff(oldLarge, newLarge),
            warmupCount: 2,
            notes: "Large file greedy diff path"));

        // Global workspace search benchmark across 80 realistic files
        var searchRoot = Path.Combine(tempRoot, "search_ws");
        CreateSampleWorkspace(searchRoot, fileCount: 80);

        var searchService = new WorkspaceSearchService();

        results.Add(await BenchmarkRunner.MeasureAsync(
            "5. Diff Engine & Workspace Search",
            "WorkspaceSearch Plain (80 files)",
            quick ? 4 : 12,
            async () => _ = await searchService.SearchAsync(searchRoot, "ExecuteTaskAsync", matchCase: false, wholeWord: false),
            warmupCount: 2,
            notes: "Parallel file scan + OrdinalIgnoreCase"));

        results.Add(await BenchmarkRunner.MeasureAsync(
            "5. Diff Engine & Workspace Search",
            "WorkspaceSearch WholeWord Regex (80 files)",
            quick ? 4 : 12,
            async () => _ = await searchService.SearchAsync(searchRoot, "Handler", matchCase: true, wholeWord: true),
            warmupCount: 2,
            notes: "Compiled word-boundary regex across workspace"));

        // Command Palette search filtering
        var palette = new CommandPaletteCoordinator();
        for (int i = 0; i < 60; i++)
        {
            palette.RegisterCommand(new CommandPaletteItem(
                $"cmd.{i}",
                $"Editor Action: Format & Align Selection Block {i}",
                i % 3 == 0 ? "File" : (i % 3 == 1 ? "View" : "Markdown"),
                $"Ctrl+Alt+{i % 10}",
                "⚡",
                () => { }));
        }

        results.Add(BenchmarkRunner.Measure(
            "5. Diff Engine & Workspace Search",
            "CommandPalette Filter (60 cmds)",
            quick ? 100 : 400,
            () =>
            {
                palette.Filter("markdown");
                palette.Filter("align");
                palette.Filter("");
            },
            warmupCount: 15,
            notes: "3 queries per op"));

        return results;
    }

    private static (string OldText, string NewText) GenerateDiffPair(int lineCount)
    {
        var oldSb = new StringBuilder();
        var newSb = new StringBuilder();

        for (int i = 0; i < lineCount; i++)
        {
            var baseLine = $"public void Method_{i}(int value) => Process(value + {i});";
            oldSb.AppendLine(baseLine);

            if (i % 20 == 0)
            {
                newSb.AppendLine($"public void Method_{i}_Updated(int value, bool flag) => Process(value + {i});");
            }
            else if (i % 45 != 0)
            {
                newSb.AppendLine(baseLine);
            }
        }

        return (oldSb.ToString(), newSb.ToString());
    }

    private static void CreateSampleWorkspace(string rootDir, int fileCount)
    {
        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(Path.Combine(rootDir, "bin"));
        Directory.CreateDirectory(Path.Combine(rootDir, ".git"));
        File.WriteAllText(Path.Combine(rootDir, "bin", "ignored.dll"), "ExecuteTaskAsync Handler");

        var contentTemplate = new StringBuilder();
        for (int l = 0; l < 120; l++)
        {
            contentTemplate.AppendLine($"    public async Task ExecuteTaskAsync(int step) {{ var x = new Handler(); }} // line {l}");
        }
        var content = contentTemplate.ToString();

        for (int i = 0; i < fileCount; i++)
        {
            var subDir = Path.Combine(rootDir, $"Module_{i % 5}");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, $"Service_{i}.cs"), content);
        }
    }
}
