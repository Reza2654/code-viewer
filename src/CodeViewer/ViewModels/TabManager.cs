using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

/// <summary>
/// State of a closed document tab for undo/reopen operations.
/// </summary>
public record ClosedTabState(string? FilePath, string Title, string Content, int CaretLine, int CaretColumn, string Language);

/// <summary>
/// Coordinates document tabs lifecycle, file loading/saving, dirty confirmations,
/// closed tab history, external disk change detection, and tab context operations.
/// </summary>
public partial class TabManager : ViewModelBase, IDisposable
{
    private readonly IFileService _fileService;
    private readonly ILanguageService _languageService;
    private readonly IDialogService _dialogService;
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly AppConfig _config;

    private readonly Stack<ClosedTabState> _recentlyClosedTabs = new();
    private readonly HashSet<string> _activeReloadPrompts = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    [ObservableProperty]
    private DocumentViewModel? _activeDocument;

    [ObservableProperty]
    private ObservableCollection<DocumentViewModel> _documents = [];

    public bool HasDocuments => Documents.Count > 0;
    public bool HasNoDocuments => Documents.Count == 0;
    public bool CanReopenClosedTab => _recentlyClosedTabs.Count > 0;
    public IReadOnlyCollection<ClosedTabState> RecentlyClosedTabs => _recentlyClosedTabs;

    // Callbacks for orchestration
    public Action<int, int>? RequestGoToLine { get; set; }
    public Func<string, Task>? RequestSetClipboardText { get; set; }
    public Func<string, int, Task>? RequestStatusMessage { get; set; }
    public Func<string?>? GetWorkspaceRootPath { get; set; }
    public Func<ColorTheme?>? GetCurrentTheme { get; set; }
    public Func<Task>? RequestSaveSession { get; set; }
    public Func<Task>? RequestReloadRecentFiles { get; set; }
    public Action? ActiveDocumentChanged { get; set; }
    public Action? DocumentsChanged { get; set; }
    public Action? DocumentStateChanged { get; set; }

