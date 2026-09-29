using System;
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

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _scriptPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _currentInput = string.Empty;

    public Action? RequestScrollToEnd { get; set; }

    public RunnerViewModel(IScriptRunnerService? runnerService = null)
    {
        _runnerService = runnerService ?? new ScriptRunnerService();
    }

    [RelayCommand]
    public void SendInput()
    {
        if (!IsRunning) return;
        var input = CurrentInput ?? string.Empty;
        CurrentInput = string.Empty;
        AppendOutput($"> {input}");
        _runnerService.SendInput(input);
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


    public async Task RunScriptAsync(string filePath)
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

        try
        {
            await _runnerService.RunAsync(filePath,
                line =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        AppendOutput(line);
                    });
                },
                (exitCode, durationMs) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IsRunning = false;
                        StatusText = exitCode == 0
                            ? $"✓ Completed in {durationMs} ms"
                            : $"⏹ Exited with code {exitCode} in {durationMs} ms";
                    });
                },
                token);
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsRunning = false;
                StatusText = "⏹ Cancelled by user";
                AppendOutput("[Script execution stopped by user]");
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
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
