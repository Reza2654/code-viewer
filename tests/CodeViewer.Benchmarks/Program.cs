using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CodeViewer.Benchmarks.Benchmarks;

namespace CodeViewer.Benchmarks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        bool quick = false;
        string? filter = null;
        string? mdOut = null;
        string? jsonOut = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--quick":
                case "-q":
                    quick = true;
                    break;
                case "--filter":
                case "-f":
                    if (i + 1 < args.Length) filter = args[++i];
                    break;
                case "--md":
                    if (i + 1 < args.Length) mdOut = args[++i];
                    break;
                case "--json":
                    if (i + 1 < args.Length) jsonOut = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
            }
        }

        Console.WriteLine("====================================================================================");
        Console.WriteLine("  CodeViewer V1 — Comprehensive Performance & Memory Profiling Suite");
        Console.WriteLine($"  Mode: {(quick ? "Quick Smoke Run" : "Full Benchmark Run")}{(string.IsNullOrEmpty(filter) ? "" : $" | Filter: '{filter}'")}");
        Console.WriteLine("====================================================================================");
        Console.WriteLine();

        var tempRoot = Path.Combine(Path.GetTempPath(), "CodeViewerBenchmarks_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempRoot);

        var totalSw = Stopwatch.StartNew();
        var allResults = new List<BenchmarkResult>();

        try
        {
            if (MatchesFilter(filter, "file", "io", "encoding"))
            {
                Console.WriteLine("Running Suite 1/6: File I/O & Encoding Detection...");
                allResults.AddRange(await FileIoBenchmarks.RunAsync(tempRoot, quick));
            }

            if (MatchesFilter(filter, "theme", "syntax", "language"))
            {
                Console.WriteLine("Running Suite 2/6: Themes & Syntax Highlighting...");
                allResults.AddRange(ThemeAndSyntaxBenchmarks.Run(tempRoot, quick));
            }

            if (MatchesFilter(filter, "symbol", "outline", "ast"))
            {
                Console.WriteLine("Running Suite 3/6: Document Symbol Extraction...");
                allResults.AddRange(SymbolExtractionBenchmarks.Run(quick));
            }

            if (MatchesFilter(filter, "markdown", "preview", "render"))
            {
                Console.WriteLine("Running Suite 4/6: Markdown Live Preview Rendering...");
                allResults.AddRange(MarkdownRendererBenchmarks.Run(quick));
            }

            if (MatchesFilter(filter, "diff", "search", "palette", "workspace"))
            {
                Console.WriteLine("Running Suite 5/6: Diff Engine & Workspace Search...");
                allResults.AddRange(await DiffAndSearchBenchmarks.RunAsync(tempRoot, quick));
            }

            if (MatchesFilter(filter, "git", "editor", "bracket", "document"))
            {
                Console.WriteLine("Running Suite 6/6: Git & Editor Pipeline...");
                var repoDir = Directory.GetCurrentDirectory();
                allResults.AddRange(await GitAndEditorBenchmarks.RunAsync(repoDir, quick));
            }

            totalSw.Stop();

            BenchmarkRunner.PrintSummaryTable(allResults, totalSw.Elapsed);

            if (!string.IsNullOrWhiteSpace(mdOut))
            {
                BenchmarkRunner.ExportMarkdownReport(mdOut, allResults, totalSw.Elapsed);
                Console.WriteLine($"Markdown report written to: {Path.GetFullPath(mdOut)}");
            }

            if (!string.IsNullOrWhiteSpace(jsonOut))
            {
                BenchmarkRunner.ExportJsonReport(jsonOut, allResults, totalSw.Elapsed);
                Console.WriteLine($"JSON report written to: {Path.GetFullPath(jsonOut)}");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        return 0;
    }

    private static bool MatchesFilter(string? filter, params string[] tags)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        foreach (var tag in tags)
        {
            if (tag.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                filter.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: CodeViewer.Benchmarks [options]");
        Console.WriteLine("  --quick, -q           Run with fewer iterations for fast verification");
        Console.WriteLine("  --filter, -f <term>   Filter suite (file, theme, symbol, markdown, diff, git)");
        Console.WriteLine("  --md <filePath>       Export benchmark summary to Markdown");
        Console.WriteLine("  --json <filePath>     Export raw benchmark metrics to JSON");
    }
}
