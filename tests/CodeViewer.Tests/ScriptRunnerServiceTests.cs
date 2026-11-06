using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class ScriptRunnerServiceTests
{
    private ScriptRunnerService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ScriptRunnerService();
    }

    [TestMethod]
    public void IsSupported_ReturnsTrueForSupportedExtensions()
    {
        Assert.IsTrue(_service.IsSupported("script.py"));
        Assert.IsTrue(_service.IsSupported("script.ps1"));
        Assert.IsTrue(_service.IsSupported("script.agent"));
        Assert.IsTrue(_service.IsSupported("script.ps2"));
        Assert.IsTrue(_service.IsSupported("script.dart"));
        Assert.IsTrue(_service.IsSupported("script.js"));
        Assert.IsTrue(_service.IsSupported("script.sh"));
        Assert.IsTrue(_service.IsSupported("script.bat"));
        Assert.IsTrue(_service.IsSupported("script.cmd"));
        Assert.IsTrue(_service.IsSupported("UPPERCASE.PY"));
    }

    [TestMethod]
    public void IsSupported_ReturnsFalseForUnsupportedExtensions()
    {
        Assert.IsFalse(_service.IsSupported(""));
        Assert.IsFalse(_service.IsSupported("notes.txt"));
        Assert.IsFalse(_service.IsSupported("code.cs"));
        Assert.IsFalse(_service.IsSupported("document.md"));
        Assert.IsFalse(_service.IsSupported("image.png"));
    }

    [TestMethod]
    public void CreateStartInfo_Auto_GeneratesCorrectStartInfoForPython()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.py");
        var psi = _service.CreateStartInfo(tempFile, ShellType.Auto);

        Assert.AreEqual(ScriptRunnerService.ResolvePython(), psi.FileName);
        StringAssert.Contains(psi.Arguments, "-u");
        StringAssert.Contains(psi.Arguments, tempFile);
        Assert.AreEqual("utf-8", psi.Environment["PYTHONIOENCODING"]);
        Assert.AreEqual("1", psi.Environment["PYTHONUTF8"]);
    }

    [TestMethod]
    public void CreateStartInfo_Auto_GeneratesCorrectStartInfoForPowerShell()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.ps1");
        var psi = _service.CreateStartInfo(tempFile, ShellType.Auto);

        Assert.AreEqual(ScriptRunnerService.ResolvePowerShell(), psi.FileName);
        StringAssert.Contains(psi.Arguments, "-NoProfile");
        StringAssert.Contains(psi.Arguments, "-ExecutionPolicy Bypass");
        StringAssert.Contains(psi.Arguments, tempFile);
    }

    [TestMethod]
    public void CreateStartInfo_Auto_GeneratesCorrectStartInfoForNode()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.js");
        var psi = _service.CreateStartInfo(tempFile, ShellType.Auto);

        Assert.AreEqual("node", psi.FileName);
        StringAssert.Contains(psi.Arguments, tempFile);
    }

    [TestMethod]
    public void CreateStartInfo_Auto_GeneratesCorrectStartInfoForBat()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.bat");
        var psi = _service.CreateStartInfo(tempFile, ShellType.Auto);

        Assert.AreEqual("cmd.exe", psi.FileName);
        StringAssert.Contains(psi.Arguments, "chcp 65001");
        StringAssert.Contains(psi.Arguments, tempFile);
    }

    [TestMethod]
    public void CreateStartInfo_Auto_ThrowsOnUnsupportedExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.unknown");
        Assert.ThrowsException<NotSupportedException>(() => _service.CreateStartInfo(tempFile, ShellType.Auto));
    }

    [TestMethod]
    public void CreateStartInfo_WithShellOverride_HonorsSelectedShell()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "anything.txt");

        var psPsi = _service.CreateStartInfo(tempFile, ShellType.PowerShell);
        Assert.AreEqual(ScriptRunnerService.ResolvePowerShell(), psPsi.FileName);

        var cmdPsi = _service.CreateStartInfo(tempFile, ShellType.CommandPrompt);
        Assert.AreEqual("cmd.exe", cmdPsi.FileName);

        var pyPsi = _service.CreateStartInfo(tempFile, ShellType.Python);
        Assert.AreEqual(ScriptRunnerService.ResolvePython(), pyPsi.FileName);

        var nodePsi = _service.CreateStartInfo(tempFile, ShellType.Node);
        Assert.AreEqual("node", nodePsi.FileName);

        var bashPsi = _service.CreateStartInfo(tempFile, ShellType.WslBash);
        Assert.AreEqual(ScriptRunnerService.ResolveBash(), bashPsi.FileName);
    }

    [TestMethod]
    public void CreateCommandStartInfo_CreatesExpectedArgumentsPerShell()
    {
        var cmdStart = _service.CreateCommandStartInfo("dir", ShellType.CommandPrompt);
        Assert.AreEqual("cmd.exe", cmdStart.FileName);
        StringAssert.Contains(cmdStart.Arguments, "chcp 65001");
        StringAssert.Contains(cmdStart.Arguments, "dir");

        var psStart = _service.CreateCommandStartInfo("Get-Process", ShellType.PowerShell);
        Assert.AreEqual(ScriptRunnerService.ResolvePowerShell(), psStart.FileName);
        StringAssert.Contains(psStart.Arguments, "Get-Process");

        var pyStart = _service.CreateCommandStartInfo("print(42)", ShellType.Python);
        Assert.AreEqual(ScriptRunnerService.ResolvePython(), pyStart.FileName);
        StringAssert.Contains(pyStart.Arguments, "-u -c \"print(42)\"");

        var nodeStart = _service.CreateCommandStartInfo("console.log(99)", ShellType.Node);
        Assert.AreEqual("node", nodeStart.FileName);
        StringAssert.Contains(nodeStart.Arguments, "-e \"console.log(99)\"");

        var bashStart = _service.CreateCommandStartInfo("ls -la", ShellType.WslBash);
        Assert.AreEqual(ScriptRunnerService.ResolveBash(), bashStart.FileName);
        StringAssert.Contains(bashStart.Arguments, "-c \"ls -la\"");
    }

    [TestMethod]
    public void CreateCommandStartInfo_Auto_DefaultsAppropriately()
    {
        var autoStart = _service.CreateCommandStartInfo("echo test", ShellType.Auto);
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(ScriptRunnerService.ResolvePowerShell(), autoStart.FileName);
        }
        else
        {
            Assert.AreEqual("bash", autoStart.FileName);
        }
    }

    [TestMethod]
    public void CreateCommandStartInfo_ThrowsOnEmptyCommand()
    {
        Assert.ThrowsException<ArgumentException>(() => _service.CreateCommandStartInfo(""));
        Assert.ThrowsException<ArgumentException>(() => _service.CreateCommandStartInfo("   "));
    }

    [TestMethod]
    public void CreateStartInfo_AppliesCustomWorkingDirectory()
    {
        var customDir = Path.GetTempPath();
        var tempFile = Path.Combine(customDir, "test.py");
        var psi = _service.CreateStartInfo(tempFile, ShellType.Python, customDir);

        Assert.AreEqual(customDir, psi.WorkingDirectory);
    }

    [TestMethod]
    public async Task RunCommandAsync_ExecutesSimpleCommandSuccessfully()
    {
        var outputLines = new System.Collections.Generic.List<string>();
        int capturedExitCode = -999;
        long duration = -1;

        // Use CommandPrompt or PowerShell echo which is guaranteed to be fast on Windows
        await _service.RunCommandAsync(
            "echo HelloCodeViewer",
            ShellType.CommandPrompt,
            Path.GetTempPath(),
            line => outputLines.Add(line),
            (code, ms) =>
            {
                capturedExitCode = code;
                duration = ms;
            },
            CancellationToken.None);

        Assert.AreEqual(0, capturedExitCode);
        Assert.IsTrue(duration >= 0);
        Assert.IsTrue(outputLines.Any(l => l.Contains("HelloCodeViewer")));
    }

    [TestMethod]
    public async Task RunCommandAsync_HandlesCancellationGracefully()
    {
        using var cts = new CancellationTokenSource();
        int capturedExitCode = -999;

        // Cancel almost immediately
        cts.CancelAfter(50);

        await _service.RunCommandAsync(
            OperatingSystem.IsWindows() ? "ping 127.0.0.1 -n 5 >nul" : "sleep 5",
            ShellType.CommandPrompt,
            Path.GetTempPath(),
            _ => { },
            (code, _) => capturedExitCode = code,
            cts.Token);

        // When canceled, exit code reported is -1
        Assert.AreEqual(-1, capturedExitCode);
    }
}
