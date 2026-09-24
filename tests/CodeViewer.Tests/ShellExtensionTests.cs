using System;
using System.IO;
using System.Runtime.InteropServices;
using CodeViewer.ShellExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class ShellExtensionTests
{
    [TestMethod]
    public void GetTitle_ReturnsOpenWithCodeViewer()
    {
        var cmd = new CodeViewerCommand();
        var hr = cmd.GetTitle(null, out var ppszName);

        Assert.AreEqual(0, hr);
        Assert.AreNotEqual(IntPtr.Zero, ppszName);

        try
        {
            var title = Marshal.PtrToStringUni(ppszName);
            Assert.AreEqual("Open with Code Viewer", title);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ppszName);
        }
    }

    [TestMethod]
    public void GetCanonicalName_ReturnsExpectedGuid()
    {
        var cmd = new CodeViewerCommand();
        var hr = cmd.GetCanonicalName(out var guid);

        Assert.AreEqual(0, hr);
        Assert.AreEqual(CodeViewerCommand.CommandGuid, guid);
    }

    [TestMethod]
    public void GetState_ReturnsEnabled()
    {
        var cmd = new CodeViewerCommand();
        var hr = cmd.GetState(null, false, out var state);

        Assert.AreEqual(0, hr);
        Assert.AreEqual(EXPCMDSTATE.ECS_ENABLED, state);
    }

    [TestMethod]
    public void FindCodeViewerExecutable_FindsExistingBinary()
    {
        var exePath = CodeViewerCommand.FindCodeViewerExecutable();

        Assert.IsNotNull(exePath);
        Assert.IsTrue(File.Exists(exePath));
        Assert.IsTrue(exePath.EndsWith("CodeViewer.exe", StringComparison.OrdinalIgnoreCase));
    }
}
