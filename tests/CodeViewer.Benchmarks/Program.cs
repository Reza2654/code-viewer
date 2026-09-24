using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeViewer.Services;

namespace CodeViewer.Benchmarks;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  Code Viewer V1 - Performance Benchmark Harness ");
        Console.WriteLine("=================================================");
        Console.WriteLine();

        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewerBenchmarks");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var smallFilePath = Path.Combine(tempDir, "small_file.cs");
        var mediumFilePath = Path.Combine(tempDir, "medium_1mb.dart");
        var largeFilePath = Path.Combine(tempDir, "large_10mb.txt");

        try
        {
            Console.WriteLine("Generating synthetic benchmark files...");
            GenerateSourceFile(smallFilePath, 10 * 1024);         // 10 KB
            GenerateSourceFile(mediumFilePath, 1024 * 1024);      // 1 MB
            GenerateSourceFile(largeFilePath, 10 * 1024 * 1024);  // 10 MB
            Console.WriteLine("Files generated successfully.");
            Console.WriteLine();

            var fileService = new FileService();
            var languageService = new LanguageService();

            // 1. Benchmark Small File (10 KB)
            await BenchmarkFileAsync("Small File (10 KB, C#)", smallFilePath, fileService, languageService);

            // 2. Benchmark Medium File (1 MB)
            await BenchmarkFileAsync("Medium File (1 MB, Dart)", mediumFilePath, fileService, languageService);

            // 3. Benchmark Large File (10 MB)
            await BenchmarkFileAsync("Large File (10 MB, Text)", largeFilePath, fileService, languageService);

            Console.WriteLine();
            Console.WriteLine("=================================================");
            Console.WriteLine("Benchmark completed.");
            Console.WriteLine("=================================================");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private static async Task BenchmarkFileAsync(
        string testName,
        string filePath,
        IFileService fileService,
        ILanguageService languageService)
    {
        Console.WriteLine($"--- Testing: {testName} ---");

        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"Size on disk: {fileInfo.Length:N0} bytes");

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memBefore = GC.GetTotalMemory(true);

        // Cold Read Measurement
        var sw = Stopwatch.StartNew();
        var doc = await fileService.OpenFileAsync(filePath);
        sw.Stop();
        var coldOpenMs = sw.ElapsedMilliseconds;

        // Language Detection & Highlighting Resolution
        sw.Restart();
        var lang = languageService.DetectLanguage(filePath);
        var def = languageService.GetHighlightingDefinition(lang);
        sw.Stop();
        var langDetectionMs = sw.Elapsed.TotalMilliseconds;

        // Warm Read Measurement (cache / JIT warmed)
        sw.Restart();
        var warmDoc = await fileService.OpenFileAsync(filePath);
        sw.Stop();
        var warmOpenMs = sw.ElapsedMilliseconds;

        var memAfter = GC.GetTotalMemory(false);
        var memDelta = Math.Max(0, memAfter - memBefore);

        Console.WriteLine($"Language: {lang} | Highlighting: {(def != null ? "Loaded" : "None")}");
        Console.WriteLine($"Detected Encoding: {doc.EncodingInfo.DisplayName} (BOM: {doc.EncodingInfo.HasBom})");
        Console.WriteLine($"Detected Line Endings: {doc.LineEnding}");
        Console.WriteLine($"Cold Open Time: {coldOpenMs} ms");
        Console.WriteLine($"Warm Open Time: {warmOpenMs} ms");
        Console.WriteLine($"Language & Syntax Resolution: {langDetectionMs:F3} ms");
        Console.WriteLine($"Allocated Memory: {memDelta / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();
    }

    private static void GenerateSourceFile(string path, long targetBytes)
    {
        var linePattern = "    void ProcessItem(int index) => Console.WriteLine($\"Item: {index}\");\r\n";
        var lineBytes = Encoding.UTF8.GetByteCount(linePattern);
        var lineCount = targetBytes / lineBytes;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var writer = new StreamWriter(fs, new UTF8Encoding(false));

        writer.WriteLine("// Auto-generated benchmark code file");
        writer.WriteLine("namespace BenchmarkNamespace;");
        writer.WriteLine("public class BenchmarkClass {");

        for (int i = 0; i < lineCount; i++)
        {
            writer.Write(linePattern);
        }

        writer.WriteLine("}");
    }
}
