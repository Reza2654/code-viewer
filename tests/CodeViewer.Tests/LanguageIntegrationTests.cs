using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class LanguageIntegrationTests
{
    private LanguageService _languageService = null!;

    [TestInitialize]
    public void Setup()
    {
        _languageService = new LanguageService();
    }

    [TestMethod]
    public void DetectLanguage_AgentFile_ReturnsAgentLang()
    {
        var lang = _languageService.DetectLanguage(@"C:\repos\agent\assistant.agent");
        Assert.AreEqual("AgentLang", lang);
    }

    [TestMethod]
    public void DetectLanguage_PS2File_ReturnsPS2()
    {
        var lang = _languageService.DetectLanguage(@"C:\scripts\pipeline.ps2");
        Assert.AreEqual("PS2", lang);
    }

    [TestMethod]
    public void DetectLanguage_PS2BundleFile_ReturnsPS2()
    {
        var lang = _languageService.DetectLanguage(@"C:\packages\deployment.ps2bundle");
        Assert.AreEqual("PS2", lang);
    }

    [TestMethod]
    public void AllSupportedLanguages_ContainsAgentLangAndPS2()
    {
        var languages = _languageService.GetSupportedLanguages();
        CollectionAssert.Contains(languages.ToList(), "AgentLang");
        CollectionAssert.Contains(languages.ToList(), "PS2");
    }

    [TestMethod]
    public void GetCommentPrefix_AgentLangAndPS2_ReturnsDoubleSlash()
    {
        Assert.AreEqual("// ", _languageService.GetCommentPrefix("AgentLang"));
        Assert.AreEqual("// ", _languageService.GetCommentPrefix("PS2"));
    }

    [TestMethod]
    public void GetHighlightingDefinition_AgentLang_ResolvesDefinition()
    {
        var def = _languageService.GetHighlightingDefinition("AgentLang");
        Assert.IsNotNull(def);
        Assert.AreEqual("AgentLang", def.Name);
    }

    [TestMethod]
    public void GetHighlightingDefinition_PS2_ResolvesDefinition()
    {
        var def = _languageService.GetHighlightingDefinition("PS2");
        Assert.IsNotNull(def);
        Assert.AreEqual("PS2", def.Name);
    }

    [TestMethod]
    public void MainViewModel_FilteredLanguageOptions_FiltersByQuery()
    {
        var vm = new MainViewModel(
            new FileService(),
            _languageService,
            new RecentFilesService(),
            new DialogService(),
            new AppConfig());

        vm.LanguageFilter = "agent";
        var options = vm.FilteredLanguageOptions.ToList();
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("AgentLang", options[0].Name);

        vm.LanguageFilter = "ps2";
        options = vm.FilteredLanguageOptions.ToList();
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("PS2", options[0].Name);
    }

    [TestMethod]
    public void MainViewModel_FilteredLanguageOptions_MarksActiveDocumentSelected()
    {
        var vm = new MainViewModel(
            new FileService(),
            _languageService,
            new RecentFilesService(),
            new DialogService(),
            new AppConfig());

        vm.CreateNewDocument();
        vm.SetLanguage("AgentLang");

        vm.LanguageFilter = string.Empty;
        var agentOption = vm.FilteredLanguageOptions.FirstOrDefault(o => o.Name == "AgentLang");
        Assert.IsNotNull(agentOption);
        Assert.IsTrue(agentOption.IsSelected);

        var plainTextOption = vm.FilteredLanguageOptions.FirstOrDefault(o => o.Name == "Plain Text");
        Assert.IsNotNull(plainTextOption);
        Assert.IsFalse(plainTextOption.IsSelected);
    }

    [TestMethod]
    public async Task IntegrationsUpdateService_ReturnsStatusForAgentLangAndPS2()
    {
        var service = new IntegrationsUpdateService();
        var statuses = await service.CheckIntegrationsAsync();

        Assert.IsNotNull(statuses);
        Assert.AreEqual(2, statuses.Count);

        var agentStatus = statuses.FirstOrDefault(s => s.LanguageName == "AgentLang");
        Assert.IsNotNull(agentStatus);
        Assert.AreEqual("v0.2.2", agentStatus.InstalledVersion);
        Assert.IsTrue(agentStatus.RepositoryUrl.Contains("Reza2654/AgentLang"));

        var ps2Status = statuses.FirstOrDefault(s => s.LanguageName.Contains("PS2"));
        Assert.IsNotNull(ps2Status);
        Assert.AreEqual("v0.4.0", ps2Status.InstalledVersion);
        Assert.IsTrue(ps2Status.RepositoryUrl.Contains("Reza2654/ps2"));
    }
}
