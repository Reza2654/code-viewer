using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvaloniaEdit.Highlighting;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta10FeatureTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CV_Beta10_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void FontHelper_GetFontFamily_CachesInstancesAcrossCalls()
    {
        var font1 = FontHelper.GetFontFamily("Cascadia Code");
        var font2 = FontHelper.GetFontFamily("Cascadia Code");
        var font3 = FontHelper.GetFontFamily("cascadia code"); // case-insensitive cache

        Assert.IsNotNull(font1);
        Assert.AreSame(font1, font2, "FontHelper should return cached FontFamily instance for identical font name.");
        Assert.AreSame(font1, font3, "FontHelper cache should be case-insensitive.");

        var defaultFont1 = FontHelper.GetFontFamily(null);
        var defaultFont2 = FontHelper.GetFontFamily(string.Empty);
        Assert.AreSame(defaultFont1, defaultFont2, "Null and empty font names should resolve to cached default font.");
    }

    [TestMethod]
    public async Task FileService_OpenFileAsync_SinglePassReadsAccuratelyWithBom()
    {
        var fileService = new FileService();
        var testFile = Path.Combine(_tempDir, "sample_with_bom.txt");
        var expectedText = "Hello World with BOM!\nLine 2\nLine 3";

        // Write file with UTF-8 BOM
        var encoding = new UTF8Encoding(true);
        await File.WriteAllTextAsync(testFile, expectedText, encoding);

        var doc = await fileService.OpenFileAsync(testFile);

        Assert.IsNotNull(doc);
        Assert.AreEqual(expectedText, doc.Text);
        Assert.IsTrue(doc.EncodingInfo.HasBom, "File should be detected as having BOM.");
        Assert.AreEqual(LineEndingType.LF, doc.LineEnding);
    }

    [TestMethod]
    public async Task FileService_OpenFileAsync_ReadsUtf8WithoutBom()
    {
        var fileService = new FileService();
        var testFile = Path.Combine(_tempDir, "sample_no_bom.txt");
        var expectedText = "Alpha Beta Gamma\r\nDelta Epsilon";

        var encoding = new UTF8Encoding(false);
        await File.WriteAllTextAsync(testFile, expectedText, encoding);

        var doc = await fileService.OpenFileAsync(testFile);

        Assert.IsNotNull(doc);
        Assert.AreEqual(expectedText, doc.Text);
        Assert.IsFalse(doc.EncodingInfo.HasBom, "File should be detected as UTF-8 without BOM.");
        Assert.AreEqual(LineEndingType.CRLF, doc.LineEnding);
    }

    [TestMethod]
    public void FileService_DetectEncodingFromBytes_HandlesAllVariants()
    {
        // UTF8 with BOM
        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 };
        var enc1 = FileService.DetectEncodingFromBytes(utf8Bom, 4);
        Assert.IsTrue(enc1.HasBom);
        Assert.IsTrue(enc1.Encoding is UTF8Encoding);

        // UTF16 LE
        var utf16Le = new byte[] { 0xFF, 0xFE, 0x41, 0x00 };
        var enc2 = FileService.DetectEncodingFromBytes(utf16Le, 4);
        Assert.IsTrue(enc2.HasBom);
        Assert.IsTrue(enc2.Encoding is UnicodeEncoding && !((UnicodeEncoding)enc2.Encoding).Equals(new UnicodeEncoding(true, true)));

        // UTF16 BE
        var utf16Be = new byte[] { 0xFE, 0xFF, 0x00, 0x41 };
        var enc3 = FileService.DetectEncodingFromBytes(utf16Be, 4);
        Assert.IsTrue(enc3.HasBom);

        // UTF8 no BOM
        var utf8NoBom = new byte[] { 0x48, 0x65, 0x6C, 0x6C };
        var enc4 = FileService.DetectEncodingFromBytes(utf8NoBom, 4);
        Assert.IsFalse(enc4.HasBom);
    }

    [TestMethod]
    public void LanguageService_CustomHighlighters_LoadedLazilyOnDemand()
    {
        var service = new LanguageService();

        // Highlighters are loaded lazily on demand
        var dartDef = service.GetHighlightingDefinition("Dart");
        Assert.IsNotNull(dartDef, "Dart highlighting definition should be resolved on demand.");

        var agentDef = service.GetHighlightingDefinition("AgentLang");
        Assert.IsNotNull(agentDef, "AgentLang highlighting definition should be resolved on demand.");

        var ps2Def = service.GetHighlightingDefinition("PS2");
        Assert.IsNotNull(ps2Def, "PS2 highlighting definition should be resolved on demand.");
    }

    [TestMethod]
    public async Task PluginService_Initialization_RegistersBuiltInInstantlyAndLoadsBackground()
    {
        var service = new PluginService();

        // Synchronously available immediately
        Assert.IsTrue(service.Plugins.Count >= 10, "Built-in plugins must be available synchronously at t=0.");
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-json-format"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-text-statistics"));

        // Background task completes cleanly
        Assert.IsNotNull(service.InitializationTask);
        await service.InitializationTask;
        Assert.IsTrue(service.InitializationTask.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task ThemeService_FastResolution_ResolvesImmediatelyAndInitializesBackground()
    {
        var service = new ThemeService(_tempDir);

        // CurrentTheme resolved instantly from built-in themes
        Assert.IsNotNull(service.CurrentTheme);
        Assert.IsTrue(service.AvailableThemes.Count >= 6, "Built-in themes should be registered at t=0.");

        // Background task completes cleanly
        Assert.IsNotNull(service.InitializationTask);
        await service.InitializationTask;
        Assert.IsTrue(service.InitializationTask.IsCompletedSuccessfully);
    }

    [TestMethod]
    public void SingleInstanceService_CustomSuffix_WorksCorrectly()
    {
        var original = SingleInstanceService.CustomSuffix;
        try
        {
            SingleInstanceService.CustomSuffix = "UnitTest_Beta10";
            Assert.AreEqual("UnitTest_Beta10", SingleInstanceService.CustomSuffix);
        }
        finally
        {
            SingleInstanceService.CustomSuffix = original;
        }
    }
}
