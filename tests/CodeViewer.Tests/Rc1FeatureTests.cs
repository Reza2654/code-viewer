using System;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Rc1FeatureTests
{
    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsRc1()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.AreEqual("1.2.0-rc.1", version);
    }

    [TestMethod]
    public void AppUpdateService_VersionComparison_ReleaseCandidateHierarchy()
    {
        // 1.2.0-rc.1 must be newer than all beta releases
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0-beta.20") > 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0-beta.19") > 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0-beta.1") > 0);

        // rc.2 > rc.1
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.2", "1.2.0-rc.1") > 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0-rc.2") < 0);

        // rc.1 and rc1 normalized equivalence
        Assert.AreEqual(0, AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0-rc1"));

        // Final release > Release Candidate
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0", "1.2.0-rc.1") > 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-rc.1", "1.2.0") < 0);
    }

    [TestMethod]
    public void ScriptRunnerService_PowerShell_EscapesDoubleQuotes()
    {
        var service = new ScriptRunnerService();
        var startInfo = service.CreateCommandStartInfo("git commit -m \"feat: test message\"", ShellType.PowerShell);

        Assert.IsNotNull(startInfo);
        Assert.IsTrue(startInfo.Arguments.Contains("\\\"feat: test message\\\""),
            $"Expected escaped quotes in arguments, but got: {startInfo.Arguments}");
    }

    [TestMethod]
    public void BuiltInThemes_Provides20ThemesWithValidColors()
    {
        var themes = BuiltInThemes.GetAll();
        Assert.AreEqual(20, themes.Count);

        foreach (var theme in themes)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(theme.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(theme.Name));
            Assert.IsTrue(theme.WindowBackground.StartsWith("#"));
            Assert.IsTrue(theme.EditorBackground.StartsWith("#"));
            Assert.IsTrue(theme.AccentColor.StartsWith("#"));
        }
    }
}
