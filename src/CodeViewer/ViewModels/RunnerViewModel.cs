using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

public partial class RunnerViewModel : ViewModelBase
{
    private readonly IScriptRunnerService _runnerService;
    private CancellationTokenSource? _runCts;
    private readonly StringBuilder _outputBuffer = new();
    private const int MaxOutputLength = 100_000;

    private readonly List<string> _commandHistory = [];
    private int _historyIndex = 0;
    private string _stashedCurrentInput = string.Empty;

    public IReadOnlyList<ShellOption> AvailableShells => ShellOption.All;

    [ObservableProperty]
    private ShellOption _selectedShell = ShellOption.All[0];

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isRunning;

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(InputPlaceholder));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    public string InputPlaceholder => IsRunning
        ? "Send input to running process (stdin)..."
        : "Enter command (e.g., git status, dotnet test, dir)...";

    public string ActionButtonText => IsRunning ? "Send" : "Run";

    [ObservableProperty]
    private string _scriptPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _currentInput = string.Empty;

    public Action? RequestScrollToEnd { get; set; }

    public Func<string?>? GetWorkingDirectory { get; set; }

    public RunnerViewModel(IScriptRunnerService? runnerService = null)
    {
        _runnerService = runnerService ?? new ScriptRunnerService();
    }

    [RelayCommand]
    public async Task SubmitInputAsync()
    {
        var input = CurrentInput?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        CurrentInput = string.Empty;

        if (IsRunning)
        {
            AppendOutput($"> {input}");
            _runnerService.SendInput(input);
        }
        else
        {
            RecordCommandHistory(input);
            await RunCommandAsync(input);
        }
    }

    [RelayCommand]
    public void SendInput()
    {
        _ = SubmitInputAsync();
    }

    [RelayCommand]
    public void ClearOutput()
    {
        _outputBuffer.Clear();
        OutputText = string.Empty;
        StatusText = IsRunning ? "Running..." : "Ready";
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void Cancel()
    {
        if (IsRunning && _runCts != null)
        {
            StatusText = "Stopping...";
            _runCts.Cancel();
        }
    }

    public void RecordCommandHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        if (_commandHistory.Count == 0 || !_commandHistory[^1].Equals(command, StringComparison.Ordinal))
        {
            _commandHistory.Add(command);
        }
        _historyIndex = _commandHistory.Count;
    }

    public bool NavigateHistory(int direction, out string historyCommand)
    {
        historyCommand = string.Empty;
        if (_commandHistory.Count == 0) return false;

        if (_historyIndex == _commandHistory.Count)
        {
            _stashedCurrentInput = CurrentInput ?? string.Empty;
        }

        var newIndex = _historyIndex + direction;
        if (newIndex < 0) newIndex = 0;
        if (newIndex > _commandHistory.Count) newIndex = _commandHistory.Count;

        if (newIndex == _historyIndex) return false;

        _historyIndex = newIndex;
        if (_historyIndex == _commandHistory.Count)
        {
            historyCommand = _stashedCurrentInput;
        }
        else
        {
            historyCommand = _commandHistory[_historyIndex];
        }
        return true;
    }

    public async Task RunScriptAsync(string filePath, string? workingDir = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (IsRunning)
        {
            Cancel();
            await Task.Delay(200);
        }

        ScriptPath = filePath;
        IsVisible = true;
        IsRunning = true;
        StatusText = $"Running {System.IO.Path.GetFileName(filePath)}...";

        _outputBuffer.Clear();
        OutputText = string.Empty;

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        var resolvedWorkingDir = workingDir
            ?? GetWorkingDirectory?.Invoke()
            ?? System.IO.Path.GetDirectoryName(filePath)
            ?? Environment.CurrentDirectory;

        try
        {
            await _runnerService.RunAsync(
                filePath,
                SelectedShell.Type,
                resolvedWorkingDir,
                line => PostToUiThread(() => AppendOutput(line)),
                (exitCode, durationMs) => PostToUiThread(() =>
                {
                    IsRunning = false;
                    StatusText = exitCode == 0
                        ? $"✓ Completed in {durationMs} ms"
                        : $"⏹ Exited with code {exitCode} in {durationMs} ms";
                }),
                token);
        }
        catch (OperationCanceledException)
        {
            PostToUiThread(() =>
            {
                IsRunning = false;
                StatusText = "⏹ Cancelled by user";
                AppendOutput("[Script execution stopped by user]");
            });
        }
        catch (Exception ex)
        {
            PostToUiThread(() =>
            {
                IsRunning = false;
                StatusText = $"❌ Failed: {ex.Message}";
                AppendOutput($"[Error: {ex.Message}]");
            });
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    public async Task RunCommandAsync(string command, string? workingDir = null)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        if (IsRunning)
        {
            Cancel();
            await Task.Delay(200);
        }

        IsVisible = true;
        IsRunning = true;
        StatusText = $"Running in {SelectedShell.DisplayName}...";

        _outputBuffer.Clear();
        OutputText = string.Empty;

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        var resolvedWorkingDir = workingDir
            ?? GetWorkingDirectory?.Invoke()
            ?? Environment.CurrentDirectory;

        try
        {
            await _runnerService.RunCommandAsync(
                command,
                SelectedShell.Type,
                resolvedWorkingDir,
                line => PostToUiThread(() => AppendOutput(line)),
                (exitCode, durationMs) => PostToUiThread(() =>
                {
                    IsRunning = false;
                    StatusText = exitCode == 0
                        ? $"✓ Completed in {durationMs} ms"
                        : $"⏹ Exited with code {exitCode} in {durationMs} ms";
                }),
                token);
        }
        catch (OperationCanceledException)
        {
            PostToUiThread(() =>
            {
                IsRunning = false;
                StatusText = "⏹ Cancelled by user";
                AppendOutput("[Command execution stopped by user]");
            });
        }
        catch (Exception ex)
        {
            PostToUiThread(() =>
            {
                IsRunning = false;
                StatusText = $"❌ Failed: {ex.Message}";
                AppendOutput($"[Error: {ex.Message}]");
            });
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void PostToUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private void AppendOutput(string line)
    {
        if (_outputBuffer.Length > MaxOutputLength)
        {
            _outputBuffer.Remove(0, _outputBuffer.Length / 2);
        }

        _outputBuffer.AppendLine(line);
        OutputText = _outputBuffer.ToString();
        RequestScrollToEnd?.Invoke();
    }
}
