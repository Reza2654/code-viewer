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

    // Fonts & Editor
    [ObservableProperty]
    private string _selectedFont;

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

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;
    public IReadOnlyList<ColorTheme> AvailableLightThemes { get; }
    public IReadOnlyList<ColorTheme> AvailableDarkThemes { get; }

    public ObservableCollection<string> AvailableFonts { get; } = [];
    public ObservableCollection<int> AvailableTabSizes { get; } = [2, 4, 8];

    public bool IsSystemTheme => string.Equals(ThemeMode, "System", StringComparison.OrdinalIgnoreCase);
    public bool IsLightTheme => string.Equals(ThemeMode, "Light", StringComparison.OrdinalIgnoreCase);
    public bool IsDarkTheme => string.Equals(ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase);

    public bool IsDefaultContrast => string.Equals(ContrastMode, "Default", StringComparison.OrdinalIgnoreCase);
    public bool IsStrongContrast => string.Equals(ContrastMode, "Strong", StringComparison.OrdinalIgnoreCase);

    public Action? RequestClose { get; set; }
    public bool IsSaved { get; private set; }

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;

        var current = _settingsService.CurrentSettings;
        _selectedFont = AppSettings.SanitizeFontFamily(current.FontFamily);
        _fontSize = current.FontSize;
        _wordWrap = current.WordWrap;
        _showLineNumbers = current.ShowLineNumbers;
        _tabSize = current.TabSize;
        _maxRecentFiles = current.MaxRecentFiles;
        _restorePreviousSession = current.RestorePreviousSession;

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
    }

    partial void OnThemeModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    partial void OnContrastModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDefaultContrast));
        OnPropertyChanged(nameof(IsStrongContrast));
    }

    partial void OnSelectedLightPresetChanged(ColorTheme value)
    {
        if (value != null)
        {
            LightBackgroundHex = value.WindowBackground;
            LightForegroundHex = value.Foreground;
            LightAccentHex = value.AccentColor;
        }
    }

    partial void OnSelectedDarkPresetChanged(ColorTheme value)
    {
        if (value != null)
        {
            DarkBackgroundHex = value.WindowBackground;
            DarkForegroundHex = value.Foreground;
            DarkAccentHex = value.AccentColor;
        }
    }

    partial void OnLightBackgroundHexChanged(string value)
    {
        LightBackgroundBrush = TryParseBrush(value, "#F9F9F9");
    }

    partial void OnLightForegroundHexChanged(string value)
    {
        LightForegroundBrush = TryParseBrush(value, "#101010");
    }

    partial void OnLightAccentHexChanged(string value)
    {
        LightAccentBrush = TryParseBrush(value, "#007ACC");
    }

    partial void OnDarkBackgroundHexChanged(string value)
    {
        DarkBackgroundBrush = TryParseBrush(value, "#101010");
    }

    partial void OnDarkForegroundHexChanged(string value)
    {
        DarkForegroundBrush = TryParseBrush(value, "#CCCCCC");
    }

    partial void OnDarkAccentHexChanged(string value)
    {
        DarkAccentBrush = TryParseBrush(value, "#007ACC");
    }

    [RelayCommand]
    public void SetThemeMode(string mode)
    {
        ThemeMode = mode;
    }

    [RelayCommand]
    public void SetContrastMode(string mode)
    {
        ContrastMode = mode;
    }

    private static IBrush TryParseBrush(string? hex, string fallbackHex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var clean = hex.Trim();
                if (!clean.StartsWith('#')) clean = "#" + clean;
                if (Color.TryParse(clean, out var color))
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
        var clean = hex.Trim();
        if (!clean.StartsWith('#')) clean = "#" + clean;
        return Color.TryParse(clean, out _) ? clean : fallbackHex;
    }

    private static List<string> DiscoverSystemFonts()
    {
        var codingFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
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

        try
        {
            var systemFonts = Avalonia.Media.FontManager.Current.SystemFonts;
            if (systemFonts != null)
            {
                foreach (var font in codingFonts)
                {
                    if (systemFonts.Any(f => string.Equals(f.Name, font, StringComparison.OrdinalIgnoreCase)))
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

        if (result.Count == 0)
        {
            result.AddRange(["Cascadia Code", "Consolas", "Courier New", "Lucida Console", "Segoe UI"]);
        }

        return result;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
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
            RestorePreviousSession = RestorePreviousSession
        };

        await _settingsService.SaveAsync(settings);
        _themeService.ApplyTheme(settings);

        IsSaved = true;
        RequestClose?.Invoke();
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
    }
}
