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
    private readonly IIntegrationsUpdateService _integrationsUpdateService;
    private readonly IWorkspaceSearchService _workspaceSearchService;
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

    // 📁 Folder Workspace & Tree Sidebar
    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private FolderItem? _rootFolder;

    [ObservableProperty]
    private FolderItem? _selectedFolderItem;

    // 🔍 Global Workspace Search (Ctrl+Shift+F)
    [ObservableProperty]
    private bool _isGlobalSearchOpen;

    [ObservableProperty]
    private string _globalSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _globalSearchMatchCase;

    [ObservableProperty]
    private bool _globalSearchWholeWord;

    [ObservableProperty]
    private string _globalSearchStatus = string.Empty;

    [ObservableProperty]
    private bool _isGlobalSearching;

    [ObservableProperty]
    private WorkspaceSearchMatch? _selectedGlobalSearchResult;

    public ObservableCollection<WorkspaceSearchMatch> GlobalSearchResults { get; } = new();

    private CancellationTokenSource? _globalSearchCts;

    // ⚡ Command Palette
    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _commandPaletteQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CommandPaletteItem> _filteredCommands = [];

    [ObservableProperty]
    private CommandPaletteItem? _selectedCommandPaletteItem;

    private readonly List<CommandPaletteItem> _allCommands = [];

    // 🔀 Split View
    [ObservableProperty]
    private bool _isSplitViewActive;

    [ObservableProperty]
    private DocumentViewModel? _secondaryDocument;

    // 📝 Markdown Live Preview
    [ObservableProperty]
    private bool _isMarkdownPreviewActive;

    // ▶ Runner & 🔍 Diff ViewModels
    [ObservableProperty]
    private RunnerViewModel _runnerViewModel = new();

    [ObservableProperty]
    private DiffViewModel _diffViewModel = new();

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public ColorTheme CurrentTheme => _themeService.CurrentTheme;
    public IThemeService ThemeService => _themeService;
    public ILanguageService LanguageService => _languageService;
    public IPluginService PluginService => _pluginService;
    public ISettingsService SettingsService => _settingsService;
    public ISessionService SessionService => _sessionService;
    public IFileWatcherService FileWatcherService => _fileWatcherService;
    public IReadOnlyList<string> AvailableLanguages => _languageService.GetSupportedLanguages();

    [ObservableProperty]
    private string _languageFilter = string.Empty;

    public IEnumerable<LanguageOption> FilteredLanguageOptions
    {
        get
        {
            var activeLang = ActiveDocument?.Language;
            var list = _languageService.GetSupportedLanguages();
            if (!string.IsNullOrWhiteSpace(LanguageFilter))
            {
                list = list.Where(l => l.Contains(LanguageFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return list.Select(name => new LanguageOption
            {
                Name = name,
                IsSelected = string.Equals(name, activeLang, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    partial void OnLanguageFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredLanguageOptions));
    }

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
        IFileWatcherService? fileWatcherService = null,
        IIntegrationsUpdateService? integrationsUpdateService = null,
        IWorkspaceSearchService? workspaceSearchService = null)
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
        _integrationsUpdateService = integrationsUpdateService ?? new IntegrationsUpdateService();
        _workspaceSearchService = workspaceSearchService ?? new WorkspaceSearchService();


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

        InitializeCommands();
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
        _themeService.ApplyTheme(settings);
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
        _themeService.ApplyTheme(settings);

        // 2. Load Recent Files
        await LoadRecentFilesAsync();

        // 3. Handle command-line file or folder arguments if provided
        var openedAny = false;
        if (commandLineArgs != null && commandLineArgs.Length > 0)
        {
            var parsedArgs = CommandLineParser.ParseArguments(commandLineArgs);
            foreach (var arg in parsedArgs)
            {
                if (Directory.Exists(arg.FilePath))
                {
                    OpenFolder(arg.FilePath);
                    openedAny = true;
                }
                else if (File.Exists(arg.FilePath))
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
            var pct = (int)Math.Round((ActiveDocument.FontSize / _config.DefaultFontSize) * 100);
            _ = ShowTemporaryStatusAsync($"Zoom: {pct}% ({ActiveDocument.FontSize:0.#} pt)");
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
            var pct = (int)Math.Round((ActiveDocument.FontSize / _config.DefaultFontSize) * 100);
            _ = ShowTemporaryStatusAsync($"Zoom: {pct}% ({ActiveDocument.FontSize:0.#} pt)");
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
            _ = ShowTemporaryStatusAsync($"Zoom: 100% ({_config.DefaultFontSize:0.#} pt)");
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
        OnPropertyChanged(nameof(FilteredLanguageOptions));
    }

    public void ApplyFirstFilteredLanguage()
    {
        var first = FilteredLanguageOptions.FirstOrDefault();
        if (first != null)
        {
            SetLanguage(first.Name);
        }
    }

    [RelayCommand]
    public async Task CheckIntegrationsUpdateAsync()
    {
        _ = ShowTemporaryStatusAsync("Checking language integration updates on GitHub...");
        var statuses = await _integrationsUpdateService.CheckIntegrationsAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Language & Integration Status:");
        sb.AppendLine();

        bool hasAnyUpdate = false;
        foreach (var status in statuses)
        {
            var icon = status.IsUpToDate ? "✓" : "⚡";
            sb.AppendLine($"{icon} {status.LanguageName}:");
            sb.AppendLine($"   • Status: {status.StatusSummary}");
            sb.AppendLine($"   • Repository: {status.RepositoryUrl}");
            sb.AppendLine();

            if (!status.IsUpToDate)
            {
                hasAnyUpdate = true;
            }
        }

        if (hasAnyUpdate)
        {
            sb.AppendLine("A newer version is available on GitHub.");
            var openRepo = await _dialogService.ShowConfirmationAsync(
                "Language Integrations",
                sb.ToString(),
                "Visit GitHub",
                "Close");

            if (openRepo)
            {
                var updateItem = statuses.FirstOrDefault(s => !s.IsUpToDate);
                if (updateItem != null)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = updateItem.RepositoryUrl,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }
        else
        {
            sb.AppendLine("All integrated languages and syntax definitions are up to date!");
            await _dialogService.ShowMessageAsync("Language Integrations", sb.ToString());
        }

        _ = ShowTemporaryStatusAsync("Language integrations check completed");
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

    public bool IsActiveDocumentMarkdown =>
        ActiveDocument != null &&
        (string.Equals(ActiveDocument.Language, "Markdown", StringComparison.OrdinalIgnoreCase) ||
         (ActiveDocument.FilePath?.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ?? false) ||
         (ActiveDocument.FilePath?.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ?? false));

    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        UpdateActiveDocumentSelection();
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(FilteredLanguageOptions));
        OnPropertyChanged(nameof(IsActiveDocumentMarkdown));
        if (!IsActiveDocumentMarkdown && IsMarkdownPreviewActive)
        {
            IsMarkdownPreviewActive = false;
        }
        _ = SaveCurrentSessionAsync();
    }

    #region Folder Workspace & Tree Sidebar Operations

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        var folderPath = await _dialogService.ShowOpenFolderDialogAsync();
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            OpenFolder(folderPath);
        }
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;
        var fullPath = Path.GetFullPath(folderPath);
        var root = new FolderItem(fullPath, true);
        root.IsExpanded = true;
        RootFolder = root;
        IsSidebarOpen = true;
        _ = ShowTemporaryStatusAsync($"Opened workspace: {root.Name}");
    }

    [RelayCommand]
    public void CloseFolder()
    {
        RootFolder = null;
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    public async Task SelectFolderItemAsync(FolderItem? item)
    {
        if (item == null) return;
        SelectedFolderItem = item;
        if (!item.IsDirectory && !string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
        {
            await OpenFileInternalAsync(item.FullPath);
        }
        else if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    [RelayCommand]
    public async Task CreateFileInFolderAsync(FolderItem? item)
    {
        var targetFolder = item?.IsDirectory == true ? item : (item?.Parent ?? RootFolder);
        if (targetFolder == null || string.IsNullOrEmpty(targetFolder.FullPath) || !Directory.Exists(targetFolder.FullPath))
        {
            await _dialogService.ShowMessageAsync("New File", "Please open a workspace folder first.");
            return;
        }

        var fileName = await _dialogService.ShowPromptAsync("New File", $"Create new file in '{targetFolder.Name}':", "untitled.txt", "e.g. main.dart");
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var fullPath = Path.Combine(targetFolder.FullPath, fileName.Trim());
        try
        {
            if (File.Exists(fullPath))
            {
                await _dialogService.ShowMessageAsync("File Exists", $"A file named '{fileName}' already exists in this folder.");
                return;
            }

            File.WriteAllText(fullPath, string.Empty);
            await targetFolder.RefreshAsync();
            await OpenFileInternalAsync(fullPath);
            _ = ShowTemporaryStatusAsync($"Created {fileName}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error", $"Could not create file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CreateFolderInFolderAsync(FolderItem? item)
    {
        var targetFolder = item?.IsDirectory == true ? item : (item?.Parent ?? RootFolder);
        if (targetFolder == null || string.IsNullOrEmpty(targetFolder.FullPath) || !Directory.Exists(targetFolder.FullPath))
        {
            await _dialogService.ShowMessageAsync("New Folder", "Please open a workspace folder first.");
            return;
        }

        var folderName = await _dialogService.ShowPromptAsync("New Folder", $"Create new folder in '{targetFolder.Name}':", "NewFolder", "e.g. src");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        var fullPath = Path.Combine(targetFolder.FullPath, folderName.Trim());
        try
        {
            if (Directory.Exists(fullPath))
            {
                await _dialogService.ShowMessageAsync("Folder Exists", $"A folder named '{folderName}' already exists.");
                return;
            }

            Directory.CreateDirectory(fullPath);
            await targetFolder.RefreshAsync();
            _ = ShowTemporaryStatusAsync($"Created folder {folderName}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error", $"Could not create folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task RenameFolderItemAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var oldName = target.Name;
        var newName = await _dialogService.ShowPromptAsync("Rename", $"Enter new name for '{oldName}':", oldName);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), oldName, StringComparison.Ordinal)) return;

        newName = newName.Trim();
        var parentDir = Path.GetDirectoryName(target.FullPath);
        if (string.IsNullOrEmpty(parentDir)) return;

        var newFullPath = Path.Combine(parentDir, newName);
        try
        {
            if (target.IsDirectory)
            {
                if (Directory.Exists(newFullPath))
                {
                    await _dialogService.ShowMessageAsync("Rename Error", $"A folder named '{newName}' already exists.");
                    return;
                }
                Directory.Move(target.FullPath, newFullPath);
            }
            else
            {
                if (File.Exists(newFullPath))
                {
                    await _dialogService.ShowMessageAsync("Rename Error", $"A file named '{newName}' already exists.");
                    return;
                }
                File.Move(target.FullPath, newFullPath);

                var openDoc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, target.FullPath, StringComparison.OrdinalIgnoreCase));
                if (openDoc != null)
                {
                    openDoc.FilePath = newFullPath;
                    openDoc.Title = newName;
                }
            }

            if (target.Parent != null)
            {
                await target.Parent.RefreshAsync();
            }
            else if (RootFolder != null)
            {
                await RootFolder.RefreshAsync();
            }

            _ = ShowTemporaryStatusAsync($"Renamed to {newName}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Rename Failed", ex.Message);
        }
    }

    [RelayCommand]
    public async Task DeleteFolderItemAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var itemType = target.IsDirectory ? "folder" : "file";
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirm Delete",
            $"Are you sure you want to delete the {itemType} '{target.Name}'?\nThis action cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            if (target.IsDirectory)
            {
                if (Directory.Exists(target.FullPath))
                {
                    Directory.Delete(target.FullPath, recursive: true);
                }
            }
            else
            {
                if (File.Exists(target.FullPath))
                {
                    File.Delete(target.FullPath);
                }

                var openDoc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, target.FullPath, StringComparison.OrdinalIgnoreCase));
                if (openDoc != null)
                {
                    await CloseTabAsync(openDoc);
                }

            }

            if (target.Parent != null)
            {
                await target.Parent.RefreshAsync();
            }
            else if (RootFolder != null)
            {
                await RootFolder.RefreshAsync();
            }

            _ = ShowTemporaryStatusAsync($"Deleted {target.Name}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Delete Failed", ex.Message);
        }
    }

    [RelayCommand]
    public void RevealFolderItemInExplorer(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        var fullPath = target?.FullPath ?? RootFolder?.FullPath;
        if (string.IsNullOrEmpty(fullPath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                }
                else if (Directory.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"\"{fullPath}\"");
                }
            }
            else
            {
                var dir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : fullPath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
            }
        }
        catch
        {
            // Ignore
        }
    }

    [RelayCommand]
    public async Task CopyItemPathAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        var path = target?.FullPath ?? RootFolder?.FullPath;
        if (string.IsNullOrEmpty(path)) return;

        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(path);
            _ = ShowTemporaryStatusAsync($"✓ Copied path: {path}");
        }
    }

    [RelayCommand]
    public async Task CopyItemRelativePathAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var root = RootFolder?.FullPath ?? Environment.CurrentDirectory;
        var rel = Path.GetRelativePath(root, target.FullPath);

        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(rel);
            _ = ShowTemporaryStatusAsync($"✓ Copied relative path: {rel}");
        }
    }

    [RelayCommand]
    public async Task RefreshFolderItemAsync(FolderItem? item)
    {
        var target = item ?? RootFolder;
        if (target != null)
        {
            await target.RefreshAsync();
            _ = ShowTemporaryStatusAsync("✓ Refreshed workspace");
        }
    }

    #endregion

    #region Global Workspace Search (Ctrl+Shift+F)

    partial void OnGlobalSearchQueryChanged(string value)
    {
        _ = TriggerGlobalSearchDebouncedAsync();
    }

    partial void OnGlobalSearchMatchCaseChanged(bool value)
    {
        _ = TriggerGlobalSearchDebouncedAsync();
    }

    partial void OnGlobalSearchWholeWordChanged(bool value)
    {
        _ = TriggerGlobalSearchDebouncedAsync();
    }

    private async Task TriggerGlobalSearchDebouncedAsync()
    {
        _globalSearchCts?.Cancel();
        _globalSearchCts?.Dispose();
        _globalSearchCts = new CancellationTokenSource();
        var ct = _globalSearchCts.Token;

        var q = GlobalSearchQuery?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            GlobalSearchResults.Clear();
            GlobalSearchStatus = string.Empty;
            IsGlobalSearching = false;
            return;
        }

        try
        {
            await Task.Delay(120, ct);
            await ExecuteGlobalSearchAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    public async Task ExecuteGlobalSearchAsync(CancellationToken ct = default)
    {
        var q = GlobalSearchQuery?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        var rootPath = RootFolder?.FullPath;
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            if (ActiveDocument != null && !string.IsNullOrEmpty(ActiveDocument.FilePath) && File.Exists(ActiveDocument.FilePath))
            {
                rootPath = Path.GetDirectoryName(ActiveDocument.FilePath);
            }
            else
            {
                rootPath = Environment.CurrentDirectory;
            }
        }

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            GlobalSearchStatus = "Open a workspace folder to search across files.";
            return;
        }

        IsGlobalSearching = true;
        GlobalSearchStatus = "Searching files...";

        try
        {
            var result = await _workspaceSearchService.SearchAsync(
                rootPath,
                q,
                GlobalSearchMatchCase,
                GlobalSearchWholeWord,
                ct: ct);

            if (ct.IsCancellationRequested) return;

            GlobalSearchResults.Clear();
            foreach (var match in result.Matches)
            {
                GlobalSearchResults.Add(match);
            }

            if (result.Matches.Count > 0)
            {
                SelectedGlobalSearchResult = result.Matches[0];
                GlobalSearchStatus = $"Found {result.Matches.Count} matches in {result.TotalFilesSearched} files ({result.ElapsedMilliseconds} ms)";
            }
            else
            {
                GlobalSearchStatus = $"No matches found in {result.TotalFilesSearched} files ({result.ElapsedMilliseconds} ms)";
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            GlobalSearchStatus = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsGlobalSearching = false;
        }
    }

    [RelayCommand]
    public void OpenGlobalSearch()
    {
        if (RequestSelectedText != null)
        {
            var sel = RequestSelectedText.Invoke();
            if (!string.IsNullOrWhiteSpace(sel) && !sel.Contains('\n'))
            {
                GlobalSearchQuery = sel.Trim();
            }
        }

        IsGlobalSearchOpen = true;
        _ = TriggerGlobalSearchDebouncedAsync();
    }

    [RelayCommand]
    public void CloseGlobalSearch()
    {
        _globalSearchCts?.Cancel();
        IsGlobalSearchOpen = false;
        GlobalSearchQuery = string.Empty;
        GlobalSearchResults.Clear();
        GlobalSearchStatus = string.Empty;
    }

    [RelayCommand]
    public void ToggleGlobalSearchMatchCase()
    {
        GlobalSearchMatchCase = !GlobalSearchMatchCase;
    }

    [RelayCommand]
    public void ToggleGlobalSearchWholeWord()
    {
        GlobalSearchWholeWord = !GlobalSearchWholeWord;
    }

    [RelayCommand]
    public async Task SelectGlobalSearchResultAsync(WorkspaceSearchMatch? match)
    {
        var target = match ?? SelectedGlobalSearchResult;
        if (target == null) return;

        CloseGlobalSearch();

        if (File.Exists(target.FilePath))
        {
            await OpenFileInternalAsync(target.FilePath);
            RequestGoToLine?.Invoke(target.LineNumber, target.ColumnNumber);
        }
    }

    #endregion


    #region Split View Operations

    [RelayCommand]
    public void ToggleSplitView()
    {
        IsSplitViewActive = !IsSplitViewActive;
        if (IsSplitViewActive)
        {
            if (Documents.Count > 1)
            {
                SecondaryDocument = Documents.FirstOrDefault(d => d != ActiveDocument) ?? ActiveDocument;
            }
            else
            {
                SecondaryDocument = ActiveDocument;
            }
        }
        else
        {
            SecondaryDocument = null;
        }
    }

    [RelayCommand]
    public void CloseSplitView()
    {
        IsSplitViewActive = false;
        SecondaryDocument = null;
    }

    #endregion

    #region Markdown Live Preview Operations

    [RelayCommand]
    public void ToggleMarkdownPreview()
    {
        if (!IsActiveDocumentMarkdown && !IsMarkdownPreviewActive)
        {
            return;
        }
        IsMarkdownPreviewActive = !IsMarkdownPreviewActive;
    }

    #endregion

    #region RTL (Right-to-Left) Operations

    [RelayCommand]
    public void ToggleRtl()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.IsRtl = !ActiveDocument.IsRtl;
        }
    }

    #endregion

    #region Diff Operations

    [RelayCommand]
    public async Task ShowDiffAsync()
    {
        var targetFile = await _dialogService.ShowOpenFileDialogAsync();
        if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile))
        {
            await CompareWithActiveAsync(targetFile);
        }
    }

    public async Task CompareWithActiveAsync(string compareFilePath)
    {
        if (ActiveDocument == null || !File.Exists(compareFilePath)) return;

        var activeText = ActiveDocument.TextDocument.Text;
        var compareText = await File.ReadAllTextAsync(compareFilePath);

        DiffViewModel.LoadDiff(compareText, activeText, Path.GetFileName(compareFilePath), ActiveDocument.DisplayName);
    }

    #endregion

    #region Script Runner Operations

    [RelayCommand]
    public async Task RunActiveScriptAsync()
    {
        if (ActiveDocument == null) return;

        if (ActiveDocument.IsModified || ActiveDocument.Model.IsNewFile)
        {
            await SaveAsync();
        }

        if (!string.IsNullOrEmpty(ActiveDocument.FilePath) && File.Exists(ActiveDocument.FilePath))
        {
            await RunnerViewModel.RunScriptAsync(ActiveDocument.FilePath);
        }
        else
        {
            _ = ShowTemporaryStatusAsync("Please save the file before running.");
        }
    }

    #endregion

    #region Command Palette Operations

    [RelayCommand]
    public void ShowCommandPalette()
    {
        CommandPaletteQuery = string.Empty;
        FilterCommands(string.Empty);
        IsCommandPaletteOpen = true;
    }

    [RelayCommand]
    public void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
    }

    partial void OnCommandPaletteQueryChanged(string value)
    {
        FilterCommands(value);
    }

    public void FilterCommands(string query)
    {
        FilteredCommands.Clear();
        var q = query.Trim();
        var matches = string.IsNullOrEmpty(q)
            ? _allCommands
            : _allCommands.Where(c =>
                c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.ShortcutText.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var cmd in matches)
        {
            FilteredCommands.Add(cmd);
        }

        SelectedCommandPaletteItem = FilteredCommands.FirstOrDefault();
    }

    [RelayCommand]
    public void ExecuteSelectedCommandPaletteItem()
    {
        var selected = SelectedCommandPaletteItem;
        IsCommandPaletteOpen = false;
        selected?.Action?.Invoke();
    }

    public void ExecutePluginById(string pluginId)
    {
        var plugin = Plugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin != null)
        {
            _ = ExecutePluginAsync(plugin);
        }
    }

    public void InitializeCommands()
    {
        _allCommands.Clear();

        // File operations
        _allCommands.Add(new CommandPaletteItem("file.new", "New File", "File", "Ctrl+N", "📄", () => CreateNewDocument()));
        _allCommands.Add(new CommandPaletteItem("file.open", "Open File...", "File", "Ctrl+O", "📂", () => _ = OpenFileAsync()));
        _allCommands.Add(new CommandPaletteItem("file.openFolder", "Open Folder / Workspace...", "File", "Ctrl+Shift+O", "📁", () => _ = OpenFolderAsync()));
        _allCommands.Add(new CommandPaletteItem("file.quickOpen", "Quick Open...", "File", "Ctrl+P", "🔍", () => _ = ShowQuickOpenAsync()));
        _allCommands.Add(new CommandPaletteItem("file.save", "Save", "File", "Ctrl+S", "💾", () => _ = SaveAsync()));
        _allCommands.Add(new CommandPaletteItem("file.saveAs", "Save As...", "File", "Ctrl+Shift+S", "💾", () => _ = SaveAsAsync()));
        _allCommands.Add(new CommandPaletteItem("file.closeTab", "Close Tab", "File", "Ctrl+W", "✕", () => _ = CloseTabAsync(ActiveDocument)));
        _allCommands.Add(new CommandPaletteItem("file.closeOtherTabs", "Close Other Tabs", "File", "", "✕", () => _ = CloseOtherTabsAsync(ActiveDocument)));
        _allCommands.Add(new CommandPaletteItem("file.closeSavedTabs", "Close Saved Tabs", "File", "", "✕", () => _ = CloseSavedTabsAsync()));
        _allCommands.Add(new CommandPaletteItem("file.copyPath", "Copy Document Path", "File", "", "📋", () => _ = CopyDocumentPathAsync(ActiveDocument)));
        _allCommands.Add(new CommandPaletteItem("file.revealInExplorer", "Reveal in Explorer", "File", "", "📁", () => RevealInExplorer(ActiveDocument)));

        // Edit operations
        _allCommands.Add(new CommandPaletteItem("edit.find", "Find...", "Edit", "Ctrl+F", "🔎", () => ShowSearch()));
        _allCommands.Add(new CommandPaletteItem("edit.findInFiles", "Find in Files (Workspace Search)...", "Edit", "Ctrl+Shift+F", "🔍", () => OpenGlobalSearch()));
        _allCommands.Add(new CommandPaletteItem("edit.replace", "Replace...", "Edit", "Ctrl+H", "🔄", () => ShowReplace()));
        _allCommands.Add(new CommandPaletteItem("edit.goToLine", "Go to Line...", "Edit", "Ctrl+G", "🎯", () => ShowGoToLine()));
        _allCommands.Add(new CommandPaletteItem("edit.toggleComment", "Toggle Line Comment", "Edit", "Ctrl+/", "💬", () => ToggleComment()));
        _allCommands.Add(new CommandPaletteItem("edit.copyAll", "Copy All Content", "Edit", "Ctrl+Shift+C", "📋", () => _ = CopyAllAsync()));

        // Workspace operations
        _allCommands.Add(new CommandPaletteItem("workspace.newFile", "Workspace: New File...", "Workspace", "", "📄", () => _ = CreateFileInFolderAsync(null)));
        _allCommands.Add(new CommandPaletteItem("workspace.newFolder", "Workspace: New Folder...", "Workspace", "", "📁", () => _ = CreateFolderInFolderAsync(null)));
        _allCommands.Add(new CommandPaletteItem("workspace.refresh", "Workspace: Refresh Folder Tree", "Workspace", "", "🔄", () => _ = RefreshFolderItemAsync(null)));


        // View operations
        _allCommands.Add(new CommandPaletteItem("view.toggleSidebar", "Toggle Folder Sidebar", "View", "Ctrl+B", "📁", () => ToggleSidebar()));
        _allCommands.Add(new CommandPaletteItem("view.toggleSplit", "Toggle Split View", "View", "Ctrl+\\", "🔀", () => ToggleSplitView()));
        _allCommands.Add(new CommandPaletteItem("view.toggleMarkdown", "Toggle Markdown Live Preview", "View", "Ctrl+Shift+M", "📝", () => ToggleMarkdownPreview()));
        _allCommands.Add(new CommandPaletteItem("view.toggleRtl", "Toggle RTL / LTR Direction (Persian/Arabic)", "View", "Ctrl+Alt+R", "⇄", () => ToggleRtl()));
        _allCommands.Add(new CommandPaletteItem("view.zoomIn", "Zoom In", "View", "Ctrl++", "🔍", () => ZoomIn()));
        _allCommands.Add(new CommandPaletteItem("view.zoomOut", "Zoom Out", "View", "Ctrl+-", "🔍", () => ZoomOut()));
        _allCommands.Add(new CommandPaletteItem("view.resetZoom", "Reset Zoom", "View", "Ctrl+0", "🔍", () => ResetZoom()));
        _allCommands.Add(new CommandPaletteItem("view.toggleWordWrap", "Toggle Word Wrap", "View", "Alt+Z", "↩️", () => ToggleWordWrap()));
        _allCommands.Add(new CommandPaletteItem("view.toggleLineNumbers", "Toggle Line Numbers", "View", "", "#️⃣", () => ToggleLineNumbers()));

        // Run & Developer Tools
        _allCommands.Add(new CommandPaletteItem("run.activeScript", "Run Active Script (AgentLang, PS2, Python, PowerShell, Dart)", "Run", "F5", "▶", () => _ = RunActiveScriptAsync()));
        _allCommands.Add(new CommandPaletteItem("run.stopScript", "Stop Running Script", "Run", "", "⏹", () => RunnerViewModel.Cancel()));
        _allCommands.Add(new CommandPaletteItem("run.clearOutput", "Clear Runner Console Output", "Run", "", "🧹", () => RunnerViewModel.ClearOutput()));
        _allCommands.Add(new CommandPaletteItem("diff.compare", "Compare / Diff Active Document with File...", "Diff", "Ctrl+Shift+D", "🔍", () => _ = ShowDiffAsync()));
        _allCommands.Add(new CommandPaletteItem("diff.close", "Close Diff Viewer", "Diff", "", "✕", () => DiffViewModel.CloseDiff()));

        // Utilities & Plugins
        _allCommands.Add(new CommandPaletteItem("tools.jsonFormat", "Format JSON (2 spaces)", "Tools", "", "⚙️", () => ExecutePluginById("builtin-json-format")));
        _allCommands.Add(new CommandPaletteItem("tools.jsonMinify", "Minify JSON", "Tools", "", "⚙️", () => ExecutePluginById("builtin-json-minify")));
        _allCommands.Add(new CommandPaletteItem("tools.sortAsc", "Sort Lines (A to Z)", "Tools", "", "🔤", () => ExecutePluginById("builtin-sort-lines-asc")));
        _allCommands.Add(new CommandPaletteItem("tools.sortDesc", "Sort Lines (Z to A)", "Tools", "", "🔤", () => ExecutePluginById("builtin-sort-lines-desc")));
        _allCommands.Add(new CommandPaletteItem("tools.removeDuplicates", "Remove Duplicate Lines", "Tools", "", "✂️", () => ExecutePluginById("builtin-remove-duplicate-lines")));
        _allCommands.Add(new CommandPaletteItem("tools.reverseLines", "Reverse Lines", "Tools", "", "🔄", () => ExecutePluginById("builtin-reverse-lines")));
        _allCommands.Add(new CommandPaletteItem("tools.base64Encode", "Base64 Encode", "Tools", "", "🔐", () => ExecutePluginById("builtin-base64-encode")));
        _allCommands.Add(new CommandPaletteItem("tools.base64Decode", "Base64 Decode", "Tools", "", "🔓", () => ExecutePluginById("builtin-base64-decode")));
        _allCommands.Add(new CommandPaletteItem("tools.urlEncode", "URL Encode", "Tools", "", "🌐", () => ExecutePluginById("builtin-url-encode")));
        _allCommands.Add(new CommandPaletteItem("tools.urlDecode", "URL Decode", "Tools", "", "🌐", () => ExecutePluginById("builtin-url-decode")));
        _allCommands.Add(new CommandPaletteItem("tools.caseUpper", "Convert to UPPERCASE", "Tools", "", "🔠", () => ExecutePluginById("builtin-case-upper")));
        _allCommands.Add(new CommandPaletteItem("tools.caseLower", "Convert to lowercase", "Tools", "", "🔡", () => ExecutePluginById("builtin-case-lower")));
        _allCommands.Add(new CommandPaletteItem("tools.caseTitle", "Convert to Title Case", "Tools", "", "🔤", () => ExecutePluginById("builtin-case-title")));
        _allCommands.Add(new CommandPaletteItem("tools.statistics", "Text Statistics...", "Tools", "", "📊", () => ExecutePluginById("builtin-text-statistics")));

        // Preferences & Themes
        _allCommands.Add(new CommandPaletteItem("theme.darkPlus", "Theme: Dark+ (VS Code)", "Preferences", "", "🎨", () => SelectThemeById("dark-plus")));
        _allCommands.Add(new CommandPaletteItem("theme.oneDark", "Theme: One Dark Pro", "Preferences", "", "🎨", () => SelectThemeById("one-dark")));
        _allCommands.Add(new CommandPaletteItem("theme.monokai", "Theme: Monokai", "Preferences", "", "🎨", () => SelectThemeById("monokai")));
        _allCommands.Add(new CommandPaletteItem("theme.dracula", "Theme: Dracula", "Preferences", "", "🎨", () => SelectThemeById("dracula")));
        _allCommands.Add(new CommandPaletteItem("theme.solarizedDark", "Theme: Solarized Dark", "Preferences", "", "🎨", () => SelectThemeById("solarized-dark")));
        _allCommands.Add(new CommandPaletteItem("theme.gitHubLight", "Theme: GitHub Light", "Preferences", "", "🎨", () => SelectThemeById("github-light")));
        _allCommands.Add(new CommandPaletteItem("theme.import", "Import Color Theme (.json)...", "Preferences", "", "🎨", () => _ = ImportThemeAsync()));
        _allCommands.Add(new CommandPaletteItem("syntax.import", "Import Syntax Definition (.xshd)...", "Preferences", "", "📜", () => _ = ImportSyntaxAsync()));
        _allCommands.Add(new CommandPaletteItem("settings.open", "Open Settings...", "Preferences", "Ctrl+,", "⚙️", () => ShowSettings()));
        _allCommands.Add(new CommandPaletteItem("help.updates", "Check Language & Integration Updates...", "Help", "", "🔄", () => _ = CheckIntegrationsUpdateAsync()));
    }

    #endregion

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
