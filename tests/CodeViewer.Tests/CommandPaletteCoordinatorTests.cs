using System;
using System.Linq;
using CodeViewer.Models;
using CodeViewer.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class CommandPaletteCoordinatorTests
{
    [TestMethod]
    public void Show_ResetsQueryAndSetsIsOpenTrue()
    {
        var coordinator = new CommandPaletteCoordinator();
        coordinator.Query = "previous search";
        coordinator.IsOpen = false;

        coordinator.Show();

        Assert.IsTrue(coordinator.IsOpen);
        Assert.AreEqual(string.Empty, coordinator.Query);
    }

    [TestMethod]
    public void Close_SetsIsOpenFalse()
    {
        var coordinator = new CommandPaletteCoordinator();
        coordinator.IsOpen = true;

        coordinator.Close();

        Assert.IsFalse(coordinator.IsOpen);
    }

    [TestMethod]
    public void Filter_WithEmptyQuery_ReturnsAllCommands()
    {
        var coordinator = new CommandPaletteCoordinator();
        coordinator.RegisterCommand(new CommandPaletteItem("1", "Save", "File", "Ctrl+S", "💾", () => { }));
        coordinator.RegisterCommand(new CommandPaletteItem("2", "Open", "File", "Ctrl+O", "📂", () => { }));

        coordinator.Filter(string.Empty);

        Assert.AreEqual(2, coordinator.FilteredCommands.Count);
        Assert.AreEqual("Save", coordinator.SelectedItem?.Title);
    }

    [TestMethod]
    public void Filter_MatchesByTitleCategoryAndShortcut()
    {
        var coordinator = new CommandPaletteCoordinator();
        coordinator.RegisterCommand(new CommandPaletteItem("1", "Save Document", "File", "Ctrl+S", "💾", () => { }));
        coordinator.RegisterCommand(new CommandPaletteItem("2", "Find Text", "Edit", "Ctrl+F", "🔍", () => { }));
        coordinator.RegisterCommand(new CommandPaletteItem("3", "Toggle Sidebar", "View", "Ctrl+B", "📁", () => { }));

        // Search by category
        coordinator.Filter("edit");
        Assert.AreEqual(1, coordinator.FilteredCommands.Count);
        Assert.AreEqual("Find Text", coordinator.FilteredCommands[0].Title);

        // Search by shortcut
        coordinator.Filter("Ctrl+B");
        Assert.AreEqual(1, coordinator.FilteredCommands.Count);
        Assert.AreEqual("Toggle Sidebar", coordinator.FilteredCommands[0].Title);

        // Search by title substring
        coordinator.Filter("doc");
        Assert.AreEqual(1, coordinator.FilteredCommands.Count);
        Assert.AreEqual("Save Document", coordinator.FilteredCommands[0].Title);
    }

    [TestMethod]
    public void ExecuteSelected_ClosesPaletteAndInvokesAction()
    {
        var coordinator = new CommandPaletteCoordinator();
        var executed = false;
        var item = new CommandPaletteItem("test", "Test Action", "General", "", "⚡", () => executed = true);
        coordinator.RegisterCommand(item);
        coordinator.Show();
        coordinator.SelectedItem = item;

        coordinator.ExecuteSelected();

        Assert.IsFalse(coordinator.IsOpen);
        Assert.IsTrue(executed);
    }

    [TestMethod]
    public void ExecuteCommandById_ExecutesMatchingAction()
    {
        var coordinator = new CommandPaletteCoordinator();
        var executed = false;
        coordinator.RegisterCommand(new CommandPaletteItem("target.id", "Target", "General", "", "⚡", () => executed = true));

        var success = coordinator.ExecuteCommandById("target.id");
        var notFound = coordinator.ExecuteCommandById("unknown.id");

        Assert.IsTrue(success);
        Assert.IsTrue(executed);
        Assert.IsFalse(notFound);
    }
}
