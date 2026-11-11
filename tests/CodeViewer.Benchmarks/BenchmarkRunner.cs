using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeViewer.Benchmarks;

public record BenchmarkResult(
    string Category,
    string Name,
    int Iterations,
    int WarmupIterations,
    double MeanMs,
    double MedianMs,
    double P95Ms,
    double MinMs,
    double MaxMs,
    double OpsPerSec,
    long AllocatedBytesPerOp,
    int Gen0Count,
    int Gen1Count,
    int Gen2Count,
    string Notes = "");

public static class BenchmarkRunner
{
    public static BenchmarkResult Measure(
        string category,
        string name,
        int iterations,
        Action action,
        int warmupCount = 3,
        string notes = "")
    {
        for (int w = 0; w < warmupCount; w++)
        {
            action();
        }

        ForceFullGc();

        var durationsMs = new double[iterations];
        int g0Start = GC.CollectionCount(0);
        int g1Start = GC.CollectionCount(1);
        int g2Start = GC.CollectionCount(2);

        long totalAlloc = 0;
        double tickToMs = 1000.0 / Stopwatch.Frequency;

        for (int i = 0; i < iterations; i++)
        {
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();

            action();

            long end = Stopwatch.GetTimestamp();
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();

            durationsMs[i] = (end - start) * tickToMs;
            totalAlloc += Math.Max(0, allocAfter - allocBefore);
        }

        int g0Delta = Math.Max(0, GC.CollectionCount(0) - g0Start);
        int g1Delta = Math.Max(0, GC.CollectionCount(1) - g1Start);
        int g2Delta = Math.Max(0, GC.CollectionCount(2) - g2Start);

        return BuildResult(category, name, iterations, warmupCount, durationsMs, totalAlloc, g0Delta, g1Delta, g2Delta, notes);
    }

    public static async Task<BenchmarkResult> MeasureAsync(
        string category,
        string name,
        int iterations,
        Func<Task> funcAsync,
        int warmupCount = 2,
        string notes = "")
    {
        for (int w = 0; w < warmupCount; w++)
        {
            await funcAsync();
        }

        ForceFullGc();

        var durationsMs = new double[iterations];
        int g0Start = GC.CollectionCount(0);
        int g1Start = GC.CollectionCount(1);
        int g2Start = GC.CollectionCount(2);

        long totalAllocBefore = GC.GetTotalAllocatedBytes(precise: false);
        double tickToMs = 1000.0 / Stopwatch.Frequency;

        for (int i = 0; i < iterations; i++)
        {
            long start = Stopwatch.GetTimestamp();
            await funcAsync();
            long end = Stopwatch.GetTimestamp();

            durationsMs[i] = (end - start) * tickToMs;
        }

        long totalAllocAfter = GC.GetTotalAllocatedBytes(precise: false);
        long totalAllocated = Math.Max(0, totalAllocAfter - totalAllocBefore);

        int g0Delta = Math.Max(0, GC.CollectionCount(0) - g0Start);
        int g1Delta = Math.Max(0, GC.CollectionCount(1) - g1Start);
        int g2Delta = Math.Max(0, GC.CollectionCount(2) - g2Start);

        return BuildResult(category, name, iterations, warmupCount, durationsMs, totalAllocated, g0Delta, g1Delta, g2Delta, notes);
    }

    private static BenchmarkResult BuildResult(
        string category,
        string name,
        int iterations,
        int warmupCount,
        double[] durationsMs,
        long totalAllocatedBytes,
        int g0,
        int g1,
        int g2,
        string notes)
    {
        Array.Sort(durationsMs);

        double min = durationsMs[0];
        double max = durationsMs[^1];
        double mean = durationsMs.Average();
        double median = durationsMs.Length % 2 == 0
            ? (durationsMs[durationsMs.Length / 2 - 1] + durationsMs[durationsMs.Length / 2]) / 2.0
            : durationsMs[durationsMs.Length / 2];

        int p95Idx = Math.Clamp((int)Math.Ceiling(durationsMs.Length * 0.95) - 1, 0, durationsMs.Length - 1);
        double p95 = durationsMs[p95Idx];

        double opsPerSec = mean > 0.0001 ? 1000.0 / mean : 0;
        long allocPerOp = totalAllocatedBytes / Math.Max(1, iterations);

        return new BenchmarkResult(
            Category: category,
            Name: name,
            Iterations: iterations,
            WarmupIterations: warmupCount,
            MeanMs: mean,
            MedianMs: median,
            P95Ms: p95,
            MinMs: min,
            MaxMs: max,
            OpsPerSec: opsPerSec,
            AllocatedBytesPerOp: allocPerOp,
            Gen0Count: g0,
            Gen1Count: g1,
            Gen2Count: g2,
            Notes: notes);
    }

