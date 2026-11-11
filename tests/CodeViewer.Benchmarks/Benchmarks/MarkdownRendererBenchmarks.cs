using System.Collections.Generic;
using System.Text;
using Avalonia.Media;
using CodeViewer.Rendering;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class MarkdownRendererBenchmarks
{
    public static List<BenchmarkResult> Run(bool quick)
    {
        var results = new List<BenchmarkResult>();

        var smallMd = GenerateRichMarkdown(3);    // ~45 lines
        var mediumMd = GenerateRichMarkdown(35);  // ~525 lines
        var largeMd = GenerateRichMarkdown(160);  // ~2,400 lines

        results.Add(BenchmarkRunner.Measure(
            "4. Markdown Live Preview",
            "Render Rich Markdown (Small, ~45 lines)",
            quick ? 20 : 80,
            () => _ = MarkdownRenderer.Render(smallMd, Brushes.White, Brushes.Gray, Brushes.Black),
            warmupCount: 5,
            notes: "Headings, table, tasks, code, inline formatting"));

        results.Add(BenchmarkRunner.Measure(
            "4. Markdown Live Preview",
            "Render Rich Markdown (Medium, ~525 lines)",
            quick ? 5 : 20,
            () => _ = MarkdownRenderer.Render(mediumMd, Brushes.White, Brushes.Gray, Brushes.Black),
            warmupCount: 3,
            notes: "Typ. README / design doc live preview cost"));

        results.Add(BenchmarkRunner.Measure(
            "4. Markdown Live Preview",
            "Render Rich Markdown (Large, ~2,400 lines)",
            quick ? 2 : 6,
            () => _ = MarkdownRenderer.Render(largeMd, Brushes.White, Brushes.Gray, Brushes.Black),
            warmupCount: 1,
            notes: "Stress test: tables + code + RTL mixed blocks"));

        results.Add(BenchmarkRunner.Measure(
            "4. Markdown Live Preview",
            "IsRtlText Scan (1,000 strings batch)",
            quick ? 50 : 200,
            () =>
            {
                for (int i = 0; i < 500; i++)
                {
                    _ = MarkdownRenderer.IsRtlText("Normal English markdown heading text");
                    _ = MarkdownRenderer.IsRtlText("تیتر فارسی در سند مارک‌داون");
                }
            },
            warmupCount: 10,
            notes: "Bidirectional script detection hot loop"));

        return results;
    }

    private static string GenerateRichMarkdown(int repeatSections)
    {
        var sb = new StringBuilder();
        for (int s = 0; s < repeatSections; s++)
        {
            sb.AppendLine($"# Architecture Section {s}");
            sb.AppendLine("This paragraph demonstrates **bold statements**, *italic emphasis*, `inline.Code()`, [documentation](https://example.com), and ~~deprecated APIs~~.");
            sb.AppendLine();
            sb.AppendLine("سلام دنیا — پشتیبانی کامل از متون فارسی و راست‌به‌چپ در ویرایشگر.");
            sb.AppendLine();
            sb.AppendLine("> Key Insight: Synchronized live preview renders headings cleanly.");
            sb.AppendLine();
            sb.AppendLine("- [x] Implement parser optimizations");
            sb.AppendLine("- [ ] Profile layout allocations");
            sb.AppendLine("- Regular bullet item with *inline styling*");
            sb.AppendLine();
            sb.AppendLine("| Metric | Target | Current | Notes |");
            sb.AppendLine("|---|---|---|---|");
            sb.AppendLine("| Latency | < 16ms | 4.2ms | 60fps ready |");
            sb.AppendLine("| Memory | < 50MB | 18MB | GC-friendly |");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine("public void RunStep(int id) => Console.WriteLine(id);");
            sb.AppendLine("```");
            sb.AppendLine("---");
        }
        return sb.ToString();
    }
}
