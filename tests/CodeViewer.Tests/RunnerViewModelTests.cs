using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class RunnerViewModelTests
{
    private class MockRunnerService : IScriptRunnerService
    {
        public List<string> Inputs { get; } = [];
        public string? LastScriptPath { get; private set; }
        public string? LastCommand { get; private set; }
        public ShellType LastShell { get; private set; }
        public string? LastWorkingDir { get; private set; }

        public bool IsSupported(string filePath) => true;

        public Task RunAsync(string filePath, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
        {
            return RunAsync(filePath, ShellType.Auto, null, onOutput, onCompleted, ct);
        }

        public Task RunAsync(string filePath, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
        {
            LastScriptPath = filePath;
            LastShell = shellType;
            LastWorkingDir = workingDir;

            onOutput("Script running...");
            onCompleted(0, 15);
            return Task.CompletedTask;
        }

        public Task RunCommandAsync(string command, ShellType shellType, string? workingDir, Action<string> onOutput, Action<int, long> onCompleted, CancellationToken ct)
        {
            LastCommand = command;
            LastShell = shellType;
            LastWorkingDir = workingDir;

            onOutput($"Command '{command}' output");
            onCompleted(0, 20);
            return Task.CompletedTask;
        }

        public void SendInput(string text)
        {
            Inputs.Add(text);
        }
    }

    [TestMethod]
    public void InitialState_PropertiesAreProperlyInitialized()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock);

        Assert.IsFalse(vm.IsVisible);
        Assert.IsFalse(vm.IsRunning);
        Assert.AreEqual(ShellType.Auto, vm.SelectedShell.Type);
        Assert.AreEqual("Ready", vm.StatusText);
        Assert.AreEqual("Enter command (e.g., git status, dotnet test, dir)...", vm.InputPlaceholder);
        Assert.AreEqual("Run", vm.ActionButtonText);
        Assert.IsTrue(vm.AvailableShells.Count >= 6);
    }

    [TestMethod]
    public void IsRunningChanged_UpdatesPlaceholderAndActionButtonText()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock);

        vm.IsRunning = true;
        Assert.AreEqual("Send input to running process (stdin)...", vm.InputPlaceholder);
        Assert.AreEqual("Send", vm.ActionButtonText);

        vm.IsRunning = false;
        Assert.AreEqual("Enter command (e.g., git status, dotnet test, dir)...", vm.InputPlaceholder);
        Assert.AreEqual("Run", vm.ActionButtonText);
    }

    [TestMethod]
    public void ClearOutput_ClearsBufferAndResetsStatus()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock);

        vm.OutputText = "Some prior output\nline 2";
        vm.ClearOutput();

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.AreEqual("Ready", vm.StatusText);
    }

    [TestMethod]
    public void Close_HidesConsoleDock()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock)
        {
            IsVisible = true
        };

        vm.Close();
        Assert.IsFalse(vm.IsVisible);
    }

    [TestMethod]
    public void HistoryNavigation_RecallsCommandsCorrectly()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock);

        vm.RecordCommandHistory("git status");
        vm.RecordCommandHistory("dotnet test");
        vm.RecordCommandHistory("npm run build");

        vm.CurrentInput = "partial query";

        // Up arrow: should get "npm run build"
        Assert.IsTrue(vm.NavigateHistory(-1, out var cmd1));
        Assert.AreEqual("npm run build", cmd1);

        // Up arrow again: should get "dotnet test"
        Assert.IsTrue(vm.NavigateHistory(-1, out var cmd2));
        Assert.AreEqual("dotnet test", cmd2);

        // Up arrow again: should get "git status"
        Assert.IsTrue(vm.NavigateHistory(-1, out var cmd3));
        Assert.AreEqual("git status", cmd3);

        // Up arrow at top: returns false
        Assert.IsFalse(vm.NavigateHistory(-1, out _));

        // Down arrow: back to "dotnet test"
        Assert.IsTrue(vm.NavigateHistory(1, out var cmd4));
        Assert.AreEqual("dotnet test", cmd4);

        // Down arrow: back to "npm run build"
        Assert.IsTrue(vm.NavigateHistory(1, out var cmd5));
        Assert.AreEqual("npm run build", cmd5);

        // Down arrow to end: restores stashed "partial query"
        Assert.IsTrue(vm.NavigateHistory(1, out var cmd6));
        Assert.AreEqual("partial query", cmd6);
    }

    [TestMethod]
    public async Task SubmitInputAsync_WhenIdle_ExecutesCommandAndRecordsHistory()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock)
        {
            SelectedShell = ShellOption.All.First(s => s.Type == ShellType.PowerShell),
            CurrentInput = "git status"
        };

        await vm.SubmitInputAsync();

        Assert.AreEqual(string.Empty, vm.CurrentInput);
        Assert.AreEqual("git status", mock.LastCommand);
        Assert.AreEqual(ShellType.PowerShell, mock.LastShell);
        Assert.IsTrue(vm.OutputText.Contains("Command 'git status' output"));

        // Verify history has it
        Assert.IsTrue(vm.NavigateHistory(-1, out var historyCmd));
        Assert.AreEqual("git status", historyCmd);
    }

    [TestMethod]
    public async Task SubmitInputAsync_WhenRunning_SendsInputToProcess()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock)
        {
            IsRunning = true,
            CurrentInput = "yes"
        };

        await vm.SubmitInputAsync();

        Assert.AreEqual(string.Empty, vm.CurrentInput);
        Assert.AreEqual(1, mock.Inputs.Count);
        Assert.AreEqual("yes", mock.Inputs[0]);
        Assert.IsTrue(vm.OutputText.Contains("> yes"));
    }

    [TestMethod]
    public async Task RunScriptAsync_UsesConfiguredShellAndWorkingDirectory()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock)
        {
            SelectedShell = ShellOption.All.First(s => s.Type == ShellType.Python),
            GetWorkingDirectory = () => @"C:\WorkspaceRoot"
        };

        await vm.RunScriptAsync(@"C:\WorkspaceRoot\scripts\main.py");

        Assert.AreEqual(@"C:\WorkspaceRoot\scripts\main.py", mock.LastScriptPath);
        Assert.AreEqual(ShellType.Python, mock.LastShell);
        Assert.AreEqual(@"C:\WorkspaceRoot", mock.LastWorkingDir);
        Assert.IsTrue(vm.IsVisible);
    }

    [TestMethod]
    public async Task RunCommandAsync_ResolvesWorkspaceDirectory()
    {
        var mock = new MockRunnerService();
        var vm = new RunnerViewModel(mock)
        {
            SelectedShell = ShellOption.All.First(s => s.Type == ShellType.CommandPrompt),
            GetWorkingDirectory = () => @"C:\Project"
        };

        await vm.RunCommandAsync("dir");

        Assert.AreEqual("dir", mock.LastCommand);
        Assert.AreEqual(ShellType.CommandPrompt, mock.LastShell);
        Assert.AreEqual(@"C:\Project", mock.LastWorkingDir);
    }
}
