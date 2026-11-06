using System;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace CodeViewer.Rendering;

/// <summary>
/// Encapsulates auto-closing brackets, quote wrapping, overtyping, and pair backspace deletion in AvaloniaEdit.
/// </summary>
public static class SmartBracketEditHandler
{
    public static bool HandleTextEntering(TextEditor editor, TextInputEventArgs e, bool autoCloseBrackets)
    {
        if (!autoCloseBrackets || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1 || editor.Document == null) return false;

        char ch = e.Text[0];
        if (ProcessCharInput(editor.Document, ch, editor.SelectionStart, editor.SelectionLength, editor.CaretOffset,
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
            e.Handled = true;
            return true;
        }

        return false;
    }

    public static bool ProcessCharInput(TextDocument doc, char ch, int selectionStart, int selectionLength, int caretOffset,
        out int newCaretOffset, out int newSelectionStart, out int newSelectionLength)
    {
        newCaretOffset = caretOffset;
        newSelectionStart = selectionStart;
        newSelectionLength = 0;

        if (doc == null) return false;

        // 1. Text Selection Wrapping: wrap selection with pair
        if (selectionLength > 0)
        {
            char? closingChar = ch switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                '"' => '"',
                '\'' => '\'',
                '`' => '`',
                _ => null
            };

            if (closingChar.HasValue)
            {
                var selectedText = doc.GetText(selectionStart, selectionLength);
                doc.Replace(selectionStart, selectionLength, $"{ch}{selectedText}{closingChar.Value}");
                newSelectionStart = selectionStart + 1;
                newSelectionLength = selectionLength;
                newCaretOffset = newSelectionStart;
                return true;
            }
        }

        // 2. Overtyping existing closing character
        if (ch is ')' or ']' or '}' or '"' or '\'' or '`')
        {
            if (caretOffset < doc.TextLength && doc.GetCharAt(caretOffset) == ch)
            {
                newCaretOffset = caretOffset + 1;
                return true;
            }
        }

        // 3. Auto-insert matching closing pair for opening characters
        char? pairClose = ch switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            '"' => '"',
            '\'' => '\'',
            '`' => '`',
            _ => null
        };

        if (pairClose.HasValue)
        {
            // For quotes, don't auto-close if preceded by alphanumeric character (e.g. contractions like don't)
            if (ch is '\'' or '"' or '`' && caretOffset > 0)
            {
                var prevChar = doc.GetCharAt(caretOffset - 1);
                if (char.IsLetterOrDigit(prevChar))
                {
                    return false;
                }
            }

            // Don't auto-close if followed immediately by alphanumeric (e.g. typing inside a word)
            if (caretOffset < doc.TextLength && char.IsLetterOrDigit(doc.GetCharAt(caretOffset)))
            {
                return false;
            }

            doc.Insert(caretOffset, $"{ch}{pairClose.Value}");
            newCaretOffset = caretOffset + 1;
            return true;
        }

        return false;
    }

    public static bool HandleKeyDown(TextEditor editor, KeyEventArgs e, bool autoCloseBrackets)
    {
        if (!autoCloseBrackets || editor.Document == null) return false;

        if (e.Key == Key.Back && editor.SelectionLength == 0)
        {
            if (ProcessBackspace(editor.Document, editor.CaretOffset, out var newCaret))
            {
                editor.CaretOffset = newCaret;
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    public static bool ProcessBackspace(TextDocument doc, int caretOffset, out int newCaretOffset)
    {
        newCaretOffset = caretOffset;
        if (doc == null || caretOffset <= 0 || caretOffset >= doc.TextLength) return false;

        char prev = doc.GetCharAt(caretOffset - 1);
        char next = doc.GetCharAt(caretOffset);

        bool isPair = (prev == '(' && next == ')') ||
                      (prev == '[' && next == ']') ||
                      (prev == '{' && next == '}') ||
                      (prev == '"' && next == '"') ||
                      (prev == '\'' && next == '\'') ||
                      (prev == '`' && next == '`');

        if (isPair)
        {
            doc.Remove(caretOffset - 1, 2);
            newCaretOffset = caretOffset - 1;
            return true;
        }

        return false;
    }
}
