using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
    private readonly IAppUpdateService _appUpdateService;
    private readonly IRecentWorkspacesService _recentWorkspacesService;
    private readonly AppConfig _config;

    public record ClosedTabState(string? FilePath, string Title, string Content, int CaretLine, int CaretColumn, string Language);

    private CancellationTokenSource? _statusCts;
    private bool _isDisposed;

    [ObservableProperty]
    private TabManager _tabManager = null!;

    public ObservableCollection<DocumentViewModel> Documents => TabManager.Documents;

    public DocumentViewModel? ActiveDocument
    {
        get => TabManager.ActiveDocument;
        set => TabManager.ActiveDocument = value;
    }

    public bool HasDocuments => TabManager.HasDocuments;
    public bool HasNoDocuments => TabManager.HasNoDocuments;
    public bool CanReopenClosedTab => TabManager.CanReopenClosedTab;

    [ObservableProperty]
    private SearchViewModel _searchViewModel = new();

    [ObservableProperty]
    private ObservableCollection<RecentFileItem> _recentFiles = [];

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = [];

    [ObservableProperty]
    private bool _showBreadcrumbs = true;

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private bool _rainbowBracketsEnabled = true;

    [ObservableProperty]
    private bool _autoCloseBrackets = true;

    [ObservableProperty]
    private bool _enableAnimations = true;

    [ObservableProperty]
    private string _currentFontFamily = "Google Sans Code";

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
    private WorkspaceCoordinator _workspaceCoordinator = null!;

    public bool IsSidebarOpen
    {
        get => WorkspaceCoordinator.IsSidebarOpen;
        set => WorkspaceCoordinator.IsSidebarOpen = value;
    }

    public FolderItem? RootFolder
    {
        get => WorkspaceCoordinator.RootFolder;
        set => WorkspaceCoordinator.RootFolder = value;
    }

    public FolderItem? SelectedFolderItem
    {
        get => WorkspaceCoordinator.SelectedFolderItem;
        set => WorkspaceCoordinator.SelectedFolderItem = value;
    }

    public ObservableCollection<RecentWorkspaceItem> RecentWorkspaces => WorkspaceCoordinator.RecentWorkspaces;

    public bool HasRecentWorkspaces => WorkspaceCoordinator.HasRecentWorkspaces;

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
    private CommandPaletteCoordinator _commandPalette = new();

    public bool IsCommandPaletteOpen
    {
        get => CommandPalette.IsOpen;
        set => CommandPalette.IsOpen = value;
    }

    public string CommandPaletteQuery
    {
        get => CommandPalette.Query;
        set => CommandPalette.Query = value;
    }

    public ObservableCollection<CommandPaletteItem> FilteredCommands => CommandPalette.FilteredCommands;

    public CommandPaletteItem? SelectedCommandPaletteItem
    {
        get => CommandPalette.SelectedItem;
        set => CommandPalette.SelectedItem = value;
    }

    // 🔀 Split View
    [ObservableProperty]
    private bool _isSplitViewActive;

    [ObservableProperty]
    private DocumentViewModel? _secondaryDocument;

    // 📝 Markdown Live Preview
    [ObservableProperty]
    private bool _isMarkdownPreviewActive;

    [ObservableProperty]
    private bool _isMarkdownScrollSyncEnabled = true;

    // ▶ Runner, 🔍 Diff & 📑 Outline ViewModels
    [ObservableProperty]
    private RunnerViewModel _runnerViewModel = new();

    [ObservableProperty]
    private DiffViewModel _diffViewModel = new();

    [ObservableProperty]
    private OutlineViewModel _outlineViewModel = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlineVisible))]
    private bool _isOutlineActive;

    public bool IsOutlineVisible => IsSidebarOpen && IsOutlineActive;

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public ColorTheme CurrentTheme => _themeService.CurrentTheme;
    public IThemeService ThemeService => _themeService;
    public ILanguageService LanguageService => _languageService;
    public IPluginService PluginService => _pluginService;
    public ISettingsService SettingsService => _settingsService;
    public ISessionService SessionService => _sessionService;
    public IFileWatcherService FileWatcherService => _fileWatcherService;
    public IDialogService DialogService => _dialogService;
    public IAppUpdateService AppUpdateService => _appUpdateService;

    public bool IsRobotoMonoActive => (CurrentFontFamily ?? string.Empty).Contains("Roboto Mono", StringComparison.OrdinalIgnoreCase);
    public bool IsGoogleSansCodeActive => !IsRobotoMonoActive;

    partial void OnCurrentFontFamilyChanged(string value)
    {
        OnPropertyChanged(nameof(IsRobotoMonoActive));
        OnPropertyChanged(nameof(IsGoogleSansCodeActive));
    }

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
        IWorkspaceSearchService? workspaceSearchService = null,
        IAppUpdateService? appUpdateService = null,
        IRecentWorkspacesService? recentWorkspacesService = null,
        IGitService? gitService = null,
        WorkspaceCoordinator? workspaceCoordinator = null,
        CommandPaletteCoordinator? commandPalette = null,
        TabManager? tabManager = null)
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
        _appUpdateService = appUpdateService ?? new AppUpdateService();
        _recentWorkspacesService = recentWorkspacesService ?? new RecentWorkspacesService();

        _workspaceCoordinator = workspaceCoordinator ?? new WorkspaceCoordinator(_dialogService, _recentWorkspacesService, gitService);
        if (commandPalette != null)
        {
            _commandPalette = commandPalette;
        }

        _workspaceCoordinator.RequestOpenFile = fullPath => OpenFileInternalAsync(fullPath);
        _workspaceCoordinator.RequestCloseFile = async fullPath =>
        {
            var openDoc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (openDoc != null)
            {
                await CloseTabAsync(openDoc);
            }
        };
        _workspaceCoordinator.NotifyFileRenamed = (oldPath, newPath) =>
        {
            var openDoc = Documents.FirstOrDefault(d => string.Equals(d.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
            if (openDoc != null)
            {
                openDoc.FilePath = newPath;
                openDoc.Title = Path.GetFileName(newPath);
            }
        };
        _workspaceCoordinator.RequestStatusMessage = msg => ShowTemporaryStatusAsync(msg);
        _workspaceCoordinator.RequestSetClipboardText = path => RequestSetClipboardText != null ? RequestSetClipboardText(path) : Task.CompletedTask;
        _workspaceCoordinator.WorkspaceChanged = () => UpdateBreadcrumbs();

        _workspaceCoordinator.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceCoordinator.IsSidebarOpen))
            {
                OnPropertyChanged(nameof(IsSidebarOpen));
                OnPropertyChanged(nameof(IsOutlineVisible));
            }
            else if (e.PropertyName == nameof(WorkspaceCoordinator.RootFolder))
            {
                OnPropertyChanged(nameof(RootFolder));
                UpdateBreadcrumbs();
            }
            else if (e.PropertyName == nameof(WorkspaceCoordinator.SelectedFolderItem))
                OnPropertyChanged(nameof(SelectedFolderItem));
            else if (e.PropertyName == nameof(WorkspaceCoordinator.HasRecentWorkspaces))
                OnPropertyChanged(nameof(HasRecentWorkspaces));
        };

        _tabManager = tabManager ?? new TabManager(
            _fileService,
            _languageService,
            _dialogService,
            _themeService,
            _settingsService,
            _fileWatcherService,
            _recentFilesService,
            _config);

        _tabManager.RequestGoToLine = (line, col) => RequestGoToLine?.Invoke(line, col);
        _tabManager.RequestSetClipboardText = text => RequestSetClipboardText != null ? RequestSetClipboardText(text) : Task.CompletedTask;
        _tabManager.RequestStatusMessage = (msg, duration) => ShowTemporaryStatusAsync(msg, duration);
        _tabManager.GetWorkspaceRootPath = () => RootFolder?.FullPath;
        _tabManager.GetCurrentTheme = () => CurrentTheme;
        _tabManager.RequestSaveSession = () => SaveCurrentSessionAsync();
        _tabManager.RequestReloadRecentFiles = () => LoadRecentFilesAsync();
        _tabManager.DocumentSaved = path => WorkspaceCoordinator.RequestDebouncedGitStatusRefresh();

        _fileWatcherService.FileChangedOnDisk += OnFileWatcherChangedOnDisk;

        _tabManager.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TabManager.ActiveDocument))
            {
                OnPropertyChanged(nameof(ActiveDocument));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(FilteredLanguageOptions));
                OnPropertyChanged(nameof(IsActiveDocumentMarkdown));
                if (!IsActiveDocumentMarkdown && IsMarkdownPreviewActive)
                {
                    IsMarkdownPreviewActive = false;
                }
                UpdateBreadcrumbs();
                OutlineViewModel.AttachDocument(ActiveDocument);
                OnPropertyChanged(nameof(MarkdownStatsDisplay));
            }
            else if (e.PropertyName == nameof(TabManager.HasDocuments))
                OnPropertyChanged(nameof(HasDocuments));
            else if (e.PropertyName == nameof(TabManager.HasNoDocuments))
                OnPropertyChanged(nameof(HasNoDocuments));
            else if (e.PropertyName == nameof(TabManager.CanReopenClosedTab))
                OnPropertyChanged(nameof(CanReopenClosedTab));
            else if (e.PropertyName == nameof(TabManager.Documents))
                OnPropertyChanged(nameof(Documents));
        };

        _tabManager.DocumentsChanged = () =>
        {
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(HasNoDocuments));
            UpdateBreadcrumbs();
        };

        _tabManager.DocumentStateChanged = () =>
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(FilteredLanguageOptions));
            OnPropertyChanged(nameof(IsActiveDocumentMarkdown));
            if (!IsActiveDocumentMarkdown && IsMarkdownPreviewActive)
            {
                IsMarkdownPreviewActive = false;
            }
            UpdateBreadcrumbs();
        };

        var settings = _settingsService.CurrentSettings;
        _currentFontFamily = settings.FontFamily;
        _showBreadcrumbs = settings.ShowBreadcrumbs;
        _showMinimap = settings.ShowMinimap;
        _rainbowBracketsEnabled = settings.EnableRainbowBrackets;
        _autoCloseBrackets = settings.AutoCloseBrackets;
        _enableAnimations = settings.EnableAnimations;

        RunnerViewModel.GetWorkingDirectory = () => WorkspaceCoordinator.RootFolder?.FullPath ?? Path.GetDirectoryName(ActiveDocument?.FilePath) ?? Environment.CurrentDirectory;
        OutlineViewModel.RequestGoToLine = (line, col) => RequestGoToLine?.Invoke(line, col);

        var defaultShell = settings.DefaultTerminalShell;
        var matchedShell = RunnerViewModel.AvailableShells.FirstOrDefault(s => string.Equals(s.Type.ToString(), defaultShell, StringComparison.OrdinalIgnoreCase));
        if (matchedShell != null)
        {
            RunnerViewModel.SelectedShell = matchedShell;
        }

        RunnerViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RunnerViewModel.SelectedShell))
            {
                var current = _settingsService.CurrentSettings;
                if (current.DefaultTerminalShell != RunnerViewModel.SelectedShell.Type.ToString())
                {
                    current.DefaultTerminalShell = RunnerViewModel.SelectedShell.Type.ToString();
                    _ = _settingsService.SaveAsync(current);
                }
            }
        };

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

        _commandPalette.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteCoordinator.IsOpen))
                OnPropertyChanged(nameof(IsCommandPaletteOpen));
            else if (e.PropertyName == nameof(CommandPaletteCoordinator.Query))
                OnPropertyChanged(nameof(CommandPaletteQuery));
            else if (e.PropertyName == nameof(CommandPaletteCoordinator.SelectedItem))
                OnPropertyChanged(nameof(SelectedCommandPaletteItem));
            else if (e.PropertyName == nameof(CommandPaletteCoordinator.FilteredCommands))
                OnPropertyChanged(nameof(FilteredCommands));
        };

        InitializeCommands();
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        CurrentFontFamily = settings.FontFamily;
        ShowBreadcrumbs = settings.ShowBreadcrumbs;
        ShowMinimap = settings.ShowMinimap;
        RainbowBracketsEnabled = settings.EnableRainbowBrackets;
        AutoCloseBrackets = settings.AutoCloseBrackets;
        EnableAnimations = settings.EnableAnimations;
        if (!string.IsNullOrEmpty(settings.DefaultTerminalShell))
        {
            var matched = RunnerViewModel.AvailableShells.FirstOrDefault(s => string.Equals(s.Type.ToString(), settings.DefaultTerminalShell, StringComparison.OrdinalIgnoreCase));
            if (matched != null && RunnerViewModel.SelectedShell.Type != matched.Type)
            {
                RunnerViewModel.SelectedShell = matched;
            }
        }

        TabManager.ApplySettings(settings);
        _themeService.ApplyTheme(settings);
        ApplyCodeColorsToAllDocuments(CurrentTheme);
    }

    public void ApplyCodeColorsToAllDocuments(ColorTheme? theme = null)
    {
        TabManager.ApplyCodeColorsToAllDocuments(theme ?? CurrentTheme);
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

        // 2. Load Recent Files and Workspaces
        await LoadRecentFilesAsync();
        await LoadRecentWorkspacesAsync();

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

        // 6. Check for updates in background if enabled
        if (settings.AutoCheckForUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000);
                    var info = await _appUpdateService.CheckForUpdatesAsync(isManualCheck: false);
                    if (info.IsUpdateAvailable)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _ = ShowTemporaryStatusAsync($"⚡ Update available: Code Viewer {info.LatestVersion}", 8000);
                        });
                    }
                }
                catch
                {
                    // Silent on auto-check background failure
                }
            });
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

    public Task LoadRecentWorkspacesAsync() => WorkspaceCoordinator.LoadRecentWorkspacesAsync();

    public Task AddRecentWorkspaceAsync(string folderPath) => WorkspaceCoordinator.AddRecentWorkspaceAsync(folderPath);

    [RelayCommand]
    public Task OpenRecentWorkspaceAsync(string? folderPath) => WorkspaceCoordinator.OpenRecentWorkspaceAsync(folderPath);

    [RelayCommand]
    public Task ClearRecentWorkspacesAsync() => WorkspaceCoordinator.ClearRecentWorkspacesAsync();

    public void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        if (ActiveDocument == null)
        {
            if (RootFolder != null)
            {
                Breadcrumbs.Add(new BreadcrumbItem
                {
                    Name = RootFolder.Name,
                    FullPath = RootFolder.FullPath,
                    IsFolder = true,
                    IsLast = true,
                    ClickCommand = new RelayCommand(() => IsSidebarOpen = true)
                });
            }
            return;
        }

        var filePath = ActiveDocument.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            Breadcrumbs.Add(new BreadcrumbItem
            {
                Name = ActiveDocument.DisplayName,
                FullPath = string.Empty,
                IsFolder = false,
                IsLast = true
            });
            return;
        }

        try
        {
            var normalizedFile = Path.GetFullPath(filePath);
            var items = new List<BreadcrumbItem>();

            if (RootFolder != null && !string.IsNullOrEmpty(RootFolder.FullPath) &&
                normalizedFile.StartsWith(RootFolder.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                // Root workspace item
                items.Add(new BreadcrumbItem
                {
                    Name = RootFolder.Name,
                    FullPath = RootFolder.FullPath,
                    IsFolder = true,
                    IsLast = false,
                    ClickCommand = new RelayCommand(() => IsSidebarOpen = true)
                });

                // Relative directory segments
                var relativePath = Path.GetRelativePath(RootFolder.FullPath, normalizedFile);
                var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                var currentAccumulated = RootFolder.FullPath;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    currentAccumulated = Path.Combine(currentAccumulated, parts[i]);
                    items.Add(new BreadcrumbItem
                    {
                        Name = parts[i],
                        FullPath = currentAccumulated,
                        IsFolder = true,
                        IsLast = false,
                        ClickCommand = new RelayCommand(() => IsSidebarOpen = true)
                    });
                }

                // File item
                items.Add(new BreadcrumbItem
                {
                    Name = Path.GetFileName(normalizedFile),
                    FullPath = normalizedFile,
                    IsFolder = false,
                    IsLast = true,
                    ClickCommand = new AsyncRelayCommand(async () =>
                    {
                        if (RequestSetClipboardText != null)
                        {
                            await RequestSetClipboardText(normalizedFile);
                            _ = ShowTemporaryStatusAsync($"Copied: {normalizedFile}");
                        }
                    })
                });
            }
            else
            {
                var dir = Path.GetDirectoryName(normalizedFile);
                if (!string.IsNullOrEmpty(dir))
                {
                    var parentDirName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(parentDirName))
                    {
                        parentDirName = dir;
                    }
                    items.Add(new BreadcrumbItem
                    {
                        Name = parentDirName,
                        FullPath = dir,
                        IsFolder = true,
                        IsLast = false
                    });
                }

                items.Add(new BreadcrumbItem
                {
                    Name = Path.GetFileName(normalizedFile),
                    FullPath = normalizedFile,
                    IsFolder = false,
                    IsLast = true,
                    ClickCommand = new AsyncRelayCommand(async () =>
                    {
                        if (RequestSetClipboardText != null)
                        {
                            await RequestSetClipboardText(normalizedFile);
                            _ = ShowTemporaryStatusAsync($"Copied: {normalizedFile}");
                        }
                    })
                });
            }

            foreach (var item in items)
            {
                Breadcrumbs.Add(item);
            }
        }
        catch
        {
            Breadcrumbs.Add(new BreadcrumbItem
            {
                Name = ActiveDocument.DisplayName,
                FullPath = filePath,
                IsFolder = false,
                IsLast = true
            });
        }
    }

    [RelayCommand]
    public void ChangeEncoding(string? encodingName)
    {
        if (ActiveDocument == null || string.IsNullOrWhiteSpace(encodingName)) return;

        FileEncodingInfo? info = encodingName switch
        {
            "UTF-8" => FileEncodingInfo.Utf8NoBom,
            "UTF-8-BOM" or "UTF-8 with BOM" => FileEncodingInfo.Utf8WithBom,
            "UTF-16 LE" or "UTF-16" => new FileEncodingInfo(Encoding.Unicode, true),
            "UTF-16 BE" => new FileEncodingInfo(Encoding.BigEndianUnicode, true),
            "ASCII" => new FileEncodingInfo(Encoding.ASCII, false),
            "Windows-1252" or "ANSI" => new FileEncodingInfo(Encoding.GetEncoding(1252), false),
            _ => null
        };

        if (info != null)
        {
            ActiveDocument.SetEncoding(info);
            _ = ShowTemporaryStatusAsync($"Encoding set to {info.DisplayName}");
        }
    }

    [RelayCommand]
    public void ChangeLineEnding(string? ending)
    {
        if (ActiveDocument == null || string.IsNullOrWhiteSpace(ending)) return;

        var targetType = ending.ToUpperInvariant() switch
        {
            "CRLF" or "WINDOWS" or "\\R\\N" => LineEndingType.CRLF,
            "LF" or "UNIX" or "\\N" => LineEndingType.LF,
            _ => LineEndingType.LF
        };

        ActiveDocument.SetLineEnding(targetType);
        _ = ShowTemporaryStatusAsync($"Line endings converted to {targetType.ToDisplayString()}");
    }

    [RelayCommand]
    public void ToggleLineEnding()
    {
        if (ActiveDocument == null) return;
        var next = ActiveDocument.Model.LineEnding == LineEndingType.CRLF ? LineEndingType.LF : LineEndingType.CRLF;
        ActiveDocument.SetLineEnding(next);
        _ = ShowTemporaryStatusAsync($"Line endings converted to {next.ToDisplayString()}");
    }

    [RelayCommand]
    public void SetTabSize(string? sizeStr)
    {
        if (ActiveDocument == null || string.IsNullOrWhiteSpace(sizeStr)) return;
        if (int.TryParse(sizeStr, out var size) && size > 0 && size <= 8)
        {
            ActiveDocument.TabSize = size;
            _ = ShowTemporaryStatusAsync($"Indentation set to {size} spaces");
        }
    }

    [RelayCommand]
    public async Task ToggleBreadcrumbsAsync()
    {
        ShowBreadcrumbs = !ShowBreadcrumbs;
        var settings = _settingsService.CurrentSettings;
        settings.ShowBreadcrumbs = ShowBreadcrumbs;
        await _settingsService.SaveAsync(settings);
    }

    [RelayCommand]
    public async Task ToggleMinimapAsync()
    {
        ShowMinimap = !ShowMinimap;
        var settings = _settingsService.CurrentSettings;
        settings.ShowMinimap = ShowMinimap;
        await _settingsService.SaveAsync(settings);
    }

    [RelayCommand]
    public async Task ToggleRainbowBracketsAsync()
    {
        RainbowBracketsEnabled = !RainbowBracketsEnabled;
        var settings = _settingsService.CurrentSettings;
        settings.EnableRainbowBrackets = RainbowBracketsEnabled;
        await _settingsService.SaveAsync(settings);
    }

    [RelayCommand]
    public async Task ToggleAutoCloseBracketsAsync()
    {
        AutoCloseBrackets = !AutoCloseBrackets;
        var settings = _settingsService.CurrentSettings;
        settings.AutoCloseBrackets = AutoCloseBrackets;
        await _settingsService.SaveAsync(settings);
    }

    [RelayCommand]
    public void CreateNewDocument() => TabManager.CreateNewDocument();

    [RelayCommand]
    public Task OpenFileAsync() => TabManager.OpenFileAsync();

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
    public Task OpenRecentFileAsync(string? filePath) => TabManager.OpenRecentFileAsync(filePath);

    public Task OpenFileInternalAsync(string filePath, int targetLine = 1, int targetCol = 1) => TabManager.OpenFileInternalAsync(filePath, targetLine, targetCol);

    [RelayCommand]
    public Task SaveAsync() => TabManager.SaveAsync();

    [RelayCommand]
    public Task SaveAsAsync() => TabManager.SaveAsAsync();

    [RelayCommand]
    public Task CloseTabAsync(DocumentViewModel? doc) => TabManager.CloseTabAsync(doc);

    [RelayCommand]
    public Task ReopenClosedTabAsync() => TabManager.ReopenClosedTabAsync();

    #region Tab Context Menu Operations

    [RelayCommand]
    public Task CloseOtherTabsAsync(DocumentViewModel? doc) => TabManager.CloseOtherTabsAsync(doc);

    [RelayCommand]
    public Task CloseTabsToTheRightAsync(DocumentViewModel? doc) => TabManager.CloseTabsToTheRightAsync(doc);

    [RelayCommand]
    public Task CloseSavedTabsAsync() => TabManager.CloseSavedTabsAsync();

    [RelayCommand]
    public Task CopyDocumentPathAsync(DocumentViewModel? doc) => TabManager.CopyDocumentPathAsync(doc);

    [RelayCommand]
    public Task CopyDocumentRelativePathAsync(DocumentViewModel? doc) => TabManager.CopyDocumentRelativePathAsync(doc);

    [RelayCommand]
    public void RevealInExplorer(DocumentViewModel? doc) => TabManager.RevealInExplorer(doc);

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

    #region Editor and Theme Operations

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

    [RelayCommand]
    public void SelectTheme(ColorTheme? theme)
    {
        if (theme != null)
        {
            _themeService.ApplyTheme(theme);
            OnPropertyChanged(nameof(CurrentTheme));
            var settings = _settingsService.CurrentSettings;
            settings.ThemeId = theme.Id;
            if (theme.IsDark)
            {
                settings.ThemeMode = "Dark";
                settings.DarkPresetId = theme.Id;
                settings.DarkBackgroundHex = theme.WindowBackground;
                settings.DarkForegroundHex = theme.Foreground;
                settings.DarkAccentHex = theme.AccentColor;
            }
            else
            {
                settings.ThemeMode = "Light";
                settings.LightPresetId = theme.Id;
                settings.LightBackgroundHex = theme.WindowBackground;
                settings.LightForegroundHex = theme.Foreground;
                settings.LightAccentHex = theme.AccentColor;
            }
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
                SelectTheme(theme);
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

    public void UpdateActiveDocumentSelection() => TabManager.UpdateActiveDocumentSelection();

    public bool IsActiveDocumentMarkdown =>
        ActiveDocument != null &&
        (string.Equals(ActiveDocument.Language, "Markdown", StringComparison.OrdinalIgnoreCase) ||
         (ActiveDocument.FilePath?.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ?? false) ||
         (ActiveDocument.FilePath?.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ?? false));

    #region Folder Workspace & Tree Sidebar Operations

    [RelayCommand]
    public Task OpenFolderAsync() => WorkspaceCoordinator.OpenFolderAsync();

    public void OpenFolder(string folderPath) => WorkspaceCoordinator.OpenFolder(folderPath);

    [RelayCommand]
    public void CloseFolder() => WorkspaceCoordinator.CloseFolder();

    [RelayCommand]
    public void ToggleSidebar() => WorkspaceCoordinator.ToggleSidebar();

    [RelayCommand]
    public void ShowExplorerSidebar()
    {
        WorkspaceCoordinator.IsSidebarOpen = true;
        IsOutlineActive = false;
    }

    [RelayCommand]
    public void ShowOutlineSidebar()
    {
        WorkspaceCoordinator.IsSidebarOpen = true;
        IsOutlineActive = true;
    }

    [RelayCommand]
    public void ToggleOutline()
    {
        if (!WorkspaceCoordinator.IsSidebarOpen)
        {
            WorkspaceCoordinator.IsSidebarOpen = true;
            IsOutlineActive = true;
        }
        else if (IsOutlineActive)
        {
            WorkspaceCoordinator.IsSidebarOpen = false;
        }
        else
        {
            IsOutlineActive = true;
        }
    }

    [RelayCommand]
    public Task SelectFolderItemAsync(FolderItem? item) => WorkspaceCoordinator.SelectFolderItemAsync(item);

    [RelayCommand]
    public Task CreateFileInFolderAsync(FolderItem? item) => WorkspaceCoordinator.CreateFileInFolderAsync(item);

    [RelayCommand]
    public Task CreateFolderInFolderAsync(FolderItem? item) => WorkspaceCoordinator.CreateFolderInFolderAsync(item);

    [RelayCommand]
    public Task RenameFolderItemAsync(FolderItem? item) => WorkspaceCoordinator.RenameFolderItemAsync(item);

    [RelayCommand]
    public Task DeleteFolderItemAsync(FolderItem? item) => WorkspaceCoordinator.DeleteFolderItemAsync(item);

    [RelayCommand]
    public void RevealFolderItemInExplorer(FolderItem? item) => WorkspaceCoordinator.RevealFolderItemInExplorer(item);

    [RelayCommand]
    public Task CopyItemPathAsync(FolderItem? item) => WorkspaceCoordinator.CopyItemPathAsync(item);

    [RelayCommand]
    public Task CopyItemRelativePathAsync(FolderItem? item) => WorkspaceCoordinator.CopyItemRelativePathAsync(item);

    [RelayCommand]
    public Task RefreshFolderItemAsync(FolderItem? item) => WorkspaceCoordinator.RefreshFolderItemAsync(item);

    public IReadOnlyDictionary<string, GitFileStatus> CachedGitStatuses => WorkspaceCoordinator.CachedGitStatuses;
    public string? GitRepoRoot => WorkspaceCoordinator.GitRepoRoot;

    [RelayCommand]
    public Task RefreshGitStatusAsync() => WorkspaceCoordinator.RefreshGitStatusAsync();

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
        if (IsMarkdownPreviewActive)
        {
            OnPropertyChanged(nameof(MarkdownStatsDisplay));
        }
    }

    [RelayCommand]
    public void ToggleMarkdownScrollSync()
    {
        IsMarkdownScrollSyncEnabled = !IsMarkdownScrollSyncEnabled;
    }

    [RelayCommand]
    public async Task CopyMarkdownPreviewAsync()
    {
        if (ActiveDocument == null) return;
        string text;
        try
        {
            text = ActiveDocument.TextDocument.Text;
        }
        catch (InvalidOperationException)
        {
            text = ActiveDocument.Model.Text ?? string.Empty;
        }
        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(text);
            _ = ShowTemporaryStatusAsync("Markdown content copied to clipboard.");
        }
    }

    public string MarkdownStatsDisplay
    {
        get
        {
            if (ActiveDocument == null) return string.Empty;
            string text;
            try
            {
                text = ActiveDocument.TextDocument.Text;
            }
            catch (InvalidOperationException)
            {
                text = ActiveDocument.Model.Text ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(text)) return "0 words";
            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var lines = text.Split('\n').Length;
            return $"{words:N0} words • {lines} lines";
        }
    }

    public void RefreshMarkdownStats()
    {
        OnPropertyChanged(nameof(MarkdownStatsDisplay));
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
            await RunnerViewModel.RunScriptAsync(ActiveDocument.FilePath, WorkspaceCoordinator.RootFolder?.FullPath);
        }
        else
        {
            _ = ShowTemporaryStatusAsync("Please save the file before running.");
        }
    }

    [RelayCommand]
    public void ToggleRunnerConsole()
    {
        RunnerViewModel.IsVisible = !RunnerViewModel.IsVisible;
    }

    #endregion

    #region Font Switching & App Updates

    [RelayCommand]
    public async Task ToggleCodingFontAsync()
    {
        var current = CurrentFontFamily ?? string.Empty;
        var target = current.Contains("Roboto Mono", StringComparison.OrdinalIgnoreCase)
            ? "Google Sans Code"
            : "Roboto Mono";

        await SetCodingFontAsync(target);
    }

    [RelayCommand]
    public async Task SetCodingFontAsync(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return;

        var sanitized = AppSettings.SanitizeFontFamily(fontName);
        CurrentFontFamily = sanitized;

        var settings = _settingsService.CurrentSettings;
        settings.FontFamily = sanitized;
        await _settingsService.SaveAsync(settings);

        _ = ShowTemporaryStatusAsync($"Coding font switched to: {sanitized}");
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        _ = ShowTemporaryStatusAsync("Checking for Code Viewer updates...");
        try
        {
            var updateInfo = await _appUpdateService.CheckForUpdatesAsync(isManualCheck: true);
            if (updateInfo.IsUpdateAvailable)
            {
                _ = ShowTemporaryStatusAsync($"Update available: {updateInfo.LatestVersion}");
                await _dialogService.ShowUpdateDialogAsync(updateInfo, _appUpdateService);
            }
            else
            {
                _ = ShowTemporaryStatusAsync($"Code Viewer is up to date ({updateInfo.CurrentVersion}).");
                await _dialogService.ShowMessageAsync("Check for Updates", $"You are running the latest version of Code Viewer ({updateInfo.CurrentVersion}).");
            }
        }
        catch (Exception ex)
        {
            _ = ShowTemporaryStatusAsync("Failed to check for updates.");
            await _dialogService.ShowMessageAsync("Update Check Failed", $"Could not check for updates:\n{ex.Message}");
        }
    }

    #endregion

    #region Command Palette Operations

    [RelayCommand]
    public void ShowCommandPalette() => CommandPalette.Show();

    [RelayCommand]
    public void CloseCommandPalette() => CommandPalette.Close();

    public void FilterCommands(string query) => CommandPalette.Filter(query);

    [RelayCommand]
    public void ExecuteSelectedCommandPaletteItem() => CommandPalette.ExecuteSelected();

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
        CommandPalette.ClearCommands();

        // File operations
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.new", "New File", "File", "Ctrl+N", "📄", () => CreateNewDocument()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.open", "Open File...", "File", "Ctrl+O", "📂", () => _ = OpenFileAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.openFolder", "Open Folder / Workspace...", "File", "Ctrl+Shift+O", "📁", () => _ = OpenFolderAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.quickOpen", "Quick Open...", "File", "Ctrl+P", "🔍", () => _ = ShowQuickOpenAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.save", "Save", "File", "Ctrl+S", "💾", () => _ = SaveAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.saveAs", "Save As...", "File", "Ctrl+Shift+S", "💾", () => _ = SaveAsAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.closeTab", "Close Tab", "File", "Ctrl+W", "✕", () => _ = CloseTabAsync(ActiveDocument)));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.closeOtherTabs", "Close Other Tabs", "File", "", "✕", () => _ = CloseOtherTabsAsync(ActiveDocument)));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.closeSavedTabs", "Close Saved Tabs", "File", "", "✕", () => _ = CloseSavedTabsAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.copyPath", "Copy Document Path", "File", "", "📋", () => _ = CopyDocumentPathAsync(ActiveDocument)));
        CommandPalette.RegisterCommand(new CommandPaletteItem("file.revealInExplorer", "Reveal in Explorer", "File", "", "📁", () => RevealInExplorer(ActiveDocument)));

        // Edit operations
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.find", "Find...", "Edit", "Ctrl+F", "🔎", () => ShowSearch()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.findInFiles", "Find in Files (Workspace Search)...", "Edit", "Ctrl+Shift+F", "🔍", () => OpenGlobalSearch()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.replace", "Replace...", "Edit", "Ctrl+H", "🔄", () => ShowReplace()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.goToLine", "Go to Line...", "Edit", "Ctrl+G", "🎯", () => ShowGoToLine()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.toggleComment", "Toggle Line Comment", "Edit", "Ctrl+/", "💬", () => ToggleComment()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("edit.copyAll", "Copy All Content", "Edit", "Ctrl+Shift+C", "📋", () => _ = CopyAllAsync()));

        // Workspace operations
        CommandPalette.RegisterCommand(new CommandPaletteItem("workspace.newFile", "Workspace: New File...", "Workspace", "", "📄", () => _ = CreateFileInFolderAsync(null)));
        CommandPalette.RegisterCommand(new CommandPaletteItem("workspace.newFolder", "Workspace: New Folder...", "Workspace", "", "📁", () => _ = CreateFolderInFolderAsync(null)));
        CommandPalette.RegisterCommand(new CommandPaletteItem("workspace.refresh", "Workspace: Refresh Folder Tree", "Workspace", "", "🔄", () => _ = RefreshFolderItemAsync(null)));


        // View operations
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleSidebar", "Toggle Folder Sidebar", "View", "Ctrl+B", "📁", () => ToggleSidebar()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleOutline", "Toggle Document Outline", "View", "Ctrl+Alt+O", "📑", () => ToggleOutline()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.focusExplorer", "Focus File Explorer", "View", "Ctrl+Shift+E", "📁", () => ShowExplorerSidebar()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.focusOutline", "Focus Document Outline", "View", "", "📑", () => ShowOutlineSidebar()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleSplit", "Toggle Split View", "View", "Ctrl+\\", "🔀", () => ToggleSplitView()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleMarkdown", "Toggle Markdown Live Preview", "View", "Ctrl+Shift+M", "📝", () => ToggleMarkdownPreview()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("markdown.toggleScrollSync", "Markdown: Toggle Synchronized Scrolling", "View", "", "🔗", () => ToggleMarkdownScrollSync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("markdown.copyText", "Markdown: Copy Preview Content as Text", "Edit", "", "📋", () => _ = CopyMarkdownPreviewAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleRtl", "Toggle RTL / LTR Direction (Persian/Arabic)", "View", "Ctrl+Alt+R", "⇄", () => ToggleRtl()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.zoomIn", "Zoom In", "View", "Ctrl++", "🔍", () => ZoomIn()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.zoomOut", "Zoom Out", "View", "Ctrl+-", "🔍", () => ZoomOut()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.resetZoom", "Reset Zoom", "View", "Ctrl+0", "🔍", () => ResetZoom()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleWordWrap", "Toggle Word Wrap", "View", "Alt+Z", "↩️", () => ToggleWordWrap()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.toggleLineNumbers", "Toggle Line Numbers", "View", "", "#️⃣", () => ToggleLineNumbers()));

        // Run & Developer Tools
        CommandPalette.RegisterCommand(new CommandPaletteItem("terminal.toggle", "Terminal: Toggle Console", "Terminal", "Ctrl+`", "💻", () => ToggleRunnerConsole()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("run.activeScript", "Terminal: Run Active Script (AgentLang, PS2, Python, PowerShell, Dart)", "Terminal", "F5", "▶", () => _ = RunActiveScriptAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("run.stopScript", "Terminal: Stop Process", "Terminal", "Shift+F5", "⏹", () => RunnerViewModel.Cancel()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("run.clearOutput", "Terminal: Clear Console Output", "Terminal", "Ctrl+K", "🧹", () => RunnerViewModel.ClearOutput()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("diff.compare", "Compare / Diff Active Document with File...", "Diff", "Ctrl+Shift+D", "🔍", () => _ = ShowDiffAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("diff.close", "Close Diff Viewer", "Diff", "", "✕", () => DiffViewModel.CloseDiff()));

        // Utilities & Plugins
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.jsonFormat", "Format JSON (2 spaces)", "Tools", "", "⚙️", () => ExecutePluginById("builtin-json-format")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.jsonMinify", "Minify JSON", "Tools", "", "⚙️", () => ExecutePluginById("builtin-json-minify")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.sortAsc", "Sort Lines (A to Z)", "Tools", "", "🔤", () => ExecutePluginById("builtin-sort-lines-asc")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.sortDesc", "Sort Lines (Z to A)", "Tools", "", "🔤", () => ExecutePluginById("builtin-sort-lines-desc")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.removeDuplicates", "Remove Duplicate Lines", "Tools", "", "✂️", () => ExecutePluginById("builtin-remove-duplicate-lines")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.reverseLines", "Reverse Lines", "Tools", "", "🔄", () => ExecutePluginById("builtin-reverse-lines")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.base64Encode", "Base64 Encode", "Tools", "", "🔐", () => ExecutePluginById("builtin-base64-encode")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.base64Decode", "Base64 Decode", "Tools", "", "🔓", () => ExecutePluginById("builtin-base64-decode")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.urlEncode", "URL Encode", "Tools", "", "🌐", () => ExecutePluginById("builtin-url-encode")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.urlDecode", "URL Decode", "Tools", "", "🌐", () => ExecutePluginById("builtin-url-decode")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.caseUpper", "Convert to UPPERCASE", "Tools", "", "🔠", () => ExecutePluginById("builtin-case-upper")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.caseLower", "Convert to lowercase", "Tools", "", "🔡", () => ExecutePluginById("builtin-case-lower")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.caseTitle", "Convert to Title Case", "Tools", "", "🔤", () => ExecutePluginById("builtin-case-title")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("tools.statistics", "Text Statistics...", "Tools", "", "📊", () => ExecutePluginById("builtin-text-statistics")));

        // Preferences & Themes
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.darkPlus", "Theme: Dark+ (VS Code)", "Preferences", "", "🎨", () => SelectThemeById("dark-plus")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.oneDark", "Theme: One Dark Pro", "Preferences", "", "🎨", () => SelectThemeById("one-dark")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.monokai", "Theme: Monokai", "Preferences", "", "🎨", () => SelectThemeById("monokai")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.dracula", "Theme: Dracula", "Preferences", "", "🎨", () => SelectThemeById("dracula")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.solarizedDark", "Theme: Solarized Dark", "Preferences", "", "🎨", () => SelectThemeById("solarized-dark")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.tokyoNight", "Theme: Tokyo Night", "Preferences", "", "🎨", () => SelectThemeById("tokyo-night")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.nord", "Theme: Nord", "Preferences", "", "🎨", () => SelectThemeById("nord")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.catppuccinMocha", "Theme: Catppuccin Mocha", "Preferences", "", "🎨", () => SelectThemeById("catppuccin-mocha")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.gitHubDark", "Theme: GitHub Dark", "Preferences", "", "🎨", () => SelectThemeById("github-dark")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.gitHubLight", "Theme: GitHub Light", "Preferences", "", "🎨", () => SelectThemeById("github-light")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.solarizedLight", "Theme: Solarized Light", "Preferences", "", "🎨", () => SelectThemeById("solarized-light")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.catppuccinLatte", "Theme: Catppuccin Latte", "Preferences", "", "🎨", () => SelectThemeById("catppuccin-latte")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.cyberpunk", "Theme: Synthwave '84 (Cyberpunk)", "Preferences", "", "🎨", () => SelectThemeById("cyberpunk")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.gruvboxDark", "Theme: Gruvbox Dark", "Preferences", "", "🎨", () => SelectThemeById("gruvbox-dark")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.monokaiPro", "Theme: Monokai Pro", "Preferences", "", "🎨", () => SelectThemeById("monokai-pro")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.rosePine", "Theme: Rosé Pine", "Preferences", "", "🎨", () => SelectThemeById("rose-pine")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.auraDark", "Theme: Aura Dark", "Preferences", "", "🎨", () => SelectThemeById("aura-dark")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.oneLight", "Theme: One Light", "Preferences", "", "🎨", () => SelectThemeById("one-light")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("theme.import", "Import Color Theme (.json)...", "Preferences", "", "🎨", () => _ = ImportThemeAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("syntax.import", "Import Syntax Definition (.xshd)...", "Preferences", "", "📜", () => _ = ImportSyntaxAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("settings.open", "Open Settings...", "Preferences", "Ctrl+,", "⚙️", () => ShowSettings()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("font.toggle", "Toggle Coding Font (Google Sans Code ⇄ Roboto Mono)", "Preferences", "Ctrl+Alt+F", "🔤", () => _ = ToggleCodingFontAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("font.googleSansCode", "Font: Google Sans Code", "Preferences", "", "🔤", () => _ = SetCodingFontAsync("Google Sans Code")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("font.robotoMono", "Font: Roboto Mono", "Preferences", "", "🔤", () => _ = SetCodingFontAsync("Roboto Mono")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.breadcrumbs", "Toggle Breadcrumbs Bar", "View", "", "🧭", () => _ = ToggleBreadcrumbsAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.minimap", "Toggle Code Minimap", "View", "Ctrl+Alt+M", "🗺️", () => _ = ToggleMinimapAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("view.rainbowBrackets", "Toggle Rainbow Brackets", "View", "", "🌈", () => _ = ToggleRainbowBracketsAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("editor.autoCloseBrackets", "Toggle Auto-Close Brackets & Quotes", "Editor", "", "()", () => _ = ToggleAutoCloseBracketsAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("editor.toggleLineEnding", "Toggle Line Endings (CRLF ⇄ LF)", "Editor", "", "↵", () => ToggleLineEnding()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("editor.encodingUtf8", "Set Encoding: UTF-8", "Editor", "", "🔤", () => ChangeEncoding("UTF-8")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("editor.encodingUtf8Bom", "Set Encoding: UTF-8 with BOM", "Editor", "", "🔤", () => ChangeEncoding("UTF-8 with BOM")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("editor.encodingUtf16Le", "Set Encoding: UTF-16 LE", "Editor", "", "🔤", () => ChangeEncoding("UTF-16 LE")));
        CommandPalette.RegisterCommand(new CommandPaletteItem("workspace.clearRecent", "Clear Recent Workspaces History", "File", "", "🗑️", () => _ = ClearRecentWorkspacesAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("workspace.refreshGitStatus", "Git: Refresh Status", "Git", "Ctrl+Shift+G", "🌿", () => _ = RefreshGitStatusAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("app.update", "Check for Code Viewer App Updates...", "Help", "", "🚀", () => _ = CheckForUpdatesAsync()));
        CommandPalette.RegisterCommand(new CommandPaletteItem("help.updates", "Check Language & Integration Updates...", "Help", "", "🔄", () => _ = CheckIntegrationsUpdateAsync()));
    }

    #endregion

    private void OnFileWatcherChangedOnDisk(object? sender, string path)
    {
        WorkspaceCoordinator.RequestDebouncedGitStatusRefresh();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _fileWatcherService.FileChangedOnDisk -= OnFileWatcherChangedOnDisk;
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        TabManager.Dispose();
        _fileWatcherService.Dispose();
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
