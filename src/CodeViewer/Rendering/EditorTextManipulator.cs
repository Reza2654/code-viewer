using System;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace CodeViewer.Rendering;

/// <summary>
/// Provides advanced line and text document manipulation for AvaloniaEdit (duplicating lines, moving lines, commenting, deletion).
/// </summary>
public static class EditorTextManipulator
{
    public static void GoToLine(TextEditor editor, int line, int col)
    {
        if (editor.Document == null) return;

        var targetLine = Math.Clamp(line, 1, editor.Document.LineCount);
        var docLine = editor.Document.GetLineByNumber(targetLine);
        var targetCol = Math.Clamp(col, 1, Math.Max(1, docLine.Length + 1));
        var targetOffset = docLine.Offset + targetCol - 1;

        editor.TextArea.Caret.Offset = targetOffset;
        editor.ScrollTo(targetLine, targetCol);
        editor.Focus();
    }

    public static void ToggleComment(TextEditor editor, string? commentPrefix)
    {
        if (editor.Document == null) return;
        ToggleComment(editor.Document, editor.SelectionStart, editor.SelectionLength, commentPrefix);
    }

    public static void ToggleComment(TextDocument document, int selectionStart, int selectionLength, string? commentPrefix)
    {
        if (document == null || string.IsNullOrEmpty(commentPrefix)) return;

        var prefixTrimmed = commentPrefix.Trim();
        var startOffset = selectionStart;
        var length = selectionLength;

        var startLineNum = document.GetLineByOffset(startOffset).LineNumber;
        var endLineNum = length > 0
            ? document.GetLineByOffset(startOffset + length).LineNumber
            : startLineNum;

        var allCommented = true;
        var hasNonEmptyLine = false;

        for (int l = startLineNum; l <= endLineNum; l++)
        {
            var line = document.GetLineByNumber(l);
            var lineText = document.GetText(line.Offset, line.Length);
            var trimmed = lineText.TrimStart();
            if (trimmed.Length == 0) continue;

            hasNonEmptyLine = true;
            if (!trimmed.StartsWith(prefixTrimmed, StringComparison.Ordinal))
            {
                allCommented = false;
                break;
            }
        }

        if (!hasNonEmptyLine) return;

        using (document.RunUpdate())
        {
            for (int l = startLineNum; l <= endLineNum; l++)
            {
                var line = document.GetLineByNumber(l);
                var lineText = document.GetText(line.Offset, line.Length);
                var leadingSpaces = lineText.Length - lineText.TrimStart().Length;

                if (allCommented)
                {
                    var trimmed = lineText.TrimStart();
                    if (trimmed.StartsWith(prefixTrimmed, StringComparison.Ordinal))
                    {
                        var removeLen = prefixTrimmed.Length;
                        if (trimmed.Length > prefixTrimmed.Length && trimmed[prefixTrimmed.Length] == ' ')
                        {
                            removeLen++;
                        }
                        document.Remove(line.Offset + leadingSpaces, removeLen);
                    }
                }
                else
                {
                    if (lineText.Trim().Length > 0)
                    {
                        document.Insert(line.Offset + leadingSpaces, commentPrefix);
                    }
                }
            }
        }
    }

    public static void DuplicateLineOrSelection(TextEditor editor)
    {
        if (editor.Document == null) return;
        if (DuplicateLineOrSelection(editor.Document, editor.SelectionStart, editor.SelectionLength, editor.CaretOffset,
            out var newCaret, out var newSelStart, out var newSelLength))
        {
            if (newSelLength > 0)
            {
                editor.Select(newSelStart, newSelLength);
            }
            else
            {
                editor.CaretOffset = newCaret;
            }
        }
    }

    public static bool DuplicateLineOrSelection(TextDocument document, int selectionStart, int selectionLength, int caretOffset,
        out int newCaretOffset, out int newSelectionStart, out int newSelectionLength)
    {
        newCaretOffset = caretOffset;
        newSelectionStart = selectionStart;
        newSelectionLength = selectionLength;

        if (document == null) return false;

        using (document.RunUpdate())
        {
            if (selectionLength > 0)
            {
                var selectedText = document.GetText(selectionStart, selectionLength);
                var insertPos = selectionStart + selectionLength;
                document.Insert(insertPos, selectedText);
                newSelectionStart = insertPos;
                newSelectionLength = selectedText.Length;
                newCaretOffset = insertPos + selectedText.Length;
                return true;
            }
            else
            {
                var line = document.GetLineByOffset(caretOffset);
                var lineText = document.GetText(line.Offset, line.Length);
                var delimiter = document.GetText(line.Offset + line.Length, line.DelimiterLength);
                if (string.IsNullOrEmpty(delimiter)) delimiter = Environment.NewLine;

                var insertOffset = line.EndOffset;
                var textToInsert = delimiter + lineText;
                document.Insert(insertOffset, textToInsert);
                newCaretOffset = Math.Min(document.TextLength, insertOffset + textToInsert.Length);
                return true;
            }
        }
    }