    private static void ForceFullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    public static string FormatDuration(double ms)
    {
        if (ms < 0.01) return $"{ms * 1000.0:F2} us";
        if (ms < 1.0) return $"{ms:F3} ms";
        if (ms < 100.0) return $"{ms:F2} ms";
        return $"{ms:F1} ms";
    }

    public static void PrintSummaryTable(IReadOnlyList<BenchmarkResult> results, TimeSpan totalElapsed)
    {
        Console.WriteLine();
        Console.WriteLine("========================================================================================================================");
        Console.WriteLine($" CodeViewer Profiling Report | .NET {Environment.Version} | {RuntimeInformation.OSDescription.Trim()} | {Environment.ProcessorCount} Logical CPUs");
        Console.WriteLine("========================================================================================================================");

        foreach (var group in results.GroupBy(r => r.Category))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{group.Key}]");
            Console.ResetColor();

            Console.WriteLine($"{"Benchmark",-42} | {"Mean",10} | {"P50",10} | {"P95",10} | {"Ops/s",9} | {"Alloc/op",10} | {"GC(0/1/2)",9}");
            Console.WriteLine(new string('-', 118));

            foreach (var r in group)
            {
                Console.Write($"{r.Name,-42} | ");

                Console.ForegroundColor = r.MeanMs < 1.0 ? ConsoleColor.Green : (r.MeanMs < 25.0 ? ConsoleColor.Yellow : ConsoleColor.Red);
                Console.Write($"{FormatDuration(r.MeanMs),10}");
                Console.ResetColor();

                Console.Write($" | {FormatDuration(r.MedianMs),10} | {FormatDuration(r.P95Ms),10} | ");
                Console.Write($"{(r.OpsPerSec >= 1000 ? $"{r.OpsPerSec / 1000.0:F1}k" : $"{r.OpsPerSec:F1}"),9} | ");

                Console.ForegroundColor = r.AllocatedBytesPerOp < 64 * 1024 ? ConsoleColor.Gray : (r.AllocatedBytesPerOp < 1024 * 1024 ? ConsoleColor.Yellow : ConsoleColor.Red);
                Console.Write($"{FormatBytes(r.AllocatedBytesPerOp),10}");
                Console.ResetColor();

                Console.WriteLine($" | {$"{r.Gen0Count}/{r.Gen1Count}/{r.Gen2Count}",9}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================================================================================================");
        Console.WriteLine($"Total Benchmarks: {results.Count} | Total Suite Runtime: {totalElapsed.TotalSeconds:F2}s");
        Console.WriteLine("========================================================================================================================");
    }

    public static void ExportMarkdownReport(string filePath, IReadOnlyList<BenchmarkResult> results, TimeSpan totalElapsed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# CodeViewer Performance & Profiling Report");
        sb.AppendLine();
        sb.AppendLine($"- **Date (UTC):** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
        sb.AppendLine($"- **Runtime:** .NET {Environment.Version}");
        sb.AppendLine($"- **OS:** {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"- **CPUs:** {Environment.ProcessorCount}");
        sb.AppendLine($"- **Total Runtime:** {totalElapsed.TotalSeconds:F2}s");
        sb.AppendLine();

        foreach (var group in results.GroupBy(r => r.Category))
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| Benchmark | Mean | Median (P50) | P95 | Ops/sec | Alloc/op | GC (0/1/2) | Notes |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");

            foreach (var r in group)
            {
                sb.AppendLine($"| {r.Name} | {FormatDuration(r.MeanMs)} | {FormatDuration(r.MedianMs)} | {FormatDuration(r.P95Ms)} | {r.OpsPerSec:N1} | {FormatBytes(r.AllocatedBytesPerOp)} | {r.Gen0Count}/{r.Gen1Count}/{r.Gen2Count} | {r.Notes} |");
            }

            sb.AppendLine();
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportJsonReport(string filePath, IReadOnlyList<BenchmarkResult> results, TimeSpan totalElapsed)
    {
        var payload = new
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DotNetVersion = Environment.Version.ToString(),
            OsDescription = RuntimeInformation.OSDescription.Trim(),
            LogicalCpuCount = Environment.ProcessorCount,
            TotalDurationSeconds = totalElapsed.TotalSeconds,
            Results = results
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }
}
