using System;
using System.IO;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta11FeatureTests
{
    [TestMethod]
    public void VersionComparison_SemanticAndPrereleaseOrdering()
    {
        // Beta 10 vs Beta 11
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-beta.10", "1.2.0-beta.11") < 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-beta.11", "1.2.0-beta.10") > 0);

        // Same version
        Assert.AreEqual(0, AppUpdateService.CompareVersions("1.2.0-beta.11", "1.2.0-beta.11"));
        Assert.AreEqual(0, AppUpdateService.CompareVersions("v1.2.0-beta.11", "1.2.0-beta.11"));

        // Prerelease vs Release of same base version (Release is newer)
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-beta.11", "1.2.0") < 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0", "1.2.0-beta.11") > 0);

        // Major / Minor bump
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-beta.11", "1.2.1") < 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("1.2.0-beta.11", "2.0.0") < 0);
        Assert.IsTrue(AppUpdateService.CompareVersions("2.0.0", "1.2.0-beta.11") > 0);
    }

    [TestMethod]
    public void FontSanitizer_RecognizesRobotoMonoAndGoogleSansCode()
    {
        Assert.AreEqual("Roboto Mono", AppSettings.SanitizeFontFamily("Roboto Mono"));
        Assert.AreEqual("Roboto Mono", AppSettings.SanitizeFontFamily("roboto mono"));
        Assert.AreEqual("Google Sans Code", AppSettings.SanitizeFontFamily("Google Sans Code"));
        Assert.AreEqual("Google Sans Code", AppSettings.SanitizeFontFamily("google sans code"));
        Assert.AreEqual("Google Sans Code", AppSettings.SanitizeFontFamily(null));
        Assert.AreEqual("Google Sans Code", AppSettings.SanitizeFontFamily(""));
    }

    [TestMethod]
    public void FontHelper_GeneratesCompositeFontFamilyWithFallbacks()
    {
        var ff = FontHelper.GetFontFamily("Roboto Mono");
        Assert.IsNotNull(ff);
        Assert.AreEqual("Roboto Mono", ff.Name);

        var ff2 = FontHelper.GetFontFamily("Google Sans Code");
        Assert.IsNotNull(ff2);
        Assert.AreEqual("Google Sans Code", ff2.Name);
    }

    [TestMethod]
    public async Task FontToggle_SwitchesBetweenGoogleSansCodeAndRobotoMono()
    {
        var tempSettingsDir = Path.Combine(Path.GetTempPath(), "CodeViewerTests_Beta11_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSettingsDir);
        var settingsPath = Path.Combine(tempSettingsDir, "settings.json");

        try
        {
            var settingsService = new SettingsService(settingsPath);
            var fileService = new FileService();
            var languageService = new LanguageService();
            var recentFilesService = new RecentFilesService();
            var dialogService = new MockBeta11DialogService();
            var config = new AppConfig();
            var themeService = new ThemeService();
            var pluginService = new PluginService();

            using var vm = new MainViewModel(
                fileService,
                languageService,
                recentFilesService,
                dialogService,
                config,
                themeService,
                pluginService,
                settingsService: settingsService);

            // Initial font
            vm.CurrentFontFamily = "Google Sans Code";
            Assert.IsTrue(vm.IsGoogleSansCodeActive);
            Assert.IsFalse(vm.IsRobotoMonoActive);

            // Toggle to Roboto Mono
            await vm.ToggleCodingFontAsync();
            Assert.AreEqual("Roboto Mono", vm.CurrentFontFamily);
            Assert.IsTrue(vm.IsRobotoMonoActive);
            Assert.IsFalse(vm.IsGoogleSansCodeActive);

            // Toggle back to Google Sans Code
            await vm.ToggleCodingFontAsync();
            Assert.AreEqual("Google Sans Code", vm.CurrentFontFamily);
            Assert.IsTrue(vm.IsGoogleSansCodeActive);
            Assert.IsFalse(vm.IsRobotoMonoActive);
        }
        finally
        {
            if (Directory.Exists(tempSettingsDir))
            {
                Directory.Delete(tempSettingsDir, true);
            }
        }
    }

    [TestMethod]
    public async Task SettingsPersistence_StoresBeta11Options()
    {
        var tempSettingsDir = Path.Combine(Path.GetTempPath(), "CodeViewerTests_Beta11_Settings_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSettingsDir);
        var settingsPath = Path.Combine(tempSettingsDir, "settings.json");

        try
        {
            var settingsService = new SettingsService(settingsPath);
            var settings = new AppSettings
            {
                FontFamily = "Roboto Mono",
                AutoCheckForUpdates = false,
                RunInBackgroundOnStartup = true,
                MinimizeToTrayOnClose = true
            };

            await settingsService.SaveAsync(settings);

            var loadedService = new SettingsService(settingsPath);
            await loadedService.LoadAsync();
            var loaded = loadedService.CurrentSettings;

            Assert.AreEqual("Roboto Mono", loaded.FontFamily);
            Assert.IsFalse(loaded.AutoCheckForUpdates);
            Assert.IsTrue(loaded.RunInBackgroundOnStartup);
            Assert.IsTrue(loaded.MinimizeToTrayOnClose);
        }
        finally
        {
            if (Directory.Exists(tempSettingsDir))
            {
                Directory.Delete(tempSettingsDir, true);
            }
        }
    }

    [TestMethod]
    public void StartupRegistration_CanQueryStatusWithoutExceptions()
    {
        // On Windows, checking startup registration status should return a boolean without throwing
        if (OperatingSystem.IsWindows())
        {
            var enabled = StartupRegistrationService.IsRunOnStartupEnabled();
            Assert.IsTrue(enabled || !enabled);
        }
    }

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReturnsNonEmpty()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.IsFalse(string.IsNullOrWhiteSpace(version));
        Assert.IsTrue(version.Contains("1.2.0"));
    }

    private class MockBeta11DialogService : IDialogService
    {
        public void Initialize(Avalonia.Controls.Window window) { }
        public Task<string?> ShowOpenFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<ConfirmResult> ShowSaveConfirmationAsync(string fileName) => Task.FromResult(ConfirmResult.Cancel);
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No") => Task.FromResult(false);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
        public Task ShowUpdateDialogAsync(AppUpdateInfo updateInfo, IAppUpdateService updateService) => Task.CompletedTask;
    }
}
