using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class ThemeServiceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Theme_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void ThemeService_InitializesWithBuiltInThemes()
    {
        var service = new ThemeService();

        Assert.IsTrue(service.AvailableThemes.Count >= 6, "Expected at least 6 built-in themes.");
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "dark-plus"));
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "one-dark"));
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "monokai"));
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "dracula"));
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "solarized-dark"));
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "github-light"));

        Assert.IsNotNull(service.CurrentTheme);
    }

    [TestMethod]
    public void ThemeService_ApplyTheme_ChangesCurrentTheme()
    {
        var service = new ThemeService();
        var monokai = service.AvailableThemes.First(t => t.Id == "monokai");

        bool eventFired = false;
        service.ThemeChanged += t =>
        {
            if (t.Id == "monokai") eventFired = true;
        };

        service.ApplyTheme(monokai);

        Assert.AreEqual("monokai", service.CurrentTheme.Id);
        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public async Task ThemeService_ImportThemeFromJsonAsync_RegistersAndApplies()
    {
        var service = new ThemeService();
        var jsonFile = Path.Combine(_tempDir, "custom-test-theme.json");
        var jsonContent = @"{
  ""id"": ""custom-test"",
  ""name"": ""Custom Test Theme"",
  ""isDark"": true,
  ""windowBackground"": ""#112233"",
  ""foreground"": ""#FFFFFF""
}";
        await File.WriteAllTextAsync(jsonFile, jsonContent);

        var imported = await service.ImportThemeFromJsonAsync(jsonFile);

        Assert.AreEqual("custom-test", imported.Id);
        Assert.AreEqual("Custom Test Theme", imported.Name);
        Assert.AreEqual("custom-test", service.CurrentTheme.Id);
        Assert.IsTrue(service.AvailableThemes.Any(t => t.Id == "custom-test"));
    }

    [TestMethod]
    public void ApplyCodeColorsToHighlighting_RemovesBoldFontWeights()
    {
        var service = new ThemeService();
        var darkTheme = service.AvailableThemes.First(t => t.Id == "dark-plus");
        var ps = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("PowerShell");
        Assert.IsNotNull(ps);

        service.ApplyCodeColorsToHighlighting(ps, darkTheme);

        foreach (var color in ps.NamedHighlightingColors)
        {
            Assert.AreEqual(Avalonia.Media.FontWeight.Normal, color.FontWeight,
                $"Color '{color.Name}' in PowerShell should have Normal font weight (no bold).");
        }
    }

    [TestMethod]
    public void ApplyCodeColorsToHighlighting_MapsCommandToReadableMethodColor()
    {
        var service = new ThemeService();
        var darkTheme = service.AvailableThemes.First(t => t.Id == "dark-plus");
        var ps = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("PowerShell");
        Assert.IsNotNull(ps);

        service.ApplyCodeColorsToHighlighting(ps, darkTheme);

        var cmdColor = ps.NamedHighlightingColors.FirstOrDefault(c => c.Name == "Command");
        Assert.IsNotNull(cmdColor);
        Assert.IsNotNull(cmdColor.Foreground);
        var brush = cmdColor.Foreground.GetBrush(null);
        Assert.IsInstanceOfType(brush, typeof(Avalonia.Media.ISolidColorBrush));
        var solid = (Avalonia.Media.ISolidColorBrush)brush!;
        // Dark+ CodeMethod is #DCDCAA
        Assert.AreEqual(Avalonia.Media.Color.Parse("#DCDCAA"), solid.Color);
        Assert.AreEqual(Avalonia.Media.FontWeight.Normal, cmdColor.FontWeight);
    }

    [TestMethod]
    public void ApplyTheme_UpdatesHighlightingColorsAcrossThemes()
    {
        var service = new ThemeService();
        var darkPlus = service.AvailableThemes.First(t => t.Id == "dark-plus");
        var githubLight = service.AvailableThemes.First(t => t.Id == "github-light");
        var ps = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("PowerShell");
        Assert.IsNotNull(ps);

        // Apply dark theme
        service.ApplyTheme(darkPlus);
        var cmdDark = ps.NamedHighlightingColors.First(c => c.Name == "Command");
        var brushDark = (Avalonia.Media.ISolidColorBrush)cmdDark.Foreground!.GetBrush(null)!;
        Assert.AreEqual(Avalonia.Media.Color.Parse(darkPlus.CodeMethod), brushDark.Color);

        // Switch to light theme
        service.ApplyTheme(githubLight);
        var cmdLight = ps.NamedHighlightingColors.First(c => c.Name == "Command");
        var brushLight = (Avalonia.Media.ISolidColorBrush)cmdLight.Foreground!.GetBrush(null)!;
        Assert.AreEqual(Avalonia.Media.Color.Parse(githubLight.CodeMethod), brushLight.Color);
    }
}
