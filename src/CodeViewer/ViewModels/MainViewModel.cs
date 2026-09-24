using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
public partial class MainViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private readonly ILanguageService _languageService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IDialogService _dialogService;
    private readonly IThemeService _themeService;
    private readonly IPluginService _pluginService;
    private readonly AppConfig _config;

    [ObservableProperty]
    private DocumentViewModel? _activeDocument;

    [ObservableProperty]
    private SearchViewModel _searchViewModel = new();

    [ObservableProperty]
    private ObservableCollection<DocumentViewModel> _documents = [];

    [ObservableProperty]
    private ObservableCollection<RecentFileItem> _recentFiles = [];

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public ColorTheme CurrentTheme => _themeService.CurrentTheme;

    public IReadOnlyList<IPlugin> Plugins => _pluginService.Plugins;
    public IEnumerable<string> PluginCategories => _pluginService.Plugins.Select(p => p.Category).Distinct().OrderBy(c => c);

    // Callbacks provided by MainWindow for active text editor interaction
    public Func<string>? RequestSelectedText { get; set; }
    public Action<string>? RequestReplaceSelection { get; set; }
    public Action<string>? RequestReplaceAll { get; set; }
    public Action<string>? RequestInsertText { get; set; }
    public Action? RequestOpenPluginManager { get; set; }

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
        IPluginService? pluginService = null)
    {
        _fileService = fileService;
        _languageService = languageService;
        _recentFilesService = recentFilesService;
        _dialogService = dialogService;
        _config = config;
        _themeService = themeService ?? new ThemeService();
        _pluginService = pluginService ?? new PluginService();

        _themeService.ThemeChanged += _ =>
        {
            OnPropertyChanged(nameof(CurrentTheme));
            OnPropertyChanged(nameof(AvailableThemes));
        };

        _pluginService.PluginsChanged += () =>
        {
            OnPropertyChanged(nameof(Plugins));
            OnPropertyChanged(nameof(PluginCategories));
        };
    }

    /// <summary>
    /// Initializes recent files, theme, and opens any command-line file arguments directly.
    /// </summary>
    public async Task InitializeAsync(string[]? commandLineArgs = null)
    {
        // 1. Apply configured or default theme
        _themeService.ApplyTheme(_themeService.CurrentTheme);

        // 2. Load Recent Files
        await LoadRecentFilesAsync();

        // 3. Handle command-line file arguments if provided (e.g. CodeViewer.exe myfile.cs)
        if (commandLineArgs != null && commandLineArgs.Length > 0)
        {
            foreach (var arg in commandLineArgs)
            {
                if (File.Exists(arg))
                {
                    await OpenFileInternalAsync(arg);
                }
            }
        }

        // 4. If no file opened, open a default empty document
        if (Documents.Count == 0)
        {
            CreateNewDocument();
        }
    }

    public async Task LoadRecentFilesAsync()
    {
        var items = await _recentFilesService.GetRecentFilesAsync();
        RecentFiles.Clear();
        foreach (var item in items)
        {
            RecentFiles.Add(item);
        }
    }

    [RelayCommand]
    public void CreateNewDocument()
    {
        var model = DocumentModel.CreateNew($"Untitled-{Documents.Count + 1}");
        var docVm = new DocumentViewModel(model, _languageService, _config.DefaultFontSize)
        {
            WordWrap = _config.WordWrap,
            ShowLineNumbers = _config.ShowLineNumbers
        };

        Documents.Add(docVm);
        ActiveDocument = docVm;
        UpdateActiveDocumentSelection();
        OnPropertyChanged(nameof(WindowTitle));
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
        if (!string.IsNullOrEmpty(filePath))
        {
            await OpenFileInternalAsync(filePath);
        }
    }

    public async Task OpenFileInternalAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(filePath);

        // Check if file is already open in a tab
        var existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveDocument = existing;
            UpdateActiveDocumentSelection();
            return;
        }

        try
        {
            var model = await _fileService.OpenFileAsync(fullPath);
            var docVm = new DocumentViewModel(model, _languageService, _config.DefaultFontSize)
            {
                WordWrap = _config.WordWrap,
                ShowLineNumbers = _config.ShowLineNumbers
            };

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
            await _recentFilesService.AddRecentFileAsync(fullPath);
            await LoadRecentFilesAsync();
            OnPropertyChanged(nameof(WindowTitle));
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
            ActiveDocument.SyncToModel();
            await _fileService.SaveFileAsync(ActiveDocument.Model);
            var fileInfo = new FileInfo(ActiveDocument.FilePath!);
            ActiveDocument.MarkSaved(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            OnPropertyChanged(nameof(WindowTitle));
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
            ActiveDocument.SyncToModel();
            await _fileService.SaveFileAsync(ActiveDocument.Model, targetPath);
            var fileInfo = new FileInfo(targetPath);
            ActiveDocument.MarkSaved(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTimeUtc);

            await _recentFilesService.AddRecentFileAsync(targetPath);
            await LoadRecentFilesAsync();
            OnPropertyChanged(nameof(WindowTitle));
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
                    await _fileService.SaveFileAsync(targetDoc.Model, targetPath);
                }
                else
                {
                    await _fileService.SaveFileAsync(targetDoc.Model);
                }
            }
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
    }

    [RelayCommand]
    public void ShowSearch()
    {
        SearchViewModel.Open();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        if (ActiveDocument != null && ActiveDocument.FontSize < _config.MaxFontSize)
        {
            ActiveDocument.FontSize = Math.Min(_config.MaxFontSize, ActiveDocument.FontSize + 1.5);
        }
    }

    [RelayCommand]
    public void ZoomOut()
    {
        if (ActiveDocument != null && ActiveDocument.FontSize > _config.MinFontSize)
        {
            ActiveDocument.FontSize = Math.Max(_config.MinFontSize, ActiveDocument.FontSize - 1.5);
        }
    }

    [RelayCommand]
    public void ResetZoom()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.FontSize = _config.DefaultFontSize;
        }
    }

    [RelayCommand]
    public void ToggleWordWrap()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.WordWrap = !ActiveDocument.WordWrap;
        }
    }

    [RelayCommand]
    public void ToggleLineNumbers()
    {
        if (ActiveDocument != null)
        {
            ActiveDocument.ShowLineNumbers = !ActiveDocument.ShowLineNumbers;
        }
    }

    #region Theme Operations

    [RelayCommand]
    public void SelectTheme(ColorTheme? theme)
    {
        if (theme != null)
        {
            _themeService.ApplyTheme(theme);
            OnPropertyChanged(nameof(CurrentTheme));
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
    }
}
