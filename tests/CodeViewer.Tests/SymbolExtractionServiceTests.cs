using System.Linq;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class SymbolExtractionServiceTests
{
    private SymbolExtractionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new SymbolExtractionService();
    }

    [TestMethod]
    public void ExtractSymbols_EmptyOrNull_ReturnsEmptyList()
    {
        Assert.AreEqual(0, _service.ExtractSymbols(null, "csharp").Count);
        Assert.AreEqual(0, _service.ExtractSymbols("", "markdown").Count);
        Assert.AreEqual(0, _service.ExtractSymbols("   ", "python").Count);
    }

    [TestMethod]
    public void ExtractMarkdown_ExtractsHeadingsWithCorrectLevels()
    {
        var md = @"# Main Title
Intro text

## Section 1
Content here

### Sub Section 1.1
More content

## Section 2
Final text";

        var symbols = _service.ExtractSymbols(md, "markdown");

        Assert.AreEqual(4, symbols.Count);

        Assert.AreEqual("Main Title", symbols[0].Name);
        Assert.AreEqual(SymbolKind.Heading, symbols[0].Kind);
        Assert.AreEqual(0, symbols[0].Level);
        Assert.AreEqual(1, symbols[0].LineNumber);

        Assert.AreEqual("Section 1", symbols[1].Name);
        Assert.AreEqual(1, symbols[1].Level);
        Assert.AreEqual(4, symbols[1].LineNumber);

        Assert.AreEqual("Sub Section 1.1", symbols[2].Name);
        Assert.AreEqual(2, symbols[2].Level);
        Assert.AreEqual(7, symbols[2].LineNumber);

        Assert.AreEqual("Section 2", symbols[3].Name);
        Assert.AreEqual(1, symbols[3].Level);
        Assert.AreEqual(10, symbols[3].LineNumber);
    }

    [TestMethod]
    public void ExtractPython_ExtractsClassesAndMethodsWithIndentation()
    {
        var py = @"class DataProcessor(BaseProcessor):
    def __init__(self, name):
        self.name = name

    async def process_async(self, data):
        return data

def standalone_helper(x):
    return x * 2";

        var symbols = _service.ExtractSymbols(py, "python");

        Assert.AreEqual(4, symbols.Count);

        Assert.AreEqual("DataProcessor", symbols[0].Name);
        Assert.AreEqual(SymbolKind.Class, symbols[0].Kind);
        Assert.AreEqual(0, symbols[0].Level);
        Assert.AreEqual(1, symbols[0].LineNumber);

        Assert.AreEqual("__init__", symbols[1].Name);
        Assert.AreEqual(SymbolKind.Method, symbols[1].Kind);
        Assert.AreEqual(1, symbols[1].Level);
        Assert.AreEqual(2, symbols[1].LineNumber);

        Assert.AreEqual("process_async", symbols[2].Name);
        Assert.AreEqual(SymbolKind.Method, symbols[2].Kind);
        Assert.AreEqual(1, symbols[2].Level);
        Assert.AreEqual(5, symbols[2].LineNumber);

        Assert.AreEqual("standalone_helper", symbols[3].Name);
        Assert.AreEqual(SymbolKind.Function, symbols[3].Kind);
        Assert.AreEqual(0, symbols[3].Level);
        Assert.AreEqual(8, symbols[3].LineNumber);
    }

    [TestMethod]
    public void ExtractCSharp_ExtractsTypesMethodsAndProperties()
    {
        var cs = @"namespace MyApp;

public interface IService
{
}

public class OrderManager
{
    public string OrderId { get; set; }

    public async Task<int> CalculateTotalAsync(int quantity, double price)
    {
        if (quantity <= 0) return 0;
        return 100;
    }
}

public enum OrderState
{
    Pending,
    Shipped
}";

        var symbols = _service.ExtractSymbols(cs, "csharp");

        Assert.IsTrue(symbols.Any(s => s.Name == "IService" && s.Kind == SymbolKind.Interface));
        Assert.IsTrue(symbols.Any(s => s.Name == "OrderManager" && s.Kind == SymbolKind.Class));
        Assert.IsTrue(symbols.Any(s => s.Name == "OrderId" && s.Kind == SymbolKind.Property));
        Assert.IsTrue(symbols.Any(s => s.Name == "CalculateTotalAsync" && s.Kind == SymbolKind.Method));
        Assert.IsTrue(symbols.Any(s => s.Name == "OrderState" && s.Kind == SymbolKind.Enum));
    }

    [TestMethod]
    public void ExtractJavaScript_ExtractsClassesFunctionsAndArrows()
    {
        var js = @"export class AppRouter {
    navigate(path) {
    }
}

export function initializeApp() {
}

const formatNumber = (num) => {
    return num.toFixed(2);
};";

        var symbols = _service.ExtractSymbols(js, "javascript");

        Assert.AreEqual(4, symbols.Count);
        Assert.AreEqual("AppRouter", symbols[0].Name);
        Assert.AreEqual(SymbolKind.Class, symbols[0].Kind);

        Assert.AreEqual("navigate", symbols[1].Name);
        Assert.AreEqual(SymbolKind.Method, symbols[1].Kind);

        Assert.AreEqual("initializeApp", symbols[2].Name);
        Assert.AreEqual(SymbolKind.Function, symbols[2].Kind);

        Assert.AreEqual("formatNumber", symbols[3].Name);
        Assert.AreEqual(SymbolKind.Function, symbols[3].Kind);
    }

    [TestMethod]
    public void ExtractPowerShell_ExtractsFunctions()
    {
        var ps1 = @"function Start-Build {
    param($Target)
}

filter Get-ActiveProcess {
}";

        var symbols = _service.ExtractSymbols(ps1, "powershell");

        Assert.AreEqual(2, symbols.Count);
        Assert.AreEqual("Start-Build", symbols[0].Name);
        Assert.AreEqual("Get-ActiveProcess", symbols[1].Name);
    }

    [TestMethod]
    public void ExtractBash_ExtractsFunctions()
    {
        var sh = @"#!/usr/bin/env bash

function deploy_app() {
    echo ""Deploying""
}

cleanup() {
    rm -rf /tmp/test
}";

        var symbols = _service.ExtractSymbols(sh, "bash");

        Assert.AreEqual(2, symbols.Count);
        Assert.AreEqual("deploy_app", symbols[0].Name);
        Assert.AreEqual("cleanup", symbols[1].Name);
    }

    [TestMethod]
    public void ExtractYamlAndJson_ExtractsKeys()
    {
        var yaml = @"name: CodeViewer
version: 1.2.0
settings:
  theme: dark-plus
  fontSize: 14";

        var yamlSymbols = _service.ExtractSymbols(yaml, "yaml");
        Assert.IsTrue(yamlSymbols.Any(s => s.Name == "name" && s.Kind == SymbolKind.Key));
        Assert.IsTrue(yamlSymbols.Any(s => s.Name == "settings" && s.Kind == SymbolKind.Key));

        var json = @"{
  ""id"": ""code-viewer"",
  ""active"": true
}";

        var jsonSymbols = _service.ExtractSymbols(json, "json");
        Assert.IsTrue(jsonSymbols.Any(s => s.Name == "id" && s.Kind == SymbolKind.Key));
        Assert.IsTrue(jsonSymbols.Any(s => s.Name == "active" && s.Kind == SymbolKind.Key));
    }
}
