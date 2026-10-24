using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta14FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_Beta14Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    #region AppSettings EnableAnimations Tests

    [TestMethod]
    public void AppSettings_EnableAnimations_DefaultIsTrue()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.EnableAnimations);
    }

    [TestMethod]
    public void AppSettings_Clone_CopiesEnableAnimations()
    {
        var original = new AppSettings { EnableAnimations = false };
        var cloned = original.Clone();

        Assert.IsFalse(cloned.EnableAnimations);

        original.EnableAnimations = true;
        var cloned2 = original.Clone();
        Assert.IsTrue(cloned2.EnableAnimations);
    }

    [TestMethod]
    public void AppSettings_Serialization_RoundTripsEnableAnimations()
    {
        var settings = new AppSettings { EnableAnimations = false };
        var json = JsonSerializer.Serialize(settings);

        Assert.IsTrue(json.Contains("\"enableAnimations\":false", StringComparison.OrdinalIgnoreCase));

        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.IsNotNull(deserialized);
        Assert.IsFalse(deserialized.EnableAnimations);
    }

    #endregion

    #region SettingsViewModel EnableAnimations Tests

    [TestMethod]
    public async Task SettingsViewModel_LoadsAndSavesEnableAnimations()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        await settingsService.SaveAsync(new AppSettings { EnableAnimations = false });

        var themeService = new ThemeService();
        var vm = new SettingsViewModel(settingsService, themeService);

        Assert.IsFalse(vm.EnableAnimations);

        vm.EnableAnimations = true;
        await vm.SaveAsync();

        var reloaded = settingsService.CurrentSettings;
        Assert.IsTrue(reloaded.EnableAnimations);
    }

    [TestMethod]
    public async Task SettingsViewModel_ResetDefaults_RestoresEnableAnimations()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        await settingsService.SaveAsync(new AppSettings { EnableAnimations = false });

        var themeService = new ThemeService();
        var vm = new SettingsViewModel(settingsService, themeService);
        Assert.IsFalse(vm.EnableAnimations);

        vm.ResetDefaults();
        Assert.IsTrue(vm.EnableAnimations);
    }

    #endregion

    #region MainViewModel EnableAnimations Tracking Tests

    [TestMethod]
    public void MainViewModel_InitializesEnableAnimations_FromSettings()
    {
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings_init.json"));
        settingsService.CurrentSettings.EnableAnimations = false;

        var mainVm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(),
            new DialogService(),
            new AppConfig(),
            settingsService: settingsService
        );

        Assert.IsFalse(mainVm.EnableAnimations);
    }

    [TestMethod]
    public async Task MainViewModel_UpdatesEnableAnimations_OnSettingsChanged()
    {
        var settingsService = new SettingsService(Path.Combine(_tempDir, "settings_changed.json"));
        var mainVm = new MainViewModel(
            new FileService(),
            new LanguageService(),
            new RecentFilesService(),
            new DialogService(),
            new AppConfig(),
            settingsService: settingsService
        );

        Assert.IsTrue(mainVm.EnableAnimations);

        var updated = settingsService.CurrentSettings.Clone();
        updated.EnableAnimations = false;
        await settingsService.SaveAsync(updated);

        Assert.IsFalse(mainVm.EnableAnimations);
    }

    #endregion

    #region App Version & Release Tests

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsCurrentVersion()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.IsTrue(version.StartsWith("1.2.0-beta.", StringComparison.OrdinalIgnoreCase),
            $"Expected version to start with 1.2.0-beta, but got: {version}");
    }

    [TestMethod]
    public void AppUpdateService_VersionComparison_Beta14IsNewerThanBeta13()
    {
        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.14", "1.2.0-beta.13");
        Assert.IsTrue(cmp > 0);

        var cmpReverse = AppUpdateService.CompareVersions("1.2.0-beta.13", "1.2.0-beta.14");
        Assert.IsTrue(cmpReverse < 0);
    }

    #endregion
}
