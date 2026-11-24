using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta16FeatureTests
{
    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBetaVersion()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        StringAssert.StartsWith(version, "1.2.0");

        var cmp = AppUpdateService.CompareVersions(version, "1.2.0-beta.15");
        Assert.IsTrue(cmp > 0, "Current version must be recognized as newer than v1.2.0-beta.15");
    }

    [TestMethod]
    public async Task RecentFilesService_FastOpenPruning_LoadsCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new RecentFilesService(10);
            var items = await service.GetRecentFilesAsync();
            Assert.IsNotNull(items);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
