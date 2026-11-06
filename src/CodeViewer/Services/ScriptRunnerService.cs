using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public enum ShellType
{
    Auto,
    PowerShell,
    CommandPrompt,
    WslBash,
    Python,
    Node
}

public sealed record ShellOption(ShellType Type, string DisplayName, string Description)
{
    public override string ToString() => DisplayName;

    public static readonly IReadOnlyList<ShellOption> All = new[]
    {
        new ShellOption(ShellType.Auto, "Auto (File Default)", "Detects runtime based on file extension"),
        new ShellOption(ShellType.PowerShell, "PowerShell", "PowerShell (pwsh / powershell.exe)"),
        new ShellOption(ShellType.CommandPrompt, "Command Prompt", "Windows Command Prompt (cmd.exe)"),
        new ShellOption(ShellType.WslBash, "WSL / Bash", "Bash shell (wsl / bash)"),
        new ShellOption(ShellType.Python, "Python", "Python interpreter (python / python3)"),
        new ShellOption(ShellType.Node, "Node.js", "Node.js JavaScript runtime (node)")
    };
}

public interface IScriptRunnerService
{
    bool IsSupported(string filePath);
    Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct);
    Task RunAsync(string filePath, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
        => RunAsync(filePath, onOutput, onCompleted, ct);
    Task RunCommandAsync(string command, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
        => Task.CompletedTask;
    void SendInput(string text);
    ProcessStartInfo CreateStartInfo(string filePath, ShellType shellType = ShellType.Auto, string? workingDir = null)
        => new ProcessStartInfo();
    ProcessStartInfo CreateCommandStartInfo(string command, ShellType shellType = ShellType.Auto, string? workingDir = null)
        => new ProcessStartInfo();
}

public class ScriptRunnerService : IScriptRunnerService
{
    private StreamWriter? _currentInput;
    private readonly object _inputLock = new();

    public void SendInput(string text)
    {
        lock (_inputLock)
        {
            if (_currentInput != null)
            {
                try
                {
                    _currentInput.WriteLine(text);
                    _currentInput.Flush();
                }
                catch
                {
                    // Ignore if process has exited or closed stdin
                }
            }
        }
    }

    public bool IsSupported(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".agent" or ".ps2" or ".py" or ".ps1" or ".dart" or ".js" or ".sh" or ".bat" or ".cmd";
    }

    public static string ResolvePowerShell()
    {
        return OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
    }

    public static string ResolvePython()
    {
        return "python";
    }

    public static string ResolveBash()
    {
        return "bash";
    }

    public ProcessStartInfo CreateStartInfo(string filePath, ShellType shellType = ShellType.Auto, string? workingDir = null)
    {
        string fileName;
        string arguments;

        if (shellType == ShellType.Auto)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
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
                    fileName = ResolvePython();
                    arguments = $"-u \"{filePath}\"";
                    break;
                case ".ps1":
                    fileName = ResolvePowerShell();
                    arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{filePath}'\"";
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
                    arguments = $"/c chcp 65001 >nul && \"{filePath}\"";
                    break;
                case ".sh":
                    fileName = ResolveBash();
                    arguments = $"\"{filePath}\"";
                    break;
                default:
                    throw new NotSupportedException($"Unsupported script format: {ext}");
            }
        }
        else
        {
            switch (shellType)
            {
                case ShellType.PowerShell:
                    fileName = ResolvePowerShell();
                    arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '{filePath}'\"";
                    break;
                case ShellType.CommandPrompt:
                    fileName = "cmd.exe";
                    arguments = $"/c chcp 65001 >nul && \"{filePath}\"";
                    break;
                case ShellType.WslBash:
                    fileName = ResolveBash();
                    arguments = $"\"{filePath}\"";
                    break;
                case ShellType.Python:
                    fileName = ResolvePython();
                    arguments = $"-u \"{filePath}\"";
                    break;
                case ShellType.Node:
                    fileName = "node";
                    arguments = $"\"{filePath}\"";
                    break;
                default:
                    throw new NotSupportedException($"Unsupported shell type: {shellType}");
            }
        }

        if (string.IsNullOrEmpty(workingDir))
        {
            workingDir = Path.GetDirectoryName(filePath);
        }
        if (string.IsNullOrEmpty(workingDir))
        {
            workingDir = Environment.CurrentDirectory;
        }

        return ConfigureStartInfo(fileName, arguments, workingDir);
    }

    public ProcessStartInfo CreateCommandStartInfo(string command, ShellType shellType = ShellType.Auto, string? workingDir = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or whitespace.", nameof(command));
        }

        if (shellType == ShellType.Auto)
        {
            shellType = OperatingSystem.IsWindows() ? ShellType.PowerShell : ShellType.WslBash;
        }

        string fileName;
        string arguments;

        switch (shellType)
        {
            case ShellType.PowerShell:
                fileName = ResolvePowerShell();
                arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command}\"";
                break;
            case ShellType.CommandPrompt:
                fileName = "cmd.exe";
                arguments = $"/c chcp 65001 >nul && {command}";
                break;
            case ShellType.WslBash:
                fileName = ResolveBash();
                var escapedBash = command.Replace("\"", "\\\"");
                arguments = $"-c \"{escapedBash}\"";
                break;
            case ShellType.Python:
                fileName = ResolvePython();
                var escapedPy = command.Replace("\"", "\\\"");
                arguments = $"-u -c \"{escapedPy}\"";
                break;
            case ShellType.Node:
                fileName = "node";
                var escapedNode = command.Replace("\"", "\\\"");
                arguments = $"-e \"{escapedNode}\"";
                break;
            default:
                throw new NotSupportedException($"Unsupported shell type: {shellType}");
        }

        if (string.IsNullOrEmpty(workingDir))
        {
            workingDir = Environment.CurrentDirectory;
        }

        return ConfigureStartInfo(fileName, arguments, workingDir);
    }

    private static ProcessStartInfo ConfigureStartInfo(string fileName, string arguments, string workingDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Guarantee child runtimes use UTF-8 output streams
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["LANG"] = "en_US.UTF-8";
        startInfo.Environment["LC_ALL"] = "en_US.UTF-8";

        return startInfo;
    }

    public Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
    {
        return RunAsync(filePath, ShellType.Auto, null, onOutput, onCompleted, ct);
    }

    public async Task RunAsync(string filePath, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            onOutput($"[Error: File not found: {filePath}]");
            onCompleted(-1, 0);
            return;
        }

        ProcessStartInfo startInfo;
        try
        {
            startInfo = CreateStartInfo(filePath, shellType, workingDir);
        }
        catch (NotSupportedException ex)
        {
            onOutput($"[{ex.Message}]");
            onCompleted(-1, 0);
            return;
        }

        await ExecuteProcessAsync(startInfo, $"{startInfo.FileName} {startInfo.Arguments}", onOutput, onCompleted, ct);
    }

    public async Task RunCommandAsync(string command, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
    {
        ProcessStartInfo startInfo;
        try
        {
            startInfo = CreateCommandStartInfo(command, shellType, workingDir);
        }
        catch (Exception ex)
        {
            onOutput($"[{ex.Message}]");
            onCompleted(-1, 0);
            return;
        }

        await ExecuteProcessAsync(startInfo, command, onOutput, onCompleted, ct);
    }

    private async Task ExecuteProcessAsync(ProcessStartInfo startInfo, string displayCommand, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        onOutput($"▶ Running: {displayCommand}");
        onOutput($"📁 Working Directory: {startInfo.WorkingDirectory}");
        onOutput("──────────────────────────────────────────────────────────");

        try
        {
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
                lock (_inputLock)
                {
                    _currentInput = process.StandardInput;
                }
            }
            catch (Exception ex)
            {
                onOutput($"[Execution Failed: Could not start '{startInfo.FileName}'. Ensure it is installed and added to PATH.]");
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
        finally
        {
            lock (_inputLock)
            {
                _currentInput = null;
            }
        }
    }
}
