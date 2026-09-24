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

    public IReadOnlyList<ColorTheme> AvailableThemes => _themeService.AvailableThemes;

    public ObservableCollection<string> AvailableFonts { get; } =
    [
        "Cascadia Code",
        "Consolas",
        "Fira Code",
        "JetBrains Mono",
        "Courier New",
        "Lucida Console",
        "Segoe UI Variable",
        "Segoe UI",
        "Cascadia Code, Consolas, Courier New, monospace"
    ];

    public ObservableCollection<int> AvailableTabSizes { get; } = [2, 4, 8];

    public Action? RequestClose { get; set; }

    public bool IsSaved { get; private set; }

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;

        var current = _settingsService.CurrentSettings;
        _selectedFont = current.FontFamily;
        _fontSize = current.FontSize;
        _wordWrap = current.WordWrap;
        _showLineNumbers = current.ShowLineNumbers;
        _tabSize = current.TabSize;
        _maxRecentFiles = current.MaxRecentFiles;

        _selectedTheme = _themeService.AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, current.ThemeId, StringComparison.OrdinalIgnoreCase))
                         ?? _themeService.CurrentTheme;

        // If the current font isn't in our curated list, add it so it displays correctly
        if (!AvailableFonts.Contains(_selectedFont, StringComparer.OrdinalIgnoreCase))
        {
            AvailableFonts.Insert(0, _selectedFont);
        }
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
            MaxRecentFiles = MaxRecentFiles
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

        var defaultTheme = AvailableThemes.FirstOrDefault(t => t.Id == defaults.ThemeId) ?? AvailableThemes[0];
        SelectedTheme = defaultTheme;
    }
}
