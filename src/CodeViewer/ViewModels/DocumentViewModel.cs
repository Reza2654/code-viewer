using System;
using System.IO;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeViewer.ViewModels;

/// <summary>
/// ViewModel representing an individual open editor tab document.
/// </summary>
public partial class DocumentViewModel : ViewModelBase
{
    private readonly ILanguageService _languageService;
    private readonly DocumentModel _model;

    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _language = "Plain Text";

    [ObservableProperty]
    private string _encodingName = "UTF-8";

    [ObservableProperty]
    private string _lineEndingDisplay = "LF";

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private double _fontSize = 14.0;

    [ObservableProperty]
    private bool _wordWrap = false;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    [ObservableProperty]
    private IHighlightingDefinition? _highlightingDefinition;

    public TextDocument TextDocument { get; }

    public DocumentModel Model => _model;

    public string DisplayName => IsModified ? $"{Title} *" : Title;

    public string CaretDisplay => $"Ln {CaretLine}, Col {CaretColumn}";

    public DocumentViewModel(DocumentModel model, ILanguageService languageService, double defaultFontSize = 14.0)
    {
        _model = model;
        _languageService = languageService;
        _filePath = model.FilePath;
        _title = model.Title;
        _isModified = model.IsModified;
        _fontSize = defaultFontSize;

        // Initialize TextDocument with model text
        TextDocument = new TextDocument(model.Text);
        TextDocument.TextChanged += OnDocumentTextChanged;

        _encodingName = model.EncodingInfo.DisplayName;
        _lineEndingDisplay = model.LineEnding.ToDisplayString();

        // Update language and highlighting
        UpdateLanguage();
    }

    public void UpdateLanguage()
    {
        var lang = _languageService.DetectLanguage(FilePath);
        Language = lang;
        HighlightingDefinition = _languageService.GetHighlightingDefinition(lang);
        _model.Language = lang;
    }

    public void SyncToModel()
    {
        _model.Text = TextDocument.Text;
        _model.FilePath = FilePath;
        _model.Title = Title;
        _model.IsModified = IsModified;
    }

    public void MarkSaved(string savedPath, long fileSize, DateTime lastModified)
    {
        FilePath = savedPath;
        Title = Path.GetFileName(savedPath);
        IsModified = false;
        _model.FilePath = savedPath;
        _model.Title = Title;
        _model.FileSizeBytes = fileSize;
        _model.LastModifiedOnDisk = lastModified;
        _model.IsModified = false;
        UpdateLanguage();
        OnPropertyChanged(nameof(DisplayName));
    }

    public void UpdateCaretPosition(int line, int col)
    {
        CaretLine = line;
        CaretColumn = col;
        OnPropertyChanged(nameof(CaretDisplay));
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (!IsModified)
        {
            IsModified = true;
            _model.IsModified = true;
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
