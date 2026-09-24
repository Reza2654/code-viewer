using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class FileServiceTests
{
    private string _tempDir = null!;
    private FileService _fileService = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CVTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _fileService = new FileService();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public async Task OpenFileAsync_Utf8WithoutBom_DetectsCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "test_utf8.txt");
        await File.WriteAllTextAsync(filePath, "Hello, World!\nSecond line", new UTF8Encoding(false));

        var doc = await _fileService.OpenFileAsync(filePath);

        Assert.IsNotNull(doc);
        Assert.AreEqual("Hello, World!\nSecond line", doc.Text);
        Assert.IsFalse(doc.EncodingInfo.HasBom);
        Assert.AreEqual(LineEndingType.LF, doc.LineEnding);
        Assert.AreEqual("test_utf8.txt", doc.Title);
    }

    [TestMethod]
    public async Task OpenFileAsync_Utf8WithBom_DetectsCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "test_bom.txt");
        await File.WriteAllTextAsync(filePath, "Line 1\r\nLine 2", new UTF8Encoding(true));

        var doc = await _fileService.OpenFileAsync(filePath);

        Assert.IsTrue(doc.EncodingInfo.HasBom);
        Assert.AreEqual(LineEndingType.CRLF, doc.LineEnding);
    }

    [TestMethod]
    public void DetectLineEndings_DistinguishesCrlfAndLfAndMixed()
    {
        Assert.AreEqual(LineEndingType.CRLF, _fileService.DetectLineEndings("a\r\nb\r\nc"));
        Assert.AreEqual(LineEndingType.LF, _fileService.DetectLineEndings("a\nb\nc"));
        Assert.AreEqual(LineEndingType.CR, _fileService.DetectLineEndings("a\rb\rc"));
        Assert.AreEqual(LineEndingType.Mixed, _fileService.DetectLineEndings("a\r\nb\nc"));
    }

    [TestMethod]
    public async Task SaveFileAsync_PreservesContentAndAtomicWrite()
    {
        var filePath = Path.Combine(_tempDir, "save_test.cs");
        var doc = DocumentModel.CreateNew("save_test.cs");
        doc.FilePath = filePath;
        doc.Text = "void Main() => Console.WriteLine(123);";
        doc.LineEnding = LineEndingType.LF;

        await _fileService.SaveFileAsync(doc);

        Assert.IsTrue(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        Assert.AreEqual(doc.Text, content);
        Assert.IsFalse(doc.IsModified);
    }
}
