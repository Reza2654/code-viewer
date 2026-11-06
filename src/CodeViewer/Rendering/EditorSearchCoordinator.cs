using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CodeViewer.ViewModels;

namespace CodeViewer.Rendering;

/// <summary>
/// Coordinates editor in-document search, replace, match highlighting, and navigation.
/// </summary>
public class EditorSearchCoordinator
{
    private readonly SearchHighlightRenderer? _searchHighlightRenderer;
    private readonly List<int> _searchMatchOffsets = new();
    private int _currentSearchMatchIndex = -1;

    public SearchHighlightRenderer? HighlightRenderer => _searchHighlightRenderer;
    public IReadOnlyList<int> MatchOffsets => _searchMatchOffsets;
    public int CurrentMatchIndex => _currentSearchMatchIndex;

    public EditorSearchCoordinator(SearchHighlightRenderer? highlightRenderer = null)
    {
        _searchHighlightRenderer = highlightRenderer;
    }

    public static List<int> FindMatchOffsets(string text, string query, bool matchCase)
    {
        var offsets = new List<int>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return offsets;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(query, index, comparison);
            if (found == -1) break;
            offsets.Add(found);
            index = found + Math.Max(query.Length, 1);
        }
        return offsets;
    }

    public static int ReplaceAll(TextDocument document, string query, string replacement, bool matchCase)
    {
        if (document == null || string.IsNullOrEmpty(query)) return 0;
        var offsets = FindMatchOffsets(document.Text, query, matchCase);
        if (offsets.Count == 0) return 0;

        using (document.RunUpdate())
        {
            for (int i = offsets.Count - 1; i >= 0; i--)
            {
                document.Replace(offsets[i], query.Length, replacement);
            }
        }
        return offsets.Count;
    }

    public void UpdateSearchMatches(TextEditor editor, SearchViewModel searchVm)
    {
        var query = searchVm.SearchText;
        _searchMatchOffsets.Clear();
        _currentSearchMatchIndex = -1;

        if (string.IsNullOrEmpty(query) || !searchVm.IsOpen)
        {
            _searchHighlightRenderer?.Clear();
            editor.TextArea?.TextView?.InvalidateVisual();
            searchVm.StatusText = string.Empty;
            searchVm.TotalMatches = 0;
            searchVm.CurrentMatchIndex = 0;
            return;
        }

        var text = editor.Text ?? string.Empty;
        var matches = FindMatchOffsets(text, query, searchVm.MatchCase);
        _searchMatchOffsets.AddRange(matches);
        searchVm.TotalMatches = _searchMatchOffsets.Count;

        if (_searchMatchOffsets.Count == 0)
        {
            _searchHighlightRenderer?.Clear();
            searchVm.StatusText = "No results";
            searchVm.CurrentMatchIndex = 0;
        }
        else
        {
            var caret = editor.SelectionStart;
            var closest = _searchMatchOffsets.FindIndex(m => m >= caret);
            if (closest == -1) closest = 0;
            _currentSearchMatchIndex = closest;

            searchVm.CurrentMatchIndex = _currentSearchMatchIndex + 1;
            searchVm.StatusText = $"{_currentSearchMatchIndex + 1} of {_searchMatchOffsets.Count}";

            var activeOffset = _searchMatchOffsets[_currentSearchMatchIndex];
            _searchHighlightRenderer?.SetMatches(_searchMatchOffsets, query.Length, activeOffset);
        }

        editor.TextArea?.TextView?.InvalidateVisual();
    }

    public void FindNext(TextEditor editor, SearchViewModel searchVm)
    {
        if (string.IsNullOrEmpty(searchVm.SearchText)) return;

        if (_searchMatchOffsets.Count == 0)
        {
            UpdateSearchMatches(editor, searchVm);
            if (_searchMatchOffsets.Count == 0) return;
        }

        _currentSearchMatchIndex = (_currentSearchMatchIndex + 1) % _searchMatchOffsets.Count;
        NavigateToMatch(editor, searchVm, _currentSearchMatchIndex);
    }

    public void FindPrevious(TextEditor editor, SearchViewModel searchVm)
    {
        if (string.IsNullOrEmpty(searchVm.SearchText)) return;

        if (_searchMatchOffsets.Count == 0)
        {
            UpdateSearchMatches(editor, searchVm);
            if (_searchMatchOffsets.Count == 0) return;
        }

        _currentSearchMatchIndex = (_currentSearchMatchIndex - 1 + _searchMatchOffsets.Count) % _searchMatchOffsets.Count;
        NavigateToMatch(editor, searchVm, _currentSearchMatchIndex);
    }

    public void NavigateToMatch(TextEditor editor, SearchViewModel searchVm, int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= _searchMatchOffsets.Count || editor.Document == null) return;

        var offset = _searchMatchOffsets[matchIndex];
        var query = searchVm.SearchText;

        editor.Select(offset, query.Length);
        var loc = editor.Document.GetLocation(offset);
        editor.ScrollTo(loc.Line, loc.Column);

        searchVm.CurrentMatchIndex = matchIndex + 1;
        searchVm.StatusText = $"{matchIndex + 1} of {_searchMatchOffsets.Count}";

        _searchHighlightRenderer?.SetActiveMatch(offset);
        editor.TextArea?.TextView?.InvalidateVisual();
    }

    public void Replace(TextEditor editor, SearchViewModel searchVm)
    {
        if (string.IsNullOrEmpty(searchVm.SearchText) || editor.Document == null) return;

        if (_currentSearchMatchIndex >= 0 && _currentSearchMatchIndex < _searchMatchOffsets.Count)
        {
            var offset = _searchMatchOffsets[_currentSearchMatchIndex];
            var query = searchVm.SearchText;
            var replacement = searchVm.ReplaceText ?? string.Empty;

            using (editor.Document.RunUpdate())
            {
                editor.Document.Replace(offset, query.Length, replacement);
            }

            UpdateSearchMatches(editor, searchVm);
        }
        else
        {
            FindNext(editor, searchVm);
        }
    }

    public void ReplaceAll(TextEditor editor, SearchViewModel searchVm, Func<string, Task>? notifyStatusCallback)
    {
        if (string.IsNullOrEmpty(searchVm.SearchText) || editor.Document == null) return;

        var count = ReplaceAll(editor.Document, searchVm.SearchText, searchVm.ReplaceText ?? string.Empty, searchVm.MatchCase);
        UpdateSearchMatches(editor, searchVm);

        if (notifyStatusCallback != null)
        {
            _ = notifyStatusCallback($"Replaced {count} occurrence(s)");
        }
    }
}
