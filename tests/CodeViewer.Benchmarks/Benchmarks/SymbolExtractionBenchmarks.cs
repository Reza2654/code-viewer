using System.Collections.Generic;
using System.Text;
using CodeViewer.Services;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class SymbolExtractionBenchmarks
{
    public static List<BenchmarkResult> Run(bool quick)
    {
        var results = new List<BenchmarkResult>();
        var service = new SymbolExtractionService();

        var csharp1k = GenerateCSharpCode(250);   // ~1,000 lines
        var csharp5k = GenerateCSharpCode(1250);  // ~5,000 lines
        var python1k = GeneratePythonCode(250);   // ~1,000 lines
        var js1k = GenerateJavaScriptCode(250);   // ~1,000 lines
        var md1k = GenerateMarkdownHeadings(1000);
        var json1k = GenerateJsonDocument(500);

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols C# (~1,000 lines)",
            quick ? 20 : 80,
            () => _ = service.ExtractSymbols(csharp1k, "csharp"),
            warmupCount: 5,
            notes: "Compiled regex + class/method/prop pass"));

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols C# (~5,000 lines)",
            quick ? 10 : 30,
            () => _ = service.ExtractSymbols(csharp5k, "csharp"),
            warmupCount: 3,
            notes: "Large source file outline scaling"));

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols Python (~1,000 lines)",
            quick ? 20 : 80,
            () => _ = service.ExtractSymbols(python1k, "python"),
            warmupCount: 5));

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols JS/TS (~1,000 lines)",
            quick ? 20 : 80,
            () => _ = service.ExtractSymbols(js1k, "typescript"),
            warmupCount: 5));

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols Markdown (1,000 lines)",
            quick ? 30 : 120,
            () => _ = service.ExtractSymbols(md1k, "markdown"),
            warmupCount: 5));

        results.Add(BenchmarkRunner.Measure(
            "3. Document Symbol Extraction",
            "ExtractSymbols JSON (1,000+ lines)",
            quick ? 20 : 80,
            () => _ = service.ExtractSymbols(json1k, "json"),
            warmupCount: 5));

        return results;
    }

    private static string GenerateCSharpCode(int methodBlocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace SampleApp.Services;");
        sb.AppendLine("public class OrderProcessor {");
        sb.AppendLine("    public string TenantId { get; set; } = string.Empty;");
        for (int i = 0; i < methodBlocks; i++)
        {
            sb.AppendLine($"    public async Task<bool> ProcessOrder{i}Async(int orderId, CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (orderId <= 0) return false;");
            sb.AppendLine("        return true;");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GeneratePythonCode(int blocks)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < blocks; i++)
        {
            sb.AppendLine($"class Worker{i}(BaseWorker):");
            sb.AppendLine("    def __init__(self, config):");
            sb.AppendLine("        self.config = config");
            sb.AppendLine($"    async def execute_{i}(self, payload: dict) -> bool:");
            sb.AppendLine("        return True");
        }
        return sb.ToString();
    }

    private static string GenerateJavaScriptCode(int blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("export class Controller {");
        for (int i = 0; i < blocks; i++)
        {
            sb.AppendLine($"  async handleRequest{i}(req, res) {{");
            sb.AppendLine("    const data = await fetchData(req.id);");
            sb.AppendLine("    return res.json(data);");
            sb.AppendLine("  }");
        }
        sb.AppendLine("}");
        sb.AppendLine("export const helperFn = (x) => x * 2;");
        return sb.ToString();
    }

    private static string GenerateMarkdownHeadings(int totalLines)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < totalLines; i++)
        {
            if (i % 10 == 0)
                sb.AppendLine($"## Section Header {i}");
            else if (i % 5 == 0)
                sb.AppendLine($"### Subsection {i}");
            else
                sb.AppendLine("Regular markdown paragraph content without headings.");
        }
        return sb.ToString();
    }

    private static string GenerateJsonDocument(int entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        for (int i = 0; i < entries; i++)
        {
            var comma = i < entries - 1 ? "," : "";
            sb.AppendLine($"  \"setting_{i}\": {{");
            sb.AppendLine($"    \"enabled\": true,");
            sb.AppendLine($"    \"timeoutMs\": {1000 + i}");
            sb.AppendLine($"  }}{comma}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}
