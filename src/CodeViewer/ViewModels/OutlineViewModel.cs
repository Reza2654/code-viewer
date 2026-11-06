using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

public partial class OutlineViewModel : ViewModelBase
{
    private readonly ISymbolExtractionService _extractionService;
    private DocumentViewModel? _currentDoc;
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty]
    private ObservableCollection<SymbolItem> _symbols = [];

    [ObservableProperty]
    private ObservableCollection<SymbolItem> _filteredSymbols = [];

    [ObservableProperty]
    private SymbolItem? _selectedSymbol;

    partial void OnSelectedSymbolChanged(SymbolItem? value)
    {
        if (value != null)
        {
            NavigateToSymbol(value);
        }
    }

    [ObservableProperty]
    private string _filterText = string.Empty;

    partial void OnFilterTextChanged(string value)
    {
        Filter(value);
    }

    [ObservableProperty]
    private string _documentTitle = "No Active Document";

    [ObservableProperty]
    private bool _hasSymbols;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _symbolCount;

    [ObservableProperty]
    private bool _isLoading;

    public Action<int, int>? RequestGoToLine { get; set; }

    public OutlineViewModel(ISymbolExtractionService? extractionService = null)
    {
        _extractionService = extractionService ?? new SymbolExtractionService();
    }

    public void AttachDocument(DocumentViewModel? doc)
    {
        if (_currentDoc != null)
        {
            try
            {
                _currentDoc.TextDocument.TextChanged -= OnDocumentTextChanged;
            }
            catch (InvalidOperationException)
            {
                // Multi-threading fallback
            }
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        _currentDoc = doc;

        if (_currentDoc == null)
        {
            DocumentTitle = "No Active Document";
            Symbols.Clear();
            FilteredSymbols.Clear();
            HasSymbols = false;
            IsEmpty = true;
            SymbolCount = 0;
            return;
        }

        DocumentTitle = _currentDoc.DisplayName;
        try
        {
            _currentDoc.TextDocument.TextChanged += OnDocumentTextChanged;
        }
        catch (InvalidOperationException)
        {
            // Multi-threading fallback
        }
        ExtractSymbolsImmediate();
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                RunOnUiThread(() =>
                {
                    ExtractSymbolsImmediate();
                });
            }
            catch (OperationCanceledException)
            {
                // Ignored - debounced typing
            }
        }, token);
    }

    public void ExtractSymbolsImmediate()
    {
        if (_currentDoc == null) return;

        string text;
        try
        {
            text = _currentDoc.TextDocument.Text;
        }
        catch (InvalidOperationException)
        {
            text = _currentDoc.Model.Text ?? string.Empty;
        }

        var lang = _currentDoc.Language ?? _currentDoc.FilePath;

        var extracted = _extractionService.ExtractSymbols(text, lang);

        Symbols.Clear();
        foreach (var sym in extracted)
        {
            Symbols.Add(sym);
        }

        SymbolCount = Symbols.Count;
        HasSymbols = SymbolCount > 0;
        IsEmpty = !HasSymbols;

        Filter(FilterText);
    }

    public void Filter(string? query)
    {
        FilteredSymbols.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var sym in Symbols)
            {
                FilteredSymbols.Add(sym);
            }
            return;
        }

        var trimmed = query.Trim();
        foreach (var sym in Symbols)
        {
            if (sym.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (sym.Detail != null && sym.Detail.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) ||
                sym.Kind.ToString().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSymbols.Add(sym);
            }
        }
    }

    [RelayCommand]
    public void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    public void NavigateToSymbol(SymbolItem? symbol)
    {
        if (symbol == null) return;
        RequestGoToLine?.Invoke(symbol.LineNumber, symbol.ColumnNumber);
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
