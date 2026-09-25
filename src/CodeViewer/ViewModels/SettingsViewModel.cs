using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    public ObservableCollection<string> AvailableFonts { get; } = [];

    public ObservableCollection<int> AvailableTabSizes { get; } = [2, 4, 8];

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
                // First, add all recognized programming fonts that are actually installed on the system
                foreach (var font in codingFonts)
                {
                    if (systemFonts.Any(f => string.Equals(f.Name, font, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(font);
                    }
                }

                // Next, add any other installed fonts containing Mono/Code/Console
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
            ThemeId = SelectedTheme.Id,
            FontFamily = SelectedFont,
            FontSize = FontSize,
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            TabSize = TabSize,
            MaxRecentFiles = MaxRecentFiles,
            RestorePreviousSession = RestorePreviousSession
        };

        await _settingsService.SaveAsync(settings);
        _themeService.ApplyTheme(SelectedTheme);

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

        var defaultTheme = AvailableThemes.FirstOrDefault(t => t.Id == defaults.ThemeId) ?? AvailableThemes[0];
        SelectedTheme = defaultTheme;
    }
}
