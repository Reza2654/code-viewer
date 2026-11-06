using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta18FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta18_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBeta18()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        StringAssert.StartsWith(version, "1.2.0-beta.");

        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.18", "1.2.0-beta.17");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.18 must be recognized as newer than v1.2.0-beta.17");
    }

    [TestMethod]
    public void AppSettings_ClonesAndPersistsCleanly_WithoutMiddleClickScroll()
    {
        var settings = new AppSettings
        {
            SmoothCursorBlink = true,
            EnableAnimations = true,
            WordWrap = true,
            ShowLineNumbers = true
        };

        var cloned = settings.Clone();
        Assert.AreEqual(settings.SmoothCursorBlink, cloned.SmoothCursorBlink);
        Assert.AreEqual(settings.EnableAnimations, cloned.EnableAnimations);
        Assert.AreEqual(settings.WordWrap, cloned.WordWrap);
        Assert.AreEqual(settings.ShowLineNumbers, cloned.ShowLineNumbers);

        // Verify AppSettings type does not contain EnableMiddleClickScroll
        var prop = typeof(AppSettings).GetProperty("EnableMiddleClickScroll");
        Assert.IsNull(prop, "EnableMiddleClickScroll property must be completely removed from AppSettings.");
    }

    [TestMethod]
    public async Task SettingsViewModel_RoundtripsSettings_WithoutMiddleClickScroll()
    {
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsFile);
        var themeService = new ThemeService();

        var vm = new SettingsViewModel(settingsService, themeService);
        var prop = typeof(SettingsViewModel).GetProperty("EnableMiddleClickScroll");
        Assert.IsNull(prop, "EnableMiddleClickScroll property must be completely removed from SettingsViewModel.");

        vm.WordWrap = true;
        vm.SaveCommand.Execute(null);

        var reloaded = new SettingsService(settingsFile);
        Assert.IsTrue(reloaded.CurrentSettings.WordWrap);
    }
}
