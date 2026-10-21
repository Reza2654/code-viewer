using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IAppUpdateService? _appUpdateService;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private ColorTheme _selectedTheme;

    [ObservableProperty]
    private string _themeMode;

    [ObservableProperty]
    private string _contrastMode;

    // Light Theme options
    [ObservableProperty]
    private ColorTheme _selectedLightPreset;

    [ObservableProperty]
    private string _lightBackgroundHex;

    [ObservableProperty]
    private string _lightForegroundHex;

    [ObservableProperty]
    private string _lightAccentHex;

    [ObservableProperty]
    private IBrush _lightBackgroundBrush;

    [ObservableProperty]
    private IBrush _lightForegroundBrush;

    [ObservableProperty]
    private IBrush _lightAccentBrush;

    // Dark Theme options
    [ObservableProperty]
    private ColorTheme _selectedDarkPreset;

    [ObservableProperty]
    private string _darkBackgroundHex;

    [ObservableProperty]
    private string _darkForegroundHex;

    [ObservableProperty]
    private string _darkAccentHex;

    [ObservableProperty]
    private IBrush _darkBackgroundBrush;

    [ObservableProperty]
    private IBrush _darkForegroundBrush;

    [ObservableProperty]
    private IBrush _darkAccentBrush;

    [ObservableProperty]
    private bool _highlightWordOccurrences = true;

    // Editor Preferences
    [ObservableProperty]
    private bool _smoothCursorBlink = true;

    [ObservableProperty]
    private bool _showWhitespace = false;

    [ObservableProperty]
    private bool _highlightCurrentLine = true;

    [ObservableProperty]
    private bool _enableBracketMatching = true;

    // Fonts & Editor
    [ObservableProperty]
    private string _selectedFont;

    public FontFamily SelectedFontFamily => FontHelper.GetFontFamily(SelectedFont);

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private bool _wordWrap;

    [ObservableProperty]
    private bool _showLineNumbers;

    [ObservableProperty]
    private int _tabSize;

    [ObservableProperty]
    private int _maxRecentFiles;

    [ObservableProperty]
    private bool _restorePreviousSession;

    [ObservableProperty]
    private bool _smoothScrolling;

    [ObservableProperty]
    private bool _autoCheckForUpdates = true;

    [ObservableProperty]
    private bool _runInBackgroundOnStartup;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    // Syntax Highlighting Customizer Overrides
    [ObservableProperty]
    private string? _customCodeKeyword;

    [ObservableProperty]
    private string? _customCodeComment;

    [ObservableProperty]
    private string? _customCodeString;

    [ObservableProperty]
    private string? _customCodeNumber;

    [ObservableProperty]
    private string? _customCodeType;

    [ObservableProperty]
    private string? _customCodeMethod;

    [ObservableProperty]
    private string? _customCodePreprocessor;

    [ObservableProperty]
    private string? _customCodePunctuation;

    [ObservableProperty]
    private IBrush _keywordBrush = new SolidColorBrush(Colors.DodgerBlue);

    [ObservableProperty]
    private IBrush _commentBrush = new SolidColorBrush(Colors.Green);

    [ObservableProperty]
    private IBrush _stringBrush = new SolidColorBrush(Colors.Orange);

    [ObservableProperty]
    private IBrush _numberBrush = new SolidColorBrush(Colors.LightGreen);

    [ObservableProperty]
    private IBrush _typeBrush = new SolidColorBrush(Colors.Teal);

    [ObservableProperty]
    private IBrush _methodBrush = new SolidColorBrush(Colors.Yellow);

    [ObservableProperty]
    private IBrush _preprocessorBrush = new SolidColorBrush(Colors.Magenta);

    [ObservableProperty]
    private IBrush _punctuationBrush = new SolidColorBrush(Colors.White);

    // Theme Gallery
    [ObservableProperty]
    private string _themeFilterMode = "All";

    public ObservableCollection<ColorTheme> FilteredThemes { get; } = [];

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public IReadOnlyList<ColorTheme> AvailableLightThemes { get; }
    public IReadOnlyList<ColorTheme> AvailableDarkThemes { get; }

    public ObservableCollection<string> AvailableFonts { get; } = [];
    public ObservableCollection<int> AvailableTabSizes { get; } = [2, 4, 8];

    public bool IsAllThemeFilter => string.Equals(ThemeFilterMode, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsDarkThemeFilter => string.Equals(ThemeFilterMode, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsLightThemeFilter => string.Equals(ThemeFilterMode, "Light", StringComparison.OrdinalIgnoreCase);

    public bool IsSystemTheme => string.Equals(ThemeMode, "System", StringComparison.OrdinalIgnoreCase);
    public bool IsLightTheme => string.Equals(ThemeMode, "Light", StringComparison.OrdinalIgnoreCase);
    public bool IsDarkTheme => string.Equals(ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase);

    public bool IsDefaultContrast => string.Equals(ContrastMode, "Default", StringComparison.OrdinalIgnoreCase);
    public bool IsStrongContrast => string.Equals(ContrastMode, "Strong", StringComparison.OrdinalIgnoreCase);

    public Action? RequestClose { get; set; }
    public bool IsSaved { get; private set; }

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IAppUpdateService? appUpdateService = null,
        IDialogService? dialogService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _appUpdateService = appUpdateService;
        _dialogService = dialogService;

        var current = _settingsService.CurrentSettings;
        _selectedFont = AppSettings.SanitizeFontFamily(current.FontFamily);
        _fontSize = current.FontSize;
        _wordWrap = current.WordWrap;
        _showLineNumbers = current.ShowLineNumbers;
        _tabSize = current.TabSize;
        _maxRecentFiles = current.MaxRecentFiles;
        _restorePreviousSession = current.RestorePreviousSession;
        _highlightWordOccurrences = current.HighlightWordOccurrences;
        _smoothScrolling = current.SmoothScrolling;
        _smoothCursorBlink = current.SmoothCursorBlink;
        _showWhitespace = current.ShowWhitespace;
        _highlightCurrentLine = current.HighlightCurrentLine;
        _enableBracketMatching = current.EnableBracketMatching;
        _autoCheckForUpdates = current.AutoCheckForUpdates;
        _runInBackgroundOnStartup = current.RunInBackgroundOnStartup;
        _minimizeToTrayOnClose = current.MinimizeToTrayOnClose;

        _customCodeKeyword = current.CustomCodeKeyword;
        _customCodeComment = current.CustomCodeComment;
        _customCodeString = current.CustomCodeString;
        _customCodeNumber = current.CustomCodeNumber;
        _customCodeType = current.CustomCodeType;
        _customCodeMethod = current.CustomCodeMethod;
        _customCodePreprocessor = current.CustomCodePreprocessor;
        _customCodePunctuation = current.CustomCodePunctuation;

        _themeMode = !string.IsNullOrWhiteSpace(current.ThemeMode) ? current.ThemeMode : "System";
        _contrastMode = !string.IsNullOrWhiteSpace(current.ContrastMode) ? current.ContrastMode : "Default";

        AvailableLightThemes = _themeService.AvailableThemes.Where(t => !t.IsDark).ToList();
        AvailableDarkThemes = _themeService.AvailableThemes.Where(t => t.IsDark).ToList();

        _selectedLightPreset = AvailableLightThemes.FirstOrDefault(t => string.Equals(t.Id, current.LightPresetId, StringComparison.OrdinalIgnoreCase))
                               ?? AvailableLightThemes.FirstOrDefault()
                               ?? _themeService.AvailableThemes[0];

        _lightBackgroundHex = !string.IsNullOrWhiteSpace(current.LightBackgroundHex) ? current.LightBackgroundHex : "#F9F9F9";
        _lightForegroundHex = !string.IsNullOrWhiteSpace(current.LightForegroundHex) ? current.LightForegroundHex : "#101010";
        _lightAccentHex = !string.IsNullOrWhiteSpace(current.LightAccentHex) ? current.LightAccentHex : "#007ACC";

        _selectedDarkPreset = AvailableDarkThemes.FirstOrDefault(t => string.Equals(t.Id, current.DarkPresetId, StringComparison.OrdinalIgnoreCase))
                              ?? AvailableDarkThemes.FirstOrDefault()
                              ?? _themeService.AvailableThemes[0];

        _darkBackgroundHex = !string.IsNullOrWhiteSpace(current.DarkBackgroundHex) ? current.DarkBackgroundHex : "#101010";
        _darkForegroundHex = !string.IsNullOrWhiteSpace(current.DarkForegroundHex) ? current.DarkForegroundHex : "#CCCCCC";
        _darkAccentHex = !string.IsNullOrWhiteSpace(current.DarkAccentHex) ? current.DarkAccentHex : "#007ACC";

        _lightBackgroundBrush = TryParseBrush(_lightBackgroundHex, "#F9F9F9");
        _lightForegroundBrush = TryParseBrush(_lightForegroundHex, "#101010");
        _lightAccentBrush = TryParseBrush(_lightAccentHex, "#007ACC");

        _darkBackgroundBrush = TryParseBrush(_darkBackgroundHex, "#101010");
        _darkForegroundBrush = TryParseBrush(_darkForegroundHex, "#CCCCCC");
        _darkAccentBrush = TryParseBrush(_darkAccentHex, "#007ACC");

        _selectedTheme = _themeService.AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, current.ThemeId, StringComparison.OrdinalIgnoreCase))
                         ?? _themeService.CurrentTheme;

        // Discover and populate actual installed coding & monospace fonts
        var discoveredFonts = DiscoverSystemFonts();
        foreach (var font in discoveredFonts)
        {
            AvailableFonts.Add(font);
        }

        // Ensure current selected font is in the list
        if (!AvailableFonts.Contains(_selectedFont, StringComparer.OrdinalIgnoreCase))
        {
            AvailableFonts.Insert(0, _selectedFont);
        }

        UpdateFilteredThemes();
        UpdateSyntaxBrushes();
        UpdateEffectivePreviewTheme();
    }

    private bool _isUpdatingTheme;

    partial void OnSelectedThemeChanged(ColorTheme value)
    {
        if (value == null || _isUpdatingTheme) return;

        try
        {
            _isUpdatingTheme = true;
            if (value.IsDark)
            {
                ThemeMode = "Dark";
                var match = AvailableDarkThemes.FirstOrDefault(t => t.Id == value.Id);
                if (match != null)
                {
                    SelectedDarkPreset = match;
                }
            }
            else
            {
                ThemeMode = "Light";
                var match = AvailableLightThemes.FirstOrDefault(t => t.Id == value.Id);
                if (match != null)
                {
                    SelectedLightPreset = match;
                }
            }
        }
        finally
        {
            _isUpdatingTheme = false;
        }
    }

    private void UpdateEffectivePreviewTheme()
    {
        if (_isUpdatingTheme) return;

        try
        {
            _isUpdatingTheme = true;
            var temp = new AppSettings
            {
                ThemeId = string.Equals(ThemeMode, "Light", StringComparison.OrdinalIgnoreCase)
                    ? (SelectedLightPreset?.Id ?? "default-light")
                    : (SelectedDarkPreset?.Id ?? "default-dark"),
                ThemeMode = ThemeMode,
                ContrastMode = ContrastMode,
                LightPresetId = SelectedLightPreset?.Id ?? "default-light",
                LightBackgroundHex = NormalizeHex(LightBackgroundHex, "#F9F9F9"),
                LightForegroundHex = NormalizeHex(LightForegroundHex, "#101010"),
                LightAccentHex = NormalizeHex(LightAccentHex, "#007ACC"),
                DarkPresetId = SelectedDarkPreset?.Id ?? "default-dark",
                DarkBackgroundHex = NormalizeHex(DarkBackgroundHex, "#101010"),
                DarkForegroundHex = NormalizeHex(DarkForegroundHex, "#CCCCCC"),
                DarkAccentHex = NormalizeHex(DarkAccentHex, "#007ACC"),
                CustomCodeKeyword = CustomCodeKeyword,
                CustomCodeComment = CustomCodeComment,
                CustomCodeString = CustomCodeString,
                CustomCodeNumber = CustomCodeNumber,
                CustomCodeType = CustomCodeType,
                CustomCodeMethod = CustomCodeMethod,
                CustomCodePreprocessor = CustomCodePreprocessor,
                CustomCodePunctuation = CustomCodePunctuation,
            };
            SelectedTheme = _themeService.ResolveEffectiveTheme(temp);
            UpdateSyntaxBrushes();
        }
        catch
        {
            // Graceful fallback
        }
        finally
        {
            _isUpdatingTheme = false;
        }
    }

    partial void OnThemeModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        UpdateEffectivePreviewTheme();
    }

    partial void OnContrastModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDefaultContrast));
        OnPropertyChanged(nameof(IsStrongContrast));
        UpdateEffectivePreviewTheme();
    }

    partial void OnSelectedLightPresetChanged(ColorTheme value)
    {
        if (value != null)
        {
            LightBackgroundHex = value.WindowBackground;
            LightForegroundHex = value.Foreground;
            LightAccentHex = value.AccentColor;
            UpdateEffectivePreviewTheme();
        }
    }

    partial void OnSelectedDarkPresetChanged(ColorTheme value)
    {
        if (value != null)
        {
            DarkBackgroundHex = value.WindowBackground;
            DarkForegroundHex = value.Foreground;
            DarkAccentHex = value.AccentColor;
            UpdateEffectivePreviewTheme();
        }
    }

    partial void OnLightBackgroundHexChanged(string value)
    {
        LightBackgroundBrush = TryParseBrush(value, "#F9F9F9");
        UpdateEffectivePreviewTheme();
    }

    partial void OnLightForegroundHexChanged(string value)
    {
        LightForegroundBrush = TryParseBrush(value, "#101010");
        UpdateEffectivePreviewTheme();
    }

    partial void OnLightAccentHexChanged(string value)
    {
        LightAccentBrush = TryParseBrush(value, "#007ACC");
        UpdateEffectivePreviewTheme();
    }

    partial void OnDarkBackgroundHexChanged(string value)
    {
        DarkBackgroundBrush = TryParseBrush(value, "#101010");
        UpdateEffectivePreviewTheme();
    }

    partial void OnDarkForegroundHexChanged(string value)
    {
        DarkForegroundBrush = TryParseBrush(value, "#CCCCCC");
        UpdateEffectivePreviewTheme();
    }

    partial void OnDarkAccentHexChanged(string value)
    {
        DarkAccentBrush = TryParseBrush(value, "#007ACC");
        UpdateEffectivePreviewTheme();
    }

    partial void OnSelectedFontChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedFontFamily));
    }

    partial void OnThemeFilterModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllThemeFilter));
        OnPropertyChanged(nameof(IsDarkThemeFilter));
        OnPropertyChanged(nameof(IsLightThemeFilter));
        UpdateFilteredThemes();
    }

    partial void OnCustomCodeKeywordChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodeCommentChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodeStringChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodeNumberChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodeTypeChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodeMethodChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodePreprocessorChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }
    partial void OnCustomCodePunctuationChanged(string? value) { UpdateSyntaxBrushes(); UpdateEffectivePreviewTheme(); }

    private void UpdateSyntaxBrushes()
    {
        var theme = SelectedTheme ?? _themeService.CurrentTheme;
        KeywordBrush = TryParseBrush(CustomCodeKeyword, theme?.CodeKeyword ?? "#569CD6");
        CommentBrush = TryParseBrush(CustomCodeComment, theme?.CodeComment ?? "#6A9955");
        StringBrush = TryParseBrush(CustomCodeString, theme?.CodeString ?? "#CE9178");
        NumberBrush = TryParseBrush(CustomCodeNumber, theme?.CodeNumber ?? "#B5CEA8");
        TypeBrush = TryParseBrush(CustomCodeType, theme?.CodeType ?? "#4EC9B0");
        MethodBrush = TryParseBrush(CustomCodeMethod, theme?.CodeMethod ?? "#DCDCAA");
        PreprocessorBrush = TryParseBrush(CustomCodePreprocessor, theme?.CodePreprocessor ?? "#9B9B9B");
        PunctuationBrush = TryParseBrush(CustomCodePunctuation, theme?.CodePunctuation ?? "#D4D4D4");
    }

    private void UpdateFilteredThemes()
    {
        FilteredThemes.Clear();
        var all = _themeService.AvailableThemes;
        IEnumerable<ColorTheme> query = all;
        if (string.Equals(ThemeFilterMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            query = all.Where(t => t.IsDark);
        }
        else if (string.Equals(ThemeFilterMode, "Light", StringComparison.OrdinalIgnoreCase))
        {
            query = all.Where(t => !t.IsDark);
        }

        foreach (var t in query)
        {
            FilteredThemes.Add(t);
        }
    }

    [RelayCommand]
    public void SetThemeFilter(string filter)
    {
        ThemeFilterMode = filter;
    }

    [RelayCommand]
    public void SelectThemeFromGallery(ColorTheme theme)
    {
        if (theme == null) return;
        SelectedTheme = theme;
        if (theme.IsDark)
        {
            ThemeMode = "Dark";
            var match = AvailableDarkThemes.FirstOrDefault(t => t.Id == theme.Id);
            if (match != null) SelectedDarkPreset = match;
            DarkBackgroundHex = theme.WindowBackground;
            DarkForegroundHex = theme.Foreground;
            DarkAccentHex = theme.AccentColor;
        }
        else
        {
            ThemeMode = "Light";
            var match = AvailableLightThemes.FirstOrDefault(t => t.Id == theme.Id);
            if (match != null) SelectedLightPreset = match;
            LightBackgroundHex = theme.WindowBackground;
            LightForegroundHex = theme.Foreground;
            LightAccentHex = theme.AccentColor;
        }
        UpdateSyntaxBrushes();
        UpdateEffectivePreviewTheme();
    }

    [RelayCommand]
    public void ResetSyntaxColors()
    {
        CustomCodeKeyword = null;
        CustomCodeComment = null;
        CustomCodeString = null;
        CustomCodeNumber = null;
        CustomCodeType = null;
        CustomCodeMethod = null;
        CustomCodePreprocessor = null;
        CustomCodePunctuation = null;
        UpdateSyntaxBrushes();
        UpdateEffectivePreviewTheme();
    }

    [RelayCommand]
    public async Task ImportThemeAsync()
    {
        if (_dialogService == null) return;
        try
        {
            var file = await _dialogService.ShowOpenSpecificFileDialogAsync("Import Color Theme", "JSON Theme (*.json)", new[] { "*.json" });
            if (!string.IsNullOrEmpty(file))
            {
                var imported = await _themeService.ImportThemeFromJsonAsync(file);
                UpdateFilteredThemes();
                SelectThemeFromGallery(imported);
                await _dialogService.ShowMessageAsync("Theme Imported", $"Theme '{imported.Name}' was successfully imported and activated!");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Import Error", $"Could not import theme: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenThemesFolder()
    {
        try
        {
            var dir = _themeService.GetThemesDirectory();
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    public void SetFontSize(double size)
    {
        FontSize = size;
    }

    [RelayCommand]
    public void IncreaseFontSize()
    {
        if (FontSize < 36) FontSize += 1;
    }

    [RelayCommand]
    public void DecreaseFontSize()
    {
        if (FontSize > 8) FontSize -= 1;
    }

    [RelayCommand]
    public void SetTabSize(int size)
    {
        TabSize = size;
    }

    [RelayCommand]
    public void SetThemeMode(string mode)
    {
        ThemeMode = mode;
        UpdateEffectivePreviewTheme();
    }

    [RelayCommand]
    public void SetContrastMode(string mode)
    {
        ContrastMode = mode;
        UpdateEffectivePreviewTheme();
    }

    private static IBrush TryParseBrush(string? hex, string fallbackHex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var clean = hex.Trim().TrimStart('#');
                if (clean.Length == 3)
                {
                    clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
                }
                var withHash = "#" + clean;
                if (Color.TryParse(withHash, out var color))
                {
                    return new SolidColorBrush(color);
                }
            }
        }
        catch { }
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static string NormalizeHex(string? hex, string fallbackHex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallbackHex;
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
        }
        var withHash = "#" + clean;
        return Color.TryParse(withHash, out _) ? withHash : fallbackHex;
    }

    private static List<string> DiscoverSystemFonts()
    {
        var codingFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google Sans Code",
            "Roboto Mono",
            "Cascadia Code",
            "Cascadia Mono",
            "Consolas",
            "Courier New",
            "Lucida Console",
            "JetBrains Mono",
            "Fira Code",
            "Source Code Pro",
            "Inconsolata",
            "Hack",
            "Segoe UI Variable Text",
            "Segoe UI"
        };

        var result = new List<string>();

        // Always include Google Sans Code and Roboto Mono as the built-in primary editor fonts
        result.Add("Google Sans Code");
        result.Add("Roboto Mono");

        try
        {
            var systemFonts = Avalonia.Media.FontManager.Current.SystemFonts;
            if (systemFonts != null)
            {
                foreach (var font in codingFonts)
                {
                    if (!result.Contains(font, StringComparer.OrdinalIgnoreCase) &&
                        systemFonts.Any(f => string.Equals(f.Name, font, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(font);
                    }
                }

                foreach (var f in systemFonts.OrderBy(f => f.Name))
                {
                    if (!result.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var lower = f.Name.ToLowerInvariant();
                        if (lower.Contains("mono") || lower.Contains("code") || lower.Contains("console"))
                        {
                            result.Add(f.Name);
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback for headless / test environments
        }

        if (result.Count <= 1)
        {
            result.AddRange(["Cascadia Code", "Consolas", "Courier New", "Lucida Console", "Segoe UI"]);
        }

        return result;
    }


    [RelayCommand]
    public async Task SaveAsync()
    {
        UpdateEffectivePreviewTheme();

        var settings = new AppSettings
        {
            ThemeId = SelectedTheme?.Id ?? (string.Equals(ThemeMode, "Light", StringComparison.OrdinalIgnoreCase) ? (SelectedLightPreset?.Id ?? "default-light") : (SelectedDarkPreset?.Id ?? "default-dark")),
            ThemeMode = ThemeMode,
            ContrastMode = ContrastMode,
            LightPresetId = SelectedLightPreset?.Id ?? "default-light",
            LightBackgroundHex = NormalizeHex(LightBackgroundHex, "#F9F9F9"),
            LightForegroundHex = NormalizeHex(LightForegroundHex, "#101010"),
            LightAccentHex = NormalizeHex(LightAccentHex, "#007ACC"),
            DarkPresetId = SelectedDarkPreset?.Id ?? "default-dark",
            DarkBackgroundHex = NormalizeHex(DarkBackgroundHex, "#101010"),
            DarkForegroundHex = NormalizeHex(DarkForegroundHex, "#CCCCCC"),
            DarkAccentHex = NormalizeHex(DarkAccentHex, "#007ACC"),
            FontFamily = SelectedFont,
            FontSize = FontSize,
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            TabSize = TabSize,
            MaxRecentFiles = MaxRecentFiles,
            RestorePreviousSession = RestorePreviousSession,
            HighlightWordOccurrences = HighlightWordOccurrences,
            SmoothScrolling = SmoothScrolling,
            SmoothCursorBlink = SmoothCursorBlink,
            ShowWhitespace = ShowWhitespace,
            HighlightCurrentLine = HighlightCurrentLine,
            EnableBracketMatching = EnableBracketMatching,
            CustomCodeKeyword = CustomCodeKeyword,
            CustomCodeComment = CustomCodeComment,
            CustomCodeString = CustomCodeString,
            CustomCodeNumber = CustomCodeNumber,
            CustomCodeType = CustomCodeType,
            CustomCodeMethod = CustomCodeMethod,
            CustomCodePreprocessor = CustomCodePreprocessor,
            CustomCodePunctuation = CustomCodePunctuation,
            AutoCheckForUpdates = AutoCheckForUpdates,
            RunInBackgroundOnStartup = RunInBackgroundOnStartup,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose
        };

        StartupRegistrationService.SetRunOnStartup(RunInBackgroundOnStartup);

        await _settingsService.SaveAsync(settings);
        _themeService.ApplyTheme(settings);

        IsSaved = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void SelectPrimaryFont(string fontName)
    {
        SelectedFont = AppSettings.SanitizeFontFamily(fontName);
    }

    [RelayCommand]
    public async Task CheckForUpdatesNowAsync()
    {
        if (_appUpdateService == null || _dialogService == null) return;
        try
        {
            var info = await _appUpdateService.CheckForUpdatesAsync(isManualCheck: true);
            if (info.IsUpdateAvailable)
            {
                await _dialogService.ShowUpdateDialogAsync(info, _appUpdateService);
            }
            else
            {
                await _dialogService.ShowMessageAsync("Check for Updates", $"You are running the latest version of Code Viewer ({info.CurrentVersion}).");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Update Error", $"Could not check for updates:\n{ex.Message}");
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        IsSaved = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        var defaults = _settingsService.GetDefaultSettings();
        SelectedFont = defaults.FontFamily;
        FontSize = defaults.FontSize;
        WordWrap = defaults.WordWrap;
        ShowLineNumbers = defaults.ShowLineNumbers;
        TabSize = defaults.TabSize;
        MaxRecentFiles = defaults.MaxRecentFiles;
        RestorePreviousSession = defaults.RestorePreviousSession;
        HighlightWordOccurrences = defaults.HighlightWordOccurrences;
        SmoothScrolling = defaults.SmoothScrolling;
        SmoothCursorBlink = defaults.SmoothCursorBlink;
        ShowWhitespace = defaults.ShowWhitespace;
        HighlightCurrentLine = defaults.HighlightCurrentLine;
        EnableBracketMatching = defaults.EnableBracketMatching;
        CustomCodeKeyword = defaults.CustomCodeKeyword;
        CustomCodeComment = defaults.CustomCodeComment;
        CustomCodeString = defaults.CustomCodeString;
        CustomCodeNumber = defaults.CustomCodeNumber;
        CustomCodeType = defaults.CustomCodeType;
        CustomCodeMethod = defaults.CustomCodeMethod;
        CustomCodePreprocessor = defaults.CustomCodePreprocessor;
        CustomCodePunctuation = defaults.CustomCodePunctuation;
        AutoCheckForUpdates = defaults.AutoCheckForUpdates;
        RunInBackgroundOnStartup = defaults.RunInBackgroundOnStartup;
        MinimizeToTrayOnClose = defaults.MinimizeToTrayOnClose;

        ThemeMode = defaults.ThemeMode;
        ContrastMode = defaults.ContrastMode;

        SelectedLightPreset = AvailableLightThemes.FirstOrDefault(t => string.Equals(t.Id, defaults.LightPresetId, StringComparison.OrdinalIgnoreCase))
                              ?? AvailableLightThemes.FirstOrDefault()!;
        LightBackgroundHex = defaults.LightBackgroundHex;
        LightForegroundHex = defaults.LightForegroundHex;
        LightAccentHex = defaults.LightAccentHex;

        SelectedDarkPreset = AvailableDarkThemes.FirstOrDefault(t => string.Equals(t.Id, defaults.DarkPresetId, StringComparison.OrdinalIgnoreCase))
                             ?? AvailableDarkThemes.FirstOrDefault()!;
        DarkBackgroundHex = defaults.DarkBackgroundHex;
        DarkForegroundHex = defaults.DarkForegroundHex;
        DarkAccentHex = defaults.DarkAccentHex;

        var defaultTheme = AvailableThemes.FirstOrDefault(t => t.Id == defaults.ThemeId) ?? AvailableThemes[0];
        SelectedTheme = defaultTheme;
        UpdateEffectivePreviewTheme();
    }
}
