using System;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta19FeatureTests
{
    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBetaVersion()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.AreEqual("1.2.0-beta.20", version);
        StringAssert.StartsWith(version, "1.2.0-beta.");

        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.19", "1.2.0-beta.18");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.19 must be recognized as newer than v1.2.0-beta.18");

        var cmp20 = AppUpdateService.CompareVersions("1.2.0-beta.20", "1.2.0-beta.19");
        Assert.IsTrue(cmp20 > 0, "v1.2.0-beta.20 must be recognized as newer than v1.2.0-beta.19");
    }

    [TestMethod]
    public void DocumentViewModel_IconGlyph_ResolvesCorrectFileBadges()
    {
        Assert.AreEqual("🔷", DocumentViewModel.GetIconForFile("Program.cs"));
        Assert.AreEqual("🐍", DocumentViewModel.GetIconForFile("script.py"));
        Assert.AreEqual("🎯", DocumentViewModel.GetIconForFile("main.dart"));
        Assert.AreEqual("🤖", DocumentViewModel.GetIconForFile("agent.agent"));
        Assert.AreEqual("📜", DocumentViewModel.GetIconForFile("routine.ps2"));
        Assert.AreEqual("📝", DocumentViewModel.GetIconForFile("README.md"));
        Assert.AreEqual("⚙️", DocumentViewModel.GetIconForFile("settings.json"));
        Assert.AreEqual("🌐", DocumentViewModel.GetIconForFile("index.html"));
        Assert.AreEqual("🎨", DocumentViewModel.GetIconForFile("style.css"));
        Assert.AreEqual("🗄️", DocumentViewModel.GetIconForFile("query.sql"));
        Assert.AreEqual("💻", DocumentViewModel.GetIconForFile("build.sh"));
        Assert.AreEqual("📄", DocumentViewModel.GetIconForFile("unknown.xyz"));
        Assert.AreEqual("📄", DocumentViewModel.GetIconForFile(""));
    }

    [TestMethod]
    public void DocumentViewModel_IconGlyph_UpdatesOnDocumentViewModel()
    {
        var langService = new LanguageService();
        var model = DocumentModel.CreateNew("app.cs");
        var vm = new DocumentViewModel(model, langService);

        Assert.AreEqual("app.cs", vm.Title);
        Assert.AreEqual("🔷", vm.IconGlyph);
    }
}
