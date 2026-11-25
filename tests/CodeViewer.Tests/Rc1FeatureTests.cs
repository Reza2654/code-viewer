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
    public void AppUpdateService_GetCurrentAppVersion_ReportsRcSeries()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        StringAssert.StartsWith(version, "1.2.0-rc.");
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

    [TestMethod]
    public void FontHelper_UiFontFamily_ConfiguredWithGoogleSansFlex()
    {
        Assert.AreEqual("Google Sans Flex", FontHelper.UiFontName);
        Assert.IsTrue(FontHelper.UiFontFamilyString.StartsWith("Google Sans Flex,"));
        Assert.IsNotNull(FontHelper.UiFontFamily);
        Assert.IsTrue(FontHelper.UiFontFamily.Name.Contains("Google Sans Flex"));
    }

    [TestMethod]
    public void BuiltInThemes_SolarizedLight_HasAccessibleBorderAndSelection()
    {
        var theme = System.Linq.Enumerable.First(BuiltInThemes.GetAll(), t => t.Id == "solarized-light");
        Assert.IsNotNull(theme);
        Assert.IsFalse(theme.IsDark);
        // Border must not be harsh magenta (#D33682)
        Assert.AreNotEqual("#D33682", theme.BorderColor);
        Assert.AreEqual("#D6D0BA", theme.BorderColor);
        // Selection must have visible contrast against EditorBackground
        Assert.AreNotEqual(theme.EditorBackground, theme.SelectionBackground);
    }

    [TestMethod]
    public void ThemeService_ResolveEffectiveTheme_DoesNotLeakHexAcrossPresets()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CV_ThemeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var themeService = new ThemeService(tempDir);
            var settings = new AppSettings
            {
                ThemeMode = "Dark",
                DarkPresetId = "solarized-dark",
                LightPresetId = "solarized-light"
            };

            var resolvedDark = themeService.ResolveEffectiveTheme(settings);
            var solarizedDark = System.Linq.Enumerable.First(BuiltInThemes.GetAll(), t => t.Id == "solarized-dark");
            Assert.AreEqual(solarizedDark.EditorBackground, resolvedDark.EditorBackground,
                "Solarized Dark preset should preserve its native background and not leak #101010");

            settings.ThemeMode = "Light";
            var resolvedLight = themeService.ResolveEffectiveTheme(settings);
            var solarizedLight = System.Linq.Enumerable.First(BuiltInThemes.GetAll(), t => t.Id == "solarized-light");
            Assert.AreEqual(solarizedLight.EditorBackground, resolvedLight.EditorBackground,
                "Solarized Light preset should preserve its native background and not leak #F9F9F9");
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir)) System.IO.Directory.Delete(tempDir, true);
        }
    }
}