    public static void DeleteCurrentLine(TextEditor editor)
    {
        if (editor.Document == null) return;
        DeleteCurrentLine(editor.Document, editor.SelectionStart, editor.SelectionLength);
    }

    public static void DeleteCurrentLine(TextDocument document, int selectionStart, int selectionLength)
    {
        if (document == null) return;

        using (document.RunUpdate())
        {
            var startOffset = selectionStart;
            var length = selectionLength;
            var startLine = document.GetLineByOffset(startOffset);
            var endLine = length > 0 ? document.GetLineByOffset(startOffset + length) : startLine;

            var removeOffset = startLine.Offset;
            var removeLength = endLine.TotalLength;
            if (endLine.LineNumber == document.LineCount && startLine.LineNumber > 1)
            {
                var prevLine = document.GetLineByNumber(startLine.LineNumber - 1);
                removeOffset = prevLine.EndOffset;
                removeLength = endLine.EndOffset - prevLine.EndOffset;
            }

            document.Remove(removeOffset, removeLength);
        }
    }

    public static void MoveSelectedLinesUp(TextEditor editor)
    {
        if (editor.Document == null) return;
        if (MoveSelectedLinesUp(editor.Document, editor.SelectionStart, editor.SelectionLength, out var newStart, out var newLen))
        {
            editor.Select(newStart, newLen);
        }
    }

    public static bool MoveSelectedLinesUp(TextDocument document, int selectionStart, int selectionLength, out int newSelectionStart, out int newSelectionLength)
    {
        newSelectionStart = selectionStart;
        newSelectionLength = selectionLength;

        if (document == null) return false;

        var startOffset = selectionStart;
        var length = selectionLength;
        var startLine = document.GetLineByOffset(startOffset);
        var endLine = length > 0 ? document.GetLineByOffset(startOffset + length) : startLine;

        if (startLine.LineNumber <= 1) return false;

        var prevLine = document.GetLineByNumber(startLine.LineNumber - 1);

        using (document.RunUpdate())
        {
            var pStart = prevLine.Offset;
            var pEnd = prevLine.EndOffset;
            var blockStart = startLine.Offset;
            var blockEnd = endLine.EndOffset;

            var pText = document.GetText(pStart, pEnd - pStart);
            var delimiter = document.GetText(pEnd, blockStart - pEnd);
            var blockText = document.GetText(blockStart, blockEnd - blockStart);

            var combinedNewText = blockText + delimiter + pText;
            var totalOldLength = blockEnd - pStart;

            document.Replace(pStart, totalOldLength, combinedNewText);

            var shift = blockStart - pStart;
            newSelectionStart = Math.Max(0, startOffset - shift);
            newSelectionLength = length;
            return true;
        }
    }

    public static void MoveSelectedLinesDown(TextEditor editor)
    {
        if (editor.Document == null) return;
        if (MoveSelectedLinesDown(editor.Document, editor.SelectionStart, editor.SelectionLength, out var newStart, out var newLen))
        {
            editor.Select(newStart, newLen);
        }
    }

    public static bool MoveSelectedLinesDown(TextDocument document, int selectionStart, int selectionLength, out int newSelectionStart, out int newSelectionLength)
    {
        newSelectionStart = selectionStart;
        newSelectionLength = selectionLength;

        if (document == null) return false;

        var startOffset = selectionStart;
        var length = selectionLength;
        var startLine = document.GetLineByOffset(startOffset);
        var endLine = length > 0 ? document.GetLineByOffset(startOffset + length) : startLine;

        if (endLine.LineNumber >= document.LineCount) return false;

        var nextLine = document.GetLineByNumber(endLine.LineNumber + 1);

        using (document.RunUpdate())
        {
            var blockStart = startLine.Offset;
            var blockEnd = endLine.EndOffset;
            var nStart = nextLine.Offset;
            var nEnd = nextLine.EndOffset;

            var blockText = document.GetText(blockStart, blockEnd - blockStart);
            var delimiter = document.GetText(blockEnd, nStart - blockEnd);
            var nText = document.GetText(nStart, nEnd - nStart);

            var combinedNewText = nText + delimiter + blockText;
            var totalOldLength = nEnd - blockStart;

            document.Replace(blockStart, totalOldLength, combinedNewText);

            var shift = nEnd - blockEnd;
            newSelectionStart = startOffset + shift;
            newSelectionLength = length;
            return true;
        }
    }
}
