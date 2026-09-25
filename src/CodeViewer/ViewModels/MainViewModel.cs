using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Configuration;
using CodeViewer.Models;
using CodeViewer.Plugins;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

/// <summary>
/// Root ViewModel orchestrating tabs, file operations, editor settings, themes, plugins, and recent files.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IFileService _fileService;
    private readonly ILanguageService _languageService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IDialogService _dialogService;
    private readonly IThemeService _themeService;
    private readonly IPluginService _pluginService;
    private readonly ISettingsService _settingsService;
    private readonly ISessionService _sessionService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly AppConfig _config;

    private readonly HashSet<string> _activeReloadPrompts = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _statusCts;
    private bool _isDisposed;

    [ObservableProperty]
    private DocumentViewModel? _activeDocument;

    [ObservableProperty]
    private SearchViewModel _searchViewModel = new();

    [ObservableProperty]
    private ObservableCollection<DocumentViewModel> _documents = [];

    [ObservableProperty]
    private ObservableCollection<RecentFileItem> _recentFiles = [];

    [ObservableProperty]
    private string _currentFontFamily = "Cascadia Code";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isQuickOpenOpen;

    [ObservableProperty]
    private string _quickOpenQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<QuickOpenItem> _filteredQuickOpenItems = [];

    [ObservableProperty]
    private QuickOpenItem? _selectedQuickOpenItem;

    private List<QuickOpenItem> _allQuickOpenItems = [];

    [ObservableProperty]
    private bool _isGoToLineOpen;

    [ObservableProperty]
    private string _goToLineInput = string.Empty;

    [ObservableProperty]
    private string _goToLineWatermark = "Go to line (e.g. 42 or 42:10)...";

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public ColorTheme CurrentTheme => _themeService.CurrentTheme;
    public IThemeService ThemeService => _themeService;
    public ILanguageService LanguageService => _languageService;
    public IPluginService PluginService => _pluginService;
    public ISettingsService SettingsService => _settingsService;
    public ISessionService SessionService => _sessionService;
    public IFileWatcherService FileWatcherService => _fileWatcherService;
    public IReadOnlyList<string> AvailableLanguages => _languageService.GetSupportedLanguages();

    public void SelectThemeById(string themeId)
    {
        var target = AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            SelectTheme(target);
        }
    }

    public IReadOnlyList<IPlugin> Plugins => _pluginService.Plugins;
    public IEnumerable<string> PluginCategories => _pluginService.Plugins.Select(p => p.Category).Distinct().OrderBy(c => c);

    // Callbacks provided by MainWindow for active text editor interaction
    public Func<string>? RequestSelectedText { get; set; }
    public Action<string>? RequestReplaceSelection { get; set; }
    public Action<string>? RequestReplaceAll { get; set; }
    public Action<string>? RequestInsertText { get; set; }
    public Func<string, Task>? RequestSetClipboardText { get; set; }
    public Action? RequestOpenPluginManager { get; set; }
    public Action? RequestOpenSettings { get; set; }
    public Action<int, int>? RequestGoToLine { get; set; }
    public Action? RequestToggleComment { get; set; }

    public string WindowTitle => ActiveDocument != null
        ? $"{ActiveDocument.DisplayName} - Code Viewer"
        : "Code Viewer";

    public MainViewModel(
        IFileService fileService,
        ILanguageService languageService,
        IRecentFilesService recentFilesService,
        IDialogService dialogService,
        AppConfig config,
        IThemeService? themeService = null,
        IPluginService? pluginService = null,
        ISettingsService? settingsService = null,
        ISessionService? sessionService = null,
        IFileWatcherService? fileWatcherService = null)
    {
        _fileService = fileService;
        _languageService = languageService;
        _recentFilesService = recentFilesService;
        _dialogService = dialogService;
        _config = config;
        _themeService = themeService ?? new ThemeService();
        _pluginService = pluginService ?? new PluginService();
        _settingsService = settingsService ?? new SettingsService();
        _sessionService = sessionService ?? new SessionService();
        _fileWatcherService = fileWatcherService ?? new FileWatcherService();

        _fileWatcherService.FileChangedOnDisk += OnFileChangedOnDisk;

        var settings = _settingsService.CurrentSettings;
        _currentFontFamily = settings.FontFamily;

        _themeService.ThemeChanged += theme =>
        {
            OnPropertyChanged(nameof(CurrentTheme));
            OnPropertyChanged(nameof(AvailableThemes));
            ApplyCodeColorsToAllDocuments(theme);
        };

        _settingsService.SettingsChanged += OnSettingsChanged;

        _pluginService.PluginsChanged += () =>
        {
            OnPropertyChanged(nameof(Plugins));
            OnPropertyChanged(nameof(PluginCategories));
        };
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        CurrentFontFamily = settings.FontFamily;
        foreach (var doc in Documents)
        {
            doc.FontSize = settings.FontSize;
            doc.WordWrap = settings.WordWrap;
            doc.ShowLineNumbers = settings.ShowLineNumbers;
        }
        ApplyCodeColorsToAllDocuments(CurrentTheme);
    }

    public void ApplyCodeColorsToAllDocuments(ColorTheme? theme = null)
    {
        var target = theme ?? CurrentTheme;
        foreach (var doc in Documents)
        {
            if (doc.HighlightingDefinition != null)
            {
                _themeService.ApplyCodeColorsToHighlighting(doc.HighlightingDefinition, target);
            }
        }
    }

    /// <summary>
    /// Initializes recent files, theme, and opens any command-line file arguments directly.
    /// </summary>
    public async Task InitializeAsync(string[]? commandLineArgs = null)
    {
        // 1. Load settings and apply configured theme & font
        await _settingsService.LoadAsync();
        var settings = _settingsService.CurrentSettings;
        CurrentFontFamily = settings.FontFamily;
        _config.DefaultFontSize = settings.FontSize;
        _config.WordWrap = settings.WordWrap;
        _config.ShowLineNumbers = settings.ShowLineNumbers;
        _themeService.ApplyTheme(settings.ThemeId);

        // 2. Load Recent Files
        await LoadRecentFilesAsync();

        // 3. Handle command-line file arguments if provided (e.g. CodeViewer.exe myfile.cs or myfile.cs:42)
        var openedAny = false;
        if (commandLineArgs != null && commandLineArgs.Length > 0)
        {
            var parsedArgs = CommandLineParser.ParseArguments(commandLineArgs);
            foreach (var arg in parsedArgs)
            {
                if (File.Exists(arg.FilePath))
                {
                    await OpenFileInternalAsync(arg.FilePath, arg.Line, arg.Column);
                    openedAny = true;
                }
            }
        }

        // 4. If no command-line files provided, restore previous session if enabled
        if (!openedAny && settings.RestorePreviousSession)
        {
            var session = await _sessionService.LoadSessionAsync();
            if (session != null && session.OpenFiles.Count > 0)
            {
                foreach (var file in session.OpenFiles)
                {
                    if (File.Exists(file))
                    {
                        await OpenFileInternalAsync(file);
                        openedAny = true;
                    }
                }

                if (!string.IsNullOrEmpty(session.ActiveFile))
                {
                    var target = Documents.FirstOrDefault(d => string.Equals(d.FilePath, session.ActiveFile, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                    {
                        ActiveDocument = target;
                        UpdateActiveDocumentSelection();
                    }
                }
            }
        }

        // 5. If no file opened, open a default empty document
        if (Documents.Count == 0)
        {
            CreateNewDocument();
        }
    }

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public async Task LoadRecentFilesAsync()
    {
        var items = await _recentFilesService.GetRecentFilesAsync();
        RecentFiles.Clear();
        foreach (var item in items)
        {
            item.OpenCommand = OpenRecentFileCommand;
            RecentFiles.Add(item);
        }
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    [RelayCommand]
    public async Task ClearRecentFilesAsync()
    {
        await _recentFilesService.ClearAsync();
        RecentFiles.Clear();
        OnPropertyChanged(nameof(HasRecentFiles));
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
            ShowLineNumbers = settings.ShowLineNumbers
        };

        if (docVm.HighlightingDefinition != null)
        {
            _themeService.ApplyCodeColorsToHighlighting(docVm.HighlightingDefinition, CurrentTheme);
        }

        Documents.Add(docVm);
        ActiveDocument = docVm;
        UpdateActiveDocumentSelection();
        OnPropertyChanged(nameof(WindowTitle));
        _ = SaveCurrentSessionAsync();
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

    public async Task ShowTemporaryStatusAsync(string message, int durationMs = 2500)
    {
        _statusCts?.Cancel();
        _statusCts = new CancellationTokenSource();
        var token = _statusCts.Token;

        StatusMessage = message;
        try
        {
            await Task.Delay(durationMs, token);
            if (!token.IsCancellationRequested)
            {
                StatusMessage = null;
            }
        }
        catch (TaskCanceledException)
        {
            // Ignored
        }
    }

    [RelayCommand]
    public async Task OpenRecentFileAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (!File.Exists(filePath))
        {
            await _recentFilesService.RemoveRecentFileAsync(filePath);
            await LoadRecentFilesAsync();
            _ = ShowTemporaryStatusAsync($"File not found: {Path.GetFileName(filePath)}");
            await _dialogService.ShowMessageAsync("File Not Found", $"The recent file '{filePath}' no longer exists on disk.\n\nIt has been removed from recent files.");
            return;
        }

        await OpenFileInternalAsync(filePath);
    }

    public async Task OpenFileInternalAsync(string filePath, int targetLine = 1, int targetCol = 1)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            await _recentFilesService.RemoveRecentFileAsync(filePath);
            await LoadRecentFilesAsync();
            _ = ShowTemporaryStatusAsync($"File not found: {Path.GetFileName(filePath)}");
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
                ShowLineNumbers = settings.ShowLineNumbers
            };

            if (docVm.HighlightingDefinition != null)
            {
                _themeService.ApplyCodeColorsToHighlighting(docVm.HighlightingDefinition, CurrentTheme);
            }

            // If the only document is an untouched empty Untitled tab, replace it
            if (Documents.Count == 1 && Documents[0].Model.IsNewFile && !Documents[0].IsModified && Documents[0].TextDocument.TextLength == 0)
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
            await LoadRecentFilesAsync();
            OnPropertyChanged(nameof(WindowTitle));
            _ = SaveCurrentSessionAsync();

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
            OnPropertyChanged(nameof(WindowTitle));
            _ = SaveCurrentSessionAsync();
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
            await LoadRecentFilesAsync();
            OnPropertyChanged(nameof(WindowTitle));
            _ = SaveCurrentSessionAsync();
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

        if (!string.IsNullOrEmpty(targetDoc.FilePath))
        {
            _fileWatcherService.UnwatchFile(targetDoc.FilePath);
        }

        var index = Documents.IndexOf(targetDoc);
        Documents.Remove(targetDoc);

        if (Documents.Count == 0)
        {
            CreateNewDocument();
        }
        else
        {
            var newIndex = Math.Clamp(index, 0, Documents.Count - 1);
            ActiveDocument = Documents[newIndex];
        }

        UpdateActiveDocumentSelection();
        OnPropertyChanged(nameof(WindowTitle));
        _ = SaveCurrentSessionAsync();
    }

    #region Tab Context Menu Operations

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
            _ = ShowTemporaryStatusAsync("✓ Copied path to clipboard");
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

    #endregion

    #region Quick Open Operations

    [RelayCommand]
    public async Task ShowQuickOpenAsync()
    {
        _allQuickOpenItems.Clear();

        // 1. Add all open tabs
        foreach (var doc in Documents)
        {
            var item = new QuickOpenItem
            {
                FilePath = doc.FilePath ?? doc.Title,
                DisplayName = doc.DisplayName,
                RelativeOrFullPath = doc.FilePath ?? "Unsaved Document",
                IsOpenTab = true
            };
            _allQuickOpenItems.Add(item);
        }

        // 2. Add recent files that aren't already open
        var recent = await _recentFilesService.GetRecentFilesAsync();
        foreach (var rf in recent)
        {
            if (_allQuickOpenItems.Any(i => string.Equals(i.FilePath, rf.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _allQuickOpenItems.Add(new QuickOpenItem
            {
                FilePath = rf.FilePath,
                DisplayName = rf.FileName,
                RelativeOrFullPath = rf.FilePath,
                IsOpenTab = false
            });
        }

        // 3. If active doc is a real file, add top-level sibling files in its directory (up to 40)
        try
        {
            if (!string.IsNullOrEmpty(ActiveDocument?.FilePath))
            {
                var dir = Path.GetDirectoryName(ActiveDocument.FilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    foreach (var file in Directory.EnumerateFiles(dir).Take(40))
                    {
                        if (_allQuickOpenItems.Any(i => string.Equals(i.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        _allQuickOpenItems.Add(new QuickOpenItem
                        {
                            FilePath = file,
                            DisplayName = Path.GetFileName(file),
                            RelativeOrFullPath = Path.GetRelativePath(dir, file),
                            IsOpenTab = false
                        });
                    }
                }
            }
        }
        catch
        {
            // Ignore filesystem enumeration errors
        }

        QuickOpenQuery = string.Empty;
        FilterQuickOpenItems(string.Empty);
        IsQuickOpenOpen = true;
    }

    [RelayCommand]
    public void CloseQuickOpen()
    {
        IsQuickOpenOpen = false;
        QuickOpenQuery = string.Empty;
    }

    [RelayCommand]
    public async Task SelectQuickOpenItemAsync(QuickOpenItem? item)
    {
        var target = item ?? SelectedQuickOpenItem;
        CloseQuickOpen();
        if (target == null) return;

        if (target.IsOpenTab)
        {
            var doc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, target.FilePath, StringComparison.OrdinalIgnoreCase) ||
                                                    string.Equals(d.Title, target.FilePath, StringComparison.OrdinalIgnoreCase));
            if (doc != null)
            {
                ActiveDocument = doc;
                UpdateActiveDocumentSelection();
                return;
            }
        }

        if (File.Exists(target.FilePath))
        {
            await OpenFileInternalAsync(target.FilePath);
        }
    }

    partial void OnQuickOpenQueryChanged(string value)
    {
        FilterQuickOpenItems(value);
    }

    private void FilterQuickOpenItems(string query)
    {
        FilteredQuickOpenItems.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var item in _allQuickOpenItems)
            {
                FilteredQuickOpenItems.Add(item);
            }
        }
        else
        {
            var q = query.Trim();
            var matches = _allQuickOpenItems
                .Where(i => i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            i.RelativeOrFullPath.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.DisplayName.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.IsOpenTab);

            foreach (var item in matches)
            {
                FilteredQuickOpenItems.Add(item);
            }
        }

        SelectedQuickOpenItem = FilteredQuickOpenItems.FirstOrDefault();
    }

    #endregion

    #region Go to Line Operations

    [RelayCommand]
    public void ShowGoToLine()
    {
        GoToLineWatermark = ActiveDocument != null
            ? $"Go to line (1 - {ActiveDocument.TextDocument.LineCount})..."
            : "Go to line (e.g. 42 or 42:10)...";
        GoToLineInput = string.Empty;
        IsGoToLineOpen = true;
    }

    [RelayCommand]
    public void CloseGoToLine()
    {
        IsGoToLineOpen = false;
        GoToLineInput = string.Empty;
    }

    [RelayCommand]
    public void ExecuteGoToLine()
    {
        if (string.IsNullOrWhiteSpace(GoToLineInput))
        {
            CloseGoToLine();
            return;
        }

        var parts = GoToLineInput.Trim().Split(new[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && int.TryParse(parts[0], out var line) && line > 0)
        {
            var col = 1;
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedCol) && parsedCol > 0)
            {
                col = parsedCol;
            }

            RequestGoToLine?.Invoke(line, col);
        }

        CloseGoToLine();
    }

    #endregion

    [RelayCommand]
    public void ShowSearch()
    {
        var selection = RequestSelectedText?.Invoke();
        SearchViewModel.Open(string.IsNullOrWhiteSpace(selection) ? null : selection, showReplace: false);
    }

    [RelayCommand]
    public void ShowReplace()
    {
        var selection = RequestSelectedText?.Invoke();
        SearchViewModel.Open(string.IsNullOrWhiteSpace(selection) ? null : selection, showReplace: true);
    }

    [RelayCommand]
    public void ToggleComment()
    {
        RequestToggleComment?.Invoke();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        if (ActiveDocument != null && ActiveDocument.FontSize < _config.MaxFontSize)
        {
            ActiveDocument.FontSize = Math.Min(_config.MaxFontSize, ActiveDocument.FontSize + 1.5);
            var settings = _settingsService.CurrentSettings;
            settings.FontSize = ActiveDocument.FontSize;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public void ZoomOut()
    {
        if (ActiveDocument != null && ActiveDocument.FontSize > _config.MinFontSize)
        {
            ActiveDocument.FontSize = Math.Max(_config.MinFontSize, ActiveDocument.FontSize - 1.5);
            var settings = _settingsService.CurrentSettings;
            settings.FontSize = ActiveDocument.FontSize;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public void ResetZoom()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.FontSize = _config.DefaultFontSize;
            var settings = _settingsService.CurrentSettings;
            settings.FontSize = ActiveDocument.FontSize;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public void ToggleWordWrap()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.WordWrap = !ActiveDocument.WordWrap;
            var settings = _settingsService.CurrentSettings;
            settings.WordWrap = ActiveDocument.WordWrap;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public void ToggleLineNumbers()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.ShowLineNumbers = !ActiveDocument.ShowLineNumbers;
            var settings = _settingsService.CurrentSettings;
            settings.ShowLineNumbers = ActiveDocument.ShowLineNumbers;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public void ShowSettings()
    {
        RequestOpenSettings?.Invoke();
    }

    #region Theme Operations

    [RelayCommand]
    public void SelectTheme(ColorTheme? theme)
    {
        if (theme != null)
        {
            _themeService.ApplyTheme(theme);
            OnPropertyChanged(nameof(CurrentTheme));
            var settings = _settingsService.CurrentSettings;
            settings.ThemeId = theme.Id;
            _ = _settingsService.SaveAsync(settings);
        }
    }

    [RelayCommand]
    public async Task ImportThemeAsync()
    {
        var file = await _dialogService.ShowOpenSpecificFileDialogAsync("Import Color Theme", "JSON Theme (*.json)", new[] { "*.json" });
        if (!string.IsNullOrEmpty(file))
        {
            try
            {
                var theme = await _themeService.ImportThemeFromJsonAsync(file);
                OnPropertyChanged(nameof(AvailableThemes));
                OnPropertyChanged(nameof(CurrentTheme));
                await _dialogService.ShowMessageAsync("Theme Imported", $"Theme '{theme.Name}' was successfully imported and activated!");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Import Theme Error", $"Could not import theme: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task ImportSyntaxAsync()
    {
        var file = await _dialogService.ShowOpenSpecificFileDialogAsync("Import Syntax Highlighting Definition", "XSHD Syntax (*.xshd)", new[] { "*.xshd" });
        if (!string.IsNullOrEmpty(file))
        {
            try
            {
                var langName = await _themeService.ImportSyntaxDefinitionAsync(file);
                if (ActiveDocument != null)
                {
                    ActiveDocument.UpdateLanguage();
                }
                await _dialogService.ShowMessageAsync("Syntax Imported", $"Syntax definition '{langName}' was successfully imported and registered!");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Import Syntax Error", $"Could not import syntax: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public void OpenThemesFolder()
    {
        try
        {
            var dir = _themeService.GetThemesDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _ = _dialogService.ShowMessageAsync("Error Opening Folder", ex.Message);
        }
    }

    #endregion

    #region Plugin Operations

    [RelayCommand]
    public async Task ExecutePluginAsync(IPlugin? plugin)
    {
        if (plugin == null || ActiveDocument == null) return;

        var selectedText = RequestSelectedText?.Invoke() ?? string.Empty;
        var replaceSel = RequestReplaceSelection ?? (newText => { });
        var replaceAll = RequestReplaceAll ?? (newText => ActiveDocument.TextDocument.Text = newText);
        var insertTxt = RequestInsertText ?? (txt => ActiveDocument.TextDocument.Insert(0, txt));

        try
        {
            await _pluginService.ExecutePluginAsync(
                plugin,
                ActiveDocument,
                selectedText,
                replaceSel,
                replaceAll,
                insertTxt,
                _dialogService);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Plugin Execution Error", $"Plugin '{plugin.Name}' threw an error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ImportPluginAsync()
    {
        var file = await _dialogService.ShowOpenSpecificFileDialogAsync("Import Plugin Assembly", ".NET Assembly (*.dll)", new[] { "*.dll" });
        if (!string.IsNullOrEmpty(file))
        {
            try
            {
                var loaded = await _pluginService.ImportPluginAsync(file);
                OnPropertyChanged(nameof(Plugins));
                OnPropertyChanged(nameof(PluginCategories));
                var names = string.Join(", ", loaded.Select(p => p.Name));
                await _dialogService.ShowMessageAsync("Plugin Imported", $"Successfully loaded {loaded.Count} plugin(s): {names}");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Import Plugin Error", $"Could not import plugin: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public void OpenPluginsFolder()
    {
        try
        {
            var dir = _pluginService.GetPluginsDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _ = _dialogService.ShowMessageAsync("Error Opening Folder", ex.Message);
        }
    }

    [RelayCommand]
    public void ShowPluginManager()
    {
        RequestOpenPluginManager?.Invoke();
    }

    #endregion

    [RelayCommand]
    public void SetLanguage(string? language)
    {
        if (ActiveDocument == null || string.IsNullOrWhiteSpace(language)) return;

        ActiveDocument.Language = language;
        var def = _languageService.GetHighlightingDefinition(language);
        if (def != null && CurrentTheme != null)
        {
            _themeService.ApplyCodeColorsToHighlighting(def, CurrentTheme);
        }
        ActiveDocument.HighlightingDefinition = def;
    }

    [RelayCommand]
    public async Task CopyAllAsync()
    {
        if (ActiveDocument == null) return;
        var text = ActiveDocument.TextDocument.Text;
        if (string.IsNullOrEmpty(text)) return;

        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText.Invoke(text);
            _ = ShowTemporaryStatusAsync("✓ Copied all content to clipboard!");
        }
    }

    public async Task SaveCurrentSessionAsync()
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.RestorePreviousSession)
        {
            await _sessionService.ClearSessionAsync();
            return;
        }

        var openPaths = Documents
            .Select(d => d.FilePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Cast<string>();

        await _sessionService.SaveSessionAsync(openPaths, ActiveDocument?.FilePath);
    }

    private void OnFileChangedOnDisk(object? sender, string changedPath)
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
                        OnPropertyChanged(nameof(WindowTitle));
                        _ = ShowTemporaryStatusAsync($"File deleted on disk: {doc.DisplayName}");
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
                    OnPropertyChanged(nameof(WindowTitle));
                    _ = ShowTemporaryStatusAsync($"Reloaded {doc.DisplayName} from disk");
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

    private void UpdateActiveDocumentSelection()
    {
        foreach (var doc in Documents)
        {
            doc.IsActive = (doc == ActiveDocument);
        }
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        UpdateActiveDocumentSelection();
        OnPropertyChanged(nameof(WindowTitle));
        _ = SaveCurrentSessionAsync();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _fileWatcherService.FileChangedOnDisk -= OnFileChangedOnDisk;
        _fileWatcherService.Dispose();
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
