using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class FileIoBenchmarks
{
    public static async Task<List<BenchmarkResult>> RunAsync(string tempRoot, bool quick)
    {
        var results = new List<BenchmarkResult>();
        var fileService = new FileService();

        var ioDir = Path.Combine(tempRoot, "io");
        Directory.CreateDirectory(ioDir);

        var smallPath = Path.Combine(ioDir, "small_10kb.cs");
        var mediumPath = Path.Combine(ioDir, "medium_1mb.dart");
        var largePath = Path.Combine(ioDir, "large_10mb.txt");
        var writePath = Path.Combine(ioDir, "save_target_500kb.cs");

        GenerateSourceFile(smallPath, 10 * 1024);
        GenerateSourceFile(mediumPath, 1024 * 1024);
        GenerateSourceFile(largePath, 10 * 1024 * 1024);

        results.Add(await BenchmarkRunner.MeasureAsync(
            "1. File I/O & Encoding",
            "OpenFileAsync (10 KB C#)",
            quick ? 10 : 30,
            async () => _ = await fileService.OpenFileAsync(smallPath),
            warmupCount: 3,
            notes: "UTF-8 seq read + line-ending scan"));

        results.Add(await BenchmarkRunner.MeasureAsync(
            "1. File I/O & Encoding",
            "OpenFileAsync (1 MB Dart)",
            quick ? 5 : 15,
            async () => _ = await fileService.OpenFileAsync(mediumPath),
            warmupCount: 2,
            notes: "64KB buffered stream"));

        results.Add(await BenchmarkRunner.MeasureAsync(
            "1. File I/O & Encoding",
            "OpenFileAsync (10 MB Text)",
            quick ? 3 : 6,
            async () => _ = await fileService.OpenFileAsync(largePath),
            warmupCount: 1,
            notes: "Large file cold/warm memory profile"));

        var docToSave = await fileService.OpenFileAsync(smallPath);
        docToSave.Text = File.ReadAllText(mediumPath)[..(500 * 1024)];

        results.Add(await BenchmarkRunner.MeasureAsync(
            "1. File I/O & Encoding",
            "SaveFileAsync (500 KB atomic write)",
            quick ? 3 : 8,
            async () => await fileService.SaveFileAsync(docToSave, writePath),
            warmupCount: 2,
            notes: "Temp file + atomic File.Move"));

        return results;
    }

    private static void GenerateSourceFile(string path, long targetBytes)
    {
        const string linePattern = "    void ProcessItem(int index) => Console.WriteLine($\"Item: {index}\");\r\n";
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
