using System;
using System.IO;
using System.Linq;
using Avalonia.Input;
using AvaloniaEdit.Document;
using CodeViewer.Rendering;
using CodeViewer.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class UiBehaviorsTests
{
    [TestMethod]
    public void SmartBracketEditHandler_Disabled_DoesNotModify()
    {
        var doc = new TextDocument("var x = ");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, '(', 8, 0, 8,
            out var newCaret, out var newSelStart, out var newSelLen);

        // When ProcessCharInput is called directly, '(' triggers auto-insert
        Assert.IsTrue(handled);
        Assert.AreEqual("var x = ()", doc.Text);
        Assert.AreEqual(9, newCaret);
    }

    [TestMethod]
    public void SmartBracketEditHandler_WrapsSelection()
    {
        var doc = new TextDocument("123 + 456");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, '(', 0, 9, 0,
            out var newCaret, out var newSelStart, out var newSelLen);

        Assert.IsTrue(handled);
        Assert.AreEqual("(123 + 456)", doc.Text);
        Assert.AreEqual(1, newSelStart);
        Assert.AreEqual(9, newSelLen);
    }

    [TestMethod]
    public void SmartBracketEditHandler_OvertypesClosingBracket()
    {
        var doc = new TextDocument("foo()");
        var handled = SmartBracketEditHandler.ProcessCharInput(doc, ')', 0, 0, 4, // caret right before )
            out var newCaret, out var newSelStart, out var newSelLen);

        Assert.IsTrue(handled);
        Assert.AreEqual("foo()", doc.Text); // No duplicate ) inserted
        Assert.AreEqual(5, newCaret); // Caret advanced past )
    }

    [TestMethod]
    public void SmartBracketEditHandler_BackspaceDeletesPair()
    {
        var doc = new TextDocument("foo()");
        var handled = SmartBracketEditHandler.ProcessBackspace(doc, 4, out var newCaret);

        Assert.IsTrue(handled);
        Assert.AreEqual("foo", doc.Text);
        Assert.AreEqual(3, newCaret);
    }

    [TestMethod]
    public void EditorTextManipulator_ToggleComment_AddsAndRemoves()
    {
        var doc = new TextDocument("var a = 1;\nvar b = 2;");

        // 1. Toggle comment ON
        EditorTextManipulator.ToggleComment(doc, 0, doc.TextLength, "// ");
        Assert.AreEqual("// var a = 1;\n// var b = 2;", doc.Text);

        // 2. Toggle comment OFF
        EditorTextManipulator.ToggleComment(doc, 0, doc.TextLength, "// ");
        Assert.AreEqual("var a = 1;\nvar b = 2;", doc.Text);
    }

    [TestMethod]
    public void EditorTextManipulator_DuplicateLine()
    {
        var doc = new TextDocument("line 1\nline 2");

        EditorTextManipulator.DuplicateLineOrSelection(doc, 0, 0, 2,
            out var newCaret, out var newSelStart, out var newSelLen);

        StringAssert.Contains(doc.Text, "line 1");
        Assert.AreEqual(3, doc.LineCount);
    }

    [TestMethod]
    public void EditorTextManipulator_DeleteCurrentLine()
    {
        var doc = new TextDocument("first\nsecond\nthird");
        var secondLine = doc.GetLineByNumber(2);

        EditorTextManipulator.DeleteCurrentLine(doc, secondLine.Offset + 1, 0);

        Assert.AreEqual("first\nthird", doc.Text);
    }

    [TestMethod]
    public void EditorTextManipulator_MoveLinesUpAndDown()
    {
        var doc = new TextDocument("line1\nline2\nline3");
        var secondLine = doc.GetLineByNumber(2);

        // Move line 2 UP
        EditorTextManipulator.MoveSelectedLinesUp(doc, secondLine.Offset, secondLine.Length,
            out var newStart, out var newLen);
        Assert.AreEqual("line2\nline1\nline3", doc.Text);

        // Move line 2 DOWN (line 2 is now line 1)
        var firstLine = doc.GetLineByNumber(1);
        EditorTextManipulator.MoveSelectedLinesDown(doc, firstLine.Offset, firstLine.Length,
            out var downStart, out var downLen);
        Assert.AreEqual("line1\nline2\nline3", doc.Text);
    }

    [TestMethod]
    public void EditorSearchCoordinator_FindMatchOffsetsAndReplaceAll()
    {
        var doc = new TextDocument("apple orange apple banana apple");

        var matches = EditorSearchCoordinator.FindMatchOffsets(doc.Text, "apple", matchCase: false);
        Assert.AreEqual(3, matches.Count);
        Assert.AreEqual(0, matches[0]);
        Assert.AreEqual(13, matches[1]);
        Assert.AreEqual(26, matches[2]);

        var count = EditorSearchCoordinator.ReplaceAll(doc, "apple", "pear", matchCase: false);

        Assert.AreEqual(3, count);
        Assert.AreEqual("pear orange pear banana pear", doc.Text);
    }

    [TestMethod]
    public void FileDragDropHandler_ResolveDroppedPaths_ParsesUriText()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Text, new Uri(tempFile).AbsoluteUri);

            var paths = FileDragDropHandler.ResolveDroppedPaths(dataObject);

            Assert.AreEqual(1, paths.Count);
            Assert.AreEqual(Path.GetFullPath(tempFile), Path.GetFullPath(paths[0]));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
