using System.Collections.Generic;
using System.Linq;
using CodeViewer.Models;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class OutlineViewModelTests
{
    private class MockSymbolExtractionService : ISymbolExtractionService
    {
        public List<SymbolItem> MockSymbols { get; } = [];

        public IReadOnlyList<SymbolItem> ExtractSymbols(string? text, string? languageOrExtension)
        {
            return MockSymbols;
        }
    }

    [TestMethod]
    public void InitialState_IsEmptyWithNoActiveDocument()
    {
        var vm = new OutlineViewModel();

        Assert.IsTrue(vm.IsEmpty);
        Assert.IsFalse(vm.HasSymbols);
        Assert.AreEqual(0, vm.SymbolCount);
        Assert.AreEqual("No Active Document", vm.DocumentTitle);
        Assert.AreEqual(0, vm.Symbols.Count);
        Assert.AreEqual(0, vm.FilteredSymbols.Count);
    }

    [TestMethod]
    public void AttachDocument_ExtractsSymbolsAndUpdatesState()
    {
        var mock = new MockSymbolExtractionService();
        mock.MockSymbols.Add(new SymbolItem { Name = "MainClass", Kind = SymbolKind.Class, LineNumber = 5, ColumnNumber = 1 });
        mock.MockSymbols.Add(new SymbolItem { Name = "ProcessData", Kind = SymbolKind.Method, LineNumber = 12, ColumnNumber = 5 });

        var vm = new OutlineViewModel(mock);
        var docModel = new DocumentModel
        {
            Title = "App.cs",
            FilePath = @"C:\src\App.cs",
            Text = "class MainClass { void ProcessData() {} }"
        };
        var docVm = new DocumentViewModel(docModel, new LanguageService());

        vm.AttachDocument(docVm);

        Assert.IsFalse(vm.IsEmpty);
        Assert.IsTrue(vm.HasSymbols);
        Assert.AreEqual(2, vm.SymbolCount);
        Assert.AreEqual("App.cs", vm.DocumentTitle);
        Assert.AreEqual(2, vm.FilteredSymbols.Count);
        Assert.AreEqual("MainClass", vm.FilteredSymbols[0].Name);
        Assert.AreEqual("ProcessData", vm.FilteredSymbols[1].Name);
    }

    [TestMethod]
    public void Filter_FiltersSymbolsByNameAndKind()
    {
        var mock = new MockSymbolExtractionService();
        mock.MockSymbols.Add(new SymbolItem { Name = "UserRepository", Kind = SymbolKind.Class, LineNumber = 1 });
        mock.MockSymbols.Add(new SymbolItem { Name = "FindById", Kind = SymbolKind.Method, LineNumber = 10 });
        mock.MockSymbols.Add(new SymbolItem { Name = "SaveUser", Kind = SymbolKind.Method, LineNumber = 20 });

        var vm = new OutlineViewModel(mock);
        var docVm = new DocumentViewModel(new DocumentModel { Title = "Repo.cs" }, new LanguageService());
        vm.AttachDocument(docVm);

        // Filter by name
        vm.FilterText = "User";
        Assert.AreEqual(2, vm.FilteredSymbols.Count);
        Assert.IsTrue(vm.FilteredSymbols.Any(s => s.Name == "UserRepository"));
        Assert.IsTrue(vm.FilteredSymbols.Any(s => s.Name == "SaveUser"));

        // Filter by kind
        vm.FilterText = "Method";
        Assert.AreEqual(2, vm.FilteredSymbols.Count);
        Assert.IsTrue(vm.FilteredSymbols.All(s => s.Kind == SymbolKind.Method));

        // Clear filter
        vm.ClearFilter();
        Assert.AreEqual(string.Empty, vm.FilterText);
        Assert.AreEqual(3, vm.FilteredSymbols.Count);
    }

    [TestMethod]
    public void AttachDocument_Null_ClearsSymbolsAndResetsTitle()
    {
        var mock = new MockSymbolExtractionService();
        mock.MockSymbols.Add(new SymbolItem { Name = "Temp", Kind = SymbolKind.Class });

        var vm = new OutlineViewModel(mock);
        var docVm = new DocumentViewModel(new DocumentModel { Title = "Test.cs" }, new LanguageService());
        vm.AttachDocument(docVm);
        Assert.AreEqual(1, vm.Symbols.Count);

        vm.AttachDocument(null);
        Assert.AreEqual(0, vm.Symbols.Count);
        Assert.AreEqual(0, vm.FilteredSymbols.Count);
        Assert.IsTrue(vm.IsEmpty);
        Assert.AreEqual("No Active Document", vm.DocumentTitle);
    }

    [TestMethod]
    public void NavigateToSymbol_InvokesRequestGoToLine()
    {
        var vm = new OutlineViewModel();
        int jumpedLine = -1;
        int jumpedCol = -1;

        vm.RequestGoToLine = (l, c) =>
        {
            jumpedLine = l;
            jumpedCol = c;
        };

        var targetSymbol = new SymbolItem
        {
            Name = "TargetFunction",
            Kind = SymbolKind.Function,
            LineNumber = 42,
            ColumnNumber = 7
        };

        vm.NavigateToSymbol(targetSymbol);

        Assert.AreEqual(42, jumpedLine);
        Assert.AreEqual(7, jumpedCol);
    }

    [TestMethod]
    public void SelectedSymbol_ChangeTriggersNavigation()
    {
        var vm = new OutlineViewModel();
        int jumpedLine = -1;

        vm.RequestGoToLine = (l, _) => jumpedLine = l;

        vm.SelectedSymbol = new SymbolItem
        {
            Name = "HandleEvent",
            Kind = SymbolKind.Method,
            LineNumber = 99
        };

        Assert.AreEqual(99, jumpedLine);
    }
}
