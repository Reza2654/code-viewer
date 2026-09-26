using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public interface IScriptRunnerService
{
    bool IsSupported(string filePath);
    Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct);
}

public class ScriptRunnerService : IScriptRunnerService
{
    public bool IsSupported(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".agent" or ".ps2" or ".py" or ".ps1" or ".dart" or ".js" or ".sh" or ".bat" or ".cmd";
    }

    public async Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            onOutput($"[Error: File not found: {filePath}]");
            onCompleted(-1, 0);
            return;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string fileName;
        string arguments;

        switch (ext)
        {
            case ".agent":
                fileName = "agent";
                arguments = $"run \"{filePath}\"";
                break;
            case ".ps2":
                fileName = "ps2";
                arguments = $"run \"{filePath}\"";
                break;
            case ".py":
                fileName = "python";
                arguments = $"-u \"{filePath}\"";
                break;
            case ".ps1":
                fileName = "powershell";
                arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{filePath}\"";
                break;
            case ".dart":
                fileName = "dart";
                arguments = $"run \"{filePath}\"";
                break;
            case ".js":
                fileName = "node";
                arguments = $"\"{filePath}\"";
                break;
            case ".bat" or ".cmd":
                fileName = "cmd.exe";
                arguments = $"/c \"{filePath}\"";
                break;
            default:
                onOutput($"[Unsupported script format: {ext}]");
                onCompleted(-1, 0);
                return;
        }

        var workingDir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
        var sw = Stopwatch.StartNew();

        onOutput($"▶ Running: {fileName} {arguments}");
        onOutput($"📁 Working Directory: {workingDir}");
        onOutput("──────────────────────────────────────────────────────────");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) onOutput(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) onOutput($"[stderr] {e.Data}");
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                onOutput($"[Execution Failed: Could not start '{fileName}'. Ensure it is installed and added to PATH.]");
                onOutput($"[Details: {ex.Message}]");
                onCompleted(-1, sw.ElapsedMilliseconds);
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore kill errors
                }
            }))
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }

            sw.Stop();
            int exitCode = process.ExitCode;
            onOutput("──────────────────────────────────────────────────────────");
            onOutput($"⏹ Finished in {sw.ElapsedMilliseconds} ms (Exit Code {exitCode})");
            onCompleted(exitCode, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            onOutput("──────────────────────────────────────────────────────────");
            onOutput($"⏹ Terminated by user after {sw.ElapsedMilliseconds} ms");
            onCompleted(-1, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            onOutput($"[Execution Error: {ex.Message}]");
            onCompleted(-1, sw.ElapsedMilliseconds);
        }
    }
}