    public TabManager(
        IFileService fileService,
        ILanguageService languageService,
        IDialogService dialogService,
        IThemeService themeService,
        ISettingsService settingsService,
        IFileWatcherService fileWatcherService,
        IRecentFilesService recentFilesService,
        AppConfig config)
    {
        _fileService = fileService;
        _languageService = languageService;
        _dialogService = dialogService;
        _themeService = themeService;
        _settingsService = settingsService;
        _fileWatcherService = fileWatcherService;
        _recentFilesService = recentFilesService;
        _config = config;

        _documents.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(HasNoDocuments));
            DocumentsChanged?.Invoke();
        };

        _fileWatcherService.FileChangedOnDisk += OnFileChangedOnDisk;
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        UpdateActiveDocumentSelection();
        ActiveDocumentChanged?.Invoke();
        DocumentStateChanged?.Invoke();
    }

    public void UpdateActiveDocumentSelection()
    {
        foreach (var doc in Documents)
        {
            doc.IsActive = (doc == ActiveDocument);
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        foreach (var doc in Documents)
        {
            doc.FontSize = settings.FontSize;
            doc.WordWrap = settings.WordWrap;
            doc.ShowLineNumbers = settings.ShowLineNumbers;
            doc.TabSize = settings.TabSize;
        }
    }

    public void ApplyCodeColorsToAllDocuments(ColorTheme? theme = null)
    {
        var target = theme ?? GetCurrentTheme?.Invoke();
        if (target == null) return;

        _themeService.ApplyThemeToAllHighlightings(target);
        foreach (var doc in Documents)
        {
            if (doc.HighlightingDefinition != null)
            {
                _themeService.ApplyCodeColorsToHighlighting(doc.HighlightingDefinition, target);
            }
        }
    }

    [RelayCommand]
    public void CreateNewDocument()
    {
        var settings = _settingsService.CurrentSettings;
        var model = DocumentModel.CreateNew($"Untitled-{Documents.Count + 1}");
        var fontSize = _config.DefaultFontSize > 0 ? _config.DefaultFontSize : settings.FontSize;
        var docVm = new DocumentViewModel(model, _languageService, fontSize)
        {
            WordWrap = settings.WordWrap,
            ShowLineNumbers = settings.ShowLineNumbers,
            TabSize = settings.TabSize
        };

        var currentTheme = GetCurrentTheme?.Invoke();
        if (docVm.HighlightingDefinition != null && currentTheme != null)
        {
            _themeService.ApplyCodeColorsToHighlighting(docVm.HighlightingDefinition, currentTheme);
        }

        Documents.Add(docVm);
        ActiveDocument = docVm;
        UpdateActiveDocumentSelection();
        DocumentStateChanged?.Invoke();
        if (RequestSaveSession != null)
        {
            _ = RequestSaveSession();
        }
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var filePath = await _dialogService.ShowOpenFileDialogAsync();
        if (!string.IsNullOrEmpty(filePath))
        {
            await OpenFileInternalAsync(filePath);
        }
    }

    [RelayCommand]
    public async Task OpenRecentFileAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (!File.Exists(filePath))
        {
            await _recentFilesService.RemoveRecentFileAsync(filePath);
            if (RequestReloadRecentFiles != null)
            {
                await RequestReloadRecentFiles();
            }
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"File not found: {Path.GetFileName(filePath)}", 2500);
            }
            await _dialogService.ShowMessageAsync("File Not Found", $"The recent file '{filePath}' no longer exists on disk.\n\nIt has been removed from recent files.");
            return;
        }

        await OpenFileInternalAsync(filePath);
    }

    public async Task OpenFileInternalAsync(string filePath, int targetLine = 1, int targetCol = 1)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (!File.Exists(filePath))
        {
            await _recentFilesService.RemoveRecentFileAsync(filePath);
            if (RequestReloadRecentFiles != null)
            {
                await RequestReloadRecentFiles();
            }
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"File not found: {Path.GetFileName(filePath)}", 2500);
            }
            return;
        }

        var fullPath = Path.GetFullPath(filePath);

        // Check if file is already open in a tab
        var existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveDocument = existing;
            UpdateActiveDocumentSelection();
            if (targetLine > 1 || targetCol > 1)
            {
                RequestGoToLine?.Invoke(targetLine, targetCol);
            }
            return;
        }

        try
        {
            var settings = _settingsService.CurrentSettings;
            var model = await _fileService.OpenFileAsync(fullPath);
            var fontSize = _config.DefaultFontSize > 0 ? _config.DefaultFontSize : settings.FontSize;
            var docVm = new DocumentViewModel(model, _languageService, fontSize)
            {
                WordWrap = settings.WordWrap,
                ShowLineNumbers = settings.ShowLineNumbers,
                TabSize = settings.TabSize
            };

            var currentTheme = GetCurrentTheme?.Invoke();
            if (docVm.HighlightingDefinition != null && currentTheme != null)
            {
                _themeService.ApplyCodeColorsToHighlighting(docVm.HighlightingDefinition, currentTheme);
            }

            // If the only document is an untouched empty Untitled tab, replace it
            bool isUntouchedEmpty = false;
            if (Documents.Count == 1 && Documents[0].Model.IsNewFile && !Documents[0].IsModified)
            {
                try
                {
                    isUntouchedEmpty = Documents[0].TextDocument.TextLength == 0;
                }
                catch
                {
                    isUntouchedEmpty = string.IsNullOrEmpty(Documents[0].Model.Text);
                }
            }

            if (isUntouchedEmpty)
            {
                Documents[0] = docVm;
            }
            else
            {
                Documents.Add(docVm);
            }

            ActiveDocument = docVm;
            UpdateActiveDocumentSelection();
            _fileWatcherService.WatchFile(fullPath);
            await _recentFilesService.AddRecentFileAsync(fullPath);
            if (RequestReloadRecentFiles != null)
            {
                await RequestReloadRecentFiles();
            }
            DocumentStateChanged?.Invoke();
            if (RequestSaveSession != null)
            {
                _ = RequestSaveSession();
            }

            if (targetLine > 1 || targetCol > 1)
            {
                RequestGoToLine?.Invoke(targetLine, targetCol);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error Opening File", $"Could not open file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (ActiveDocument == null) return;

        if (ActiveDocument.Model.IsNewFile)
        {
            await SaveAsAsync();
            return;
        }

        try
        {
            if (ActiveDocument.FilePath != null)
            {
                _fileWatcherService.TemporarilyIgnore(ActiveDocument.FilePath);
            }

            ActiveDocument.SyncToModel();
            await _fileService.SaveFileAsync(ActiveDocument.Model);
            var fileInfo = new FileInfo(ActiveDocument.FilePath!);
            ActiveDocument.MarkSaved(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            _fileWatcherService.WatchFile(fileInfo.FullName);
            DocumentStateChanged?.Invoke();
            if (RequestSaveSession != null)
            {
                _ = RequestSaveSession();
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error Saving File", $"Could not save file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SaveAsAsync()
    {
        if (ActiveDocument == null) return;

        var defaultName = ActiveDocument.Title;
        var targetPath = await _dialogService.ShowSaveFileDialogAsync(defaultName);
        if (string.IsNullOrEmpty(targetPath)) return;

        try
        {
            _fileWatcherService.TemporarilyIgnore(targetPath);
            ActiveDocument.SyncToModel();
            await _fileService.SaveFileAsync(ActiveDocument.Model, targetPath);
            var fileInfo = new FileInfo(targetPath);
            ActiveDocument.MarkSaved(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            _fileWatcherService.WatchFile(fileInfo.FullName);

            await _recentFilesService.AddRecentFileAsync(targetPath);
            if (RequestReloadRecentFiles != null)
            {
                await RequestReloadRecentFiles();
            }
            DocumentStateChanged?.Invoke();
            if (RequestSaveSession != null)
            {
                _ = RequestSaveSession();
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error Saving File", $"Could not save file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CloseTabAsync(DocumentViewModel? doc)
    {
        var targetDoc = doc ?? ActiveDocument;
        if (targetDoc == null) return;

        if (targetDoc.IsModified)
        {
            var confirm = await _dialogService.ShowSaveConfirmationAsync(targetDoc.Title);
            if (confirm == ConfirmResult.Cancel)
            {
                return;
            }
            if (confirm == ConfirmResult.Save)
            {
                targetDoc.SyncToModel();
                if (targetDoc.Model.IsNewFile)
                {
                    var targetPath = await _dialogService.ShowSaveFileDialogAsync(targetDoc.Title);
                    if (string.IsNullOrEmpty(targetPath)) return;
                    _fileWatcherService.TemporarilyIgnore(targetPath);
                    await _fileService.SaveFileAsync(targetDoc.Model, targetPath);
                }
                else
                {
                    if (targetDoc.FilePath != null)
                    {
                        _fileWatcherService.TemporarilyIgnore(targetDoc.FilePath);
                    }
                    await _fileService.SaveFileAsync(targetDoc.Model);
                }
            }
        }

        if (!string.IsNullOrEmpty(targetDoc.FilePath) || !string.IsNullOrEmpty(targetDoc.TextDocument?.Text))
        {
            if (_recentlyClosedTabs.Count >= 20)
            {
                var list = _recentlyClosedTabs.ToList();
                list.RemoveAt(list.Count - 1);
                _recentlyClosedTabs.Clear();
                for (int i = list.Count - 1; i >= 0; i--) _recentlyClosedTabs.Push(list[i]);
            }
            string text = string.Empty;
            try
            {
                text = targetDoc.TextDocument?.Text ?? string.Empty;
            }
            catch
            {
                // Fallback for background thread/test executions where UI dispatcher isn't current
            }
            _recentlyClosedTabs.Push(new ClosedTabState(targetDoc.FilePath, targetDoc.Title, text, targetDoc.CaretLine, targetDoc.CaretColumn, targetDoc.Language));
            OnPropertyChanged(nameof(CanReopenClosedTab));
        }

        if (!string.IsNullOrEmpty(targetDoc.FilePath))
        {
            _fileWatcherService.UnwatchFile(targetDoc.FilePath);
        }

        var index = Documents.IndexOf(targetDoc);
        Documents.Remove(targetDoc);

        if (Documents.Count == 0)
        {
            ActiveDocument = null;
        }
        else
        {
            var newIndex = Math.Clamp(index, 0, Documents.Count - 1);
            ActiveDocument = Documents[newIndex];
        }

        UpdateActiveDocumentSelection();
        DocumentStateChanged?.Invoke();
        if (RequestSaveSession != null)
        {
            _ = RequestSaveSession();
        }
    }

    [RelayCommand]
    public async Task ReopenClosedTabAsync()
    {
        if (_recentlyClosedTabs.Count == 0) return;

        var closed = _recentlyClosedTabs.Pop();
        OnPropertyChanged(nameof(CanReopenClosedTab));
        if (!string.IsNullOrEmpty(closed.FilePath) && File.Exists(closed.FilePath))
        {
            await OpenFileInternalAsync(closed.FilePath, closed.CaretLine, closed.CaretColumn);
        }
        else if (!string.IsNullOrEmpty(closed.Content))
        {
            CreateNewDocument();
            if (ActiveDocument != null)
            {
                ActiveDocument.TextDocument.Text = closed.Content;
                ActiveDocument.Title = closed.Title;
                ActiveDocument.Language = closed.Language;
                ActiveDocument.CaretLine = closed.CaretLine;
                ActiveDocument.CaretColumn = closed.CaretColumn;
            }
        }
    }

    [RelayCommand]
    public async Task CloseOtherTabsAsync(DocumentViewModel? doc)
    {
        var keepDoc = doc ?? ActiveDocument;
        if (keepDoc == null) return;

        var toClose = Documents.Where(d => d != keepDoc).ToList();
        foreach (var d in toClose)
        {
            await CloseTabAsync(d);
        }
    }

    [RelayCommand]
    public async Task CloseTabsToTheRightAsync(DocumentViewModel? doc)
    {
        var target = doc ?? ActiveDocument;
        if (target == null) return;

        var index = Documents.IndexOf(target);
        if (index < 0 || index >= Documents.Count - 1) return;

        var toClose = Documents.Skip(index + 1).ToList();
        foreach (var d in toClose)
        {
            await CloseTabAsync(d);
        }
    }

    [RelayCommand]
    public async Task CloseSavedTabsAsync()
    {
        var toClose = Documents.Where(d => !d.IsModified).ToList();
        foreach (var d in toClose)
        {
            await CloseTabAsync(d);
        }
    }

    [RelayCommand]
    public async Task CopyDocumentPathAsync(DocumentViewModel? doc)
    {
        var target = doc ?? ActiveDocument;
        if (target?.FilePath != null && RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(target.FilePath);
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage("✓ Copied path to clipboard", 2500);
            }
        }
    }

    [RelayCommand]
    public async Task CopyDocumentRelativePathAsync(DocumentViewModel? doc)
    {
        var target = doc ?? ActiveDocument;
        if (target?.FilePath != null && RequestSetClipboardText != null)
        {
            var path = target.FilePath;
            var workspaceRoot = GetWorkspaceRootPath?.Invoke();
            if (workspaceRoot != null && path.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetRelativePath(workspaceRoot, path);
            }
            await RequestSetClipboardText(path);
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage("✓ Copied relative path to clipboard", 2500);
            }
        }
    }

    [RelayCommand]
    public void RevealInExplorer(DocumentViewModel? doc)
    {
        var target = doc ?? ActiveDocument;
        if (!string.IsNullOrEmpty(target?.FilePath) && File.Exists(target.FilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{target.FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _ = _dialogService.ShowMessageAsync("Error", $"Could not open Explorer: {ex.Message}");
            }
        }
    }

    public void OnFileChangedOnDisk(object? sender, string changedPath)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var doc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, changedPath, StringComparison.OrdinalIgnoreCase));
            if (doc == null) return;

            lock (_activeReloadPrompts)
            {
                if (_activeReloadPrompts.Contains(changedPath)) return;
                _activeReloadPrompts.Add(changedPath);
            }

            try
            {
                if (!File.Exists(changedPath))
                {
                    var keepOpen = await _dialogService.ShowConfirmationAsync(
                        "File Deleted Externally",
                        $"'{doc.DisplayName}' has been deleted on disk.\n\nDo you want to keep the document open in Code Viewer or close this tab?",
                        confirmText: "Keep Open",
                        cancelText: "Close Tab");
                    if (!keepOpen)
                    {
                        await CloseTabAsync(doc);
                    }
                    else
                    {
                        doc.IsModified = true;
                        DocumentStateChanged?.Invoke();
                        if (RequestStatusMessage != null)
                        {
                            _ = RequestStatusMessage($"File deleted on disk: {doc.DisplayName}", 2500);
                        }
                    }
                    return;
                }

                bool shouldReload;
                if (doc.IsModified)
                {
                    shouldReload = await _dialogService.ShowConfirmationAsync(
                        "File Changed Externally",
                        $"'{doc.DisplayName}' has been modified outside of Code Viewer, but you have unsaved local changes.\n\nDo you want to reload it from disk and overwrite your local changes?",
                        confirmText: "Reload",
                        cancelText: "Keep Local");
                }
                else
                {
                    shouldReload = await _dialogService.ShowConfirmationAsync(
                        "File Changed Externally",
                        $"'{doc.DisplayName}' has been modified outside of Code Viewer.\n\nDo you want to reload it from disk?",
                        confirmText: "Reload",
                        cancelText: "Ignore");
                }

                if (shouldReload && File.Exists(changedPath))
                {
                    var newContent = await ReadAllTextWithRetryAsync(changedPath, doc.Model.EncodingInfo.Encoding);
                    var fileInfo = new FileInfo(changedPath);
                    doc.TextDocument.Text = newContent;
                    doc.MarkSaved(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
                    DocumentStateChanged?.Invoke();
                    if (RequestStatusMessage != null)
                    {
                        _ = RequestStatusMessage($"Reloaded {doc.DisplayName} from disk", 2500);
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Reload Error", $"Could not reload file: {ex.Message}");
            }
            finally
            {
                lock (_activeReloadPrompts)
                {
                    _activeReloadPrompts.Remove(changedPath);
                }
            }
        });
    }

    private static async Task<string> ReadAllTextWithRetryAsync(string path, System.Text.Encoding encoding, int maxRetries = 5, int delayMs = 120)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(stream, encoding);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs);
            }
        }

        using var finalStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var finalReader = new StreamReader(finalStream, encoding);
        return await finalReader.ReadToEndAsync();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _fileWatcherService.FileChangedOnDisk -= OnFileChangedOnDisk;
    }
}
