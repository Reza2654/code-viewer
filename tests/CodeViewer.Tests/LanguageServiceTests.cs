using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class LanguageServiceTests
{
    private LanguageService _languageService = null!;

    [TestInitialize]
    public void Setup()
    {
        _languageService = new LanguageService();
    }

    [TestMethod]
    [DataRow("app.dart", "Dart")]
    [DataRow("Program.cs", "C#")]
    [DataRow("script.csx", "C#")]
    [DataRow("main.c", "C")]
    [DataRow("header.h", "C/C++ Header")]
    [DataRow("main.cpp", "C++")]
    [DataRow("Service.java", "Java")]
    [DataRow("MainActivity.kt", "Kotlin")]
    [DataRow("index.js", "JavaScript")]
    [DataRow("app.tsx", "TypeScript")]
    [DataRow("server.ts", "TypeScript")]
    [DataRow("script.py", "Python")]
    [DataRow("index.html", "HTML")]
    [DataRow("style.css", "CSS")]
    [DataRow("data.json", "JSON")]
    [DataRow("schema.xml", "XML")]
    [DataRow("View.axaml", "XML")]
    [DataRow("config.yaml", "YAML")]
    [DataRow("README.md", "Markdown")]
    [DataRow("queries.sql", "SQL")]
    [DataRow("deploy.ps1", "PowerShell")]
    [DataRow("build.sh", "Shell / Bash")]
    [DataRow("Dockerfile", "Docker")]
    public void DetectLanguage_MapsExtensionsCorrectly(string fileName, string expectedLanguage)
    {
        var actual = _languageService.DetectLanguage(fileName);
        Assert.AreEqual(expectedLanguage, actual);
    }

    [TestMethod]
    public void DetectLanguage_UnknownExtension_DefaultsToPlainText()
    {
        var actual = _languageService.DetectLanguage("unknown_file.xyz_abc");
        Assert.AreEqual("Plain Text", actual);
    }
}
