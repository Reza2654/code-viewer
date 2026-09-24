using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Plugins;
using CodeViewer.Plugins.BuiltIn;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class PluginServiceTests
{
    private sealed class MockPluginContext : IPluginContext
    {
        public string CurrentText { get; set; } = string.Empty;
        public string SelectedText { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string Language { get; set; } = "Plain Text";
        public bool HasSelection => !string.IsNullOrEmpty(SelectedText);
        public string? LastMessageTitle { get; private set; }
        public string? LastMessageBody { get; private set; }

        public void ReplaceSelectedText(string newText)
        {
            SelectedText = newText;
        }

        public void ReplaceAllText(string newText)
        {
            CurrentText = newText;
        }

        public void InsertText(string text)
        {
            CurrentText += text;
        }

        public Task ShowMessageAsync(string title, string message)
        {
            LastMessageTitle = title;
            LastMessageBody = message;
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public void PluginService_InitializesWithBuiltInPlugins()
    {
        var service = new PluginService();

        Assert.IsTrue(service.Plugins.Count >= 10, "Expected at least 10 built-in plugins.");
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-json-format"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-json-minify"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-base64-encode"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-base64-decode"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-url-encode"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-sort-lines-asc"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-remove-duplicate-lines"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-case-upper"));
        Assert.IsTrue(service.Plugins.Any(p => p.Id == "builtin-text-statistics"));
    }

    [TestMethod]
    public async Task JsonFormatPlugin_FormatsValidJsonCorrectly()
    {
        var plugin = new JsonFormatPlugin();
        var context = new MockPluginContext
        {
            CurrentText = "{\"name\":\"CodeViewer\",\"version\":1}"
        };

        await plugin.ExecuteAsync(context);

        Assert.IsTrue(context.CurrentText.Contains("\n  \"name\": \"CodeViewer\",") ||
                      context.CurrentText.Contains("\r\n  \"name\": \"CodeViewer\","));
    }

    [TestMethod]
    public async Task JsonMinifyPlugin_MinifiesJsonCorrectly()
    {
        var plugin = new JsonMinifyPlugin();
        var context = new MockPluginContext
        {
            CurrentText = "{\n  \"a\": 1,\n  \"b\": 2\n}"
        };

        await plugin.ExecuteAsync(context);

        Assert.AreEqual("{\"a\":1,\"b\":2}", context.CurrentText.Trim());
    }

    [TestMethod]
    public async Task Base64Plugins_EncodeAndDecodeText()
    {
        var encoder = new Base64EncodePlugin();
        var decoder = new Base64DecodePlugin();

        var context = new MockPluginContext
        {
            CurrentText = "Hello Code Viewer!"
        };

        await encoder.ExecuteAsync(context);
        Assert.AreEqual("SGVsbG8gQ29kZSBWaWV3ZXIh", context.CurrentText);

        await decoder.ExecuteAsync(context);
        Assert.AreEqual("Hello Code Viewer!", context.CurrentText);
    }

    [TestMethod]
    public async Task LineToolsPlugins_SortAndDeduplicate()
    {
        var sortAsc = new SortLinesAscendingPlugin();
        var sortDesc = new SortLinesDescendingPlugin();
        var dedupe = new RemoveDuplicateLinesPlugin();

        var context = new MockPluginContext
        {
            CurrentText = "banana\napple\norange\napple"
        };

        await dedupe.ExecuteAsync(context);
        Assert.AreEqual("banana\napple\norange", context.CurrentText);

        await sortAsc.ExecuteAsync(context);
        Assert.AreEqual("apple\nbanana\norange", context.CurrentText);

        await sortDesc.ExecuteAsync(context);
        Assert.AreEqual("orange\nbanana\napple", context.CurrentText);
    }

    [TestMethod]
    public async Task CaseTransformPlugins_TransformsCases()
    {
        var upper = new UpperCasePlugin();
        var lower = new LowerCasePlugin();
        var title = new TitleCasePlugin();

        var context = new MockPluginContext { CurrentText = "hello world" };

        await upper.ExecuteAsync(context);
        Assert.AreEqual("HELLO WORLD", context.CurrentText);

        await lower.ExecuteAsync(context);
        Assert.AreEqual("hello world", context.CurrentText);

        await title.ExecuteAsync(context);
        Assert.AreEqual("Hello World", context.CurrentText);
    }

    [TestMethod]
    public async Task TextStatisticsPlugin_GeneratesAccurateSummary()
    {
        var plugin = new TextStatisticsPlugin();
        var context = new MockPluginContext
        {
            CurrentText = "First line\nSecond line with words\n\nFourth line"
        };

        await plugin.ExecuteAsync(context);

        Assert.IsNotNull(context.LastMessageBody);
        Assert.IsTrue(context.LastMessageBody.Contains("Total Lines: 4"));
        Assert.IsTrue(context.LastMessageBody.Contains("Non-Empty Lines: 3"));
        Assert.IsTrue(context.LastMessageBody.Contains("Total Words: 8"));
    }
}
