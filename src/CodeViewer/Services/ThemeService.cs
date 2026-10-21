using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using CodeViewer.Models;

namespace CodeViewer.Services;

public class ThemeService : IThemeService
{
    private readonly string _themesDirectory;
    private readonly string _settingsFilePath;
    private readonly List<ColorTheme> _themes = [];
    private ColorTheme _currentTheme;

    public IReadOnlyList<ColorTheme> AvailableThemes
    {
        get
        {
            lock (_themes)
            {
                return _themes.ToList().AsReadOnly();
            }
        }
    }
    public ColorTheme CurrentTheme => _currentTheme;
    public event Action<ColorTheme>? ThemeChanged;
    public Task InitializationTask { get; }

    public ThemeService(string? baseDir = null)
    {
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            baseDir = Path.Combine(appData, "CodeViewer");
        }
        _themesDirectory = Path.Combine(baseDir, "Themes");
        _settingsFilePath = Path.Combine(baseDir, "current_theme.txt");

        RegisterBuiltInThemes();

        // Instant in-memory resolution of saved theme
        var savedTheme = LoadSavedThemeName();
        ColorTheme? initialTheme = null;
        lock (_themes)
        {
            initialTheme = _themes.FirstOrDefault(t => string.Equals(t.Id, savedTheme, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(t.Name, savedTheme, StringComparison.OrdinalIgnoreCase));
        }

        // If saved theme is not built-in, load only that single theme file synchronously
        if (initialTheme == null && !string.IsNullOrWhiteSpace(savedTheme))
        {
            initialTheme = LoadSingleUserTheme(savedTheme);
            if (initialTheme != null)
            {
                lock (_themes)
                {
                    _themes.Add(initialTheme);
                }
            }
        }

        _currentTheme = initialTheme ?? _themes[0];

        // Defer directory creation, sample file generation, and full scanning to background
        InitializationTask = Task.Run(() =>
        {
            EnsureDirectoriesAndSamples();
            LoadUserThemes();
        });
    }

    private ColorTheme? LoadSingleUserTheme(string themeId)
    {
        try
        {
            var path = Path.Combine(_themesDirectory, $"{themeId}.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var theme = JsonSerializer.Deserialize<ColorTheme>(json);
                if (theme != null)
                {
                    if (string.IsNullOrWhiteSpace(theme.Id)) theme.Id = themeId;
                    if (string.IsNullOrWhiteSpace(theme.Name)) theme.Name = Path.GetFileNameWithoutExtension(path);
                    return theme;
                }
            }

            // Fallback scan: if filename does not strictly equal themeId, check all json files in _themesDirectory
            if (Directory.Exists(_themesDirectory))
            {
                foreach (var file in Directory.GetFiles(_themesDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var theme = JsonSerializer.Deserialize<ColorTheme>(json);
                        if (theme != null)
                        {
                            var autoId = string.IsNullOrWhiteSpace(theme.Id)
                                ? Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace(' ', '-')
                                : theme.Id;
                            var autoName = string.IsNullOrWhiteSpace(theme.Name)
                                ? Path.GetFileNameWithoutExtension(file)
                                : theme.Name;

                            if (string.Equals(autoId, themeId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(autoName, themeId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Path.GetFileNameWithoutExtension(file), themeId, StringComparison.OrdinalIgnoreCase))
                            {
                                theme.Id = autoId;
                                theme.Name = autoName;
                                return theme;
                            }
                        }
                    }
                    catch
                    {
                        // continue scanning
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private void RegisterBuiltInThemes()
    {
        _themes.Add(new ColorTheme
        {
            Id = "default-dark",
            Name = "Default Dark",
            IsDark = true,
            WindowBackground = "#101010",
            Foreground = "#CCCCCC",
            MenuBackground = "#161616",
            MenuForeground = "#CCCCCC",
            TabBarBackground = "#161616",
            TabItemActiveBackground = "#101010",
            TabItemInactiveBackground = "#202020",
            TabItemActiveForeground = "#FFFFFF",
            TabItemInactiveForeground = "#888888",
            EditorBackground = "#101010",
            EditorForeground = "#CCCCCC",
            LineNumbersForeground = "#555555",
            SelectionBackground = "#264F78",
            StatusBarBackground = "#007ACC",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#007ACC",
            BorderColor = "#252525",
            CodeKeyword = "#569CD6",
            CodeComment = "#6A9955",
            CodeString = "#CE9178",
            CodeNumber = "#B5CEA8",
            CodeType = "#4EC9B0",
            CodeMethod = "#DCDCAA",
            CodePreprocessor = "#9B9B9B",
            CodePunctuation = "#CCCCCC"
        });

        _themes.Add(new ColorTheme
        {
            Id = "default-light",
            Name = "Default Light",
            IsDark = false,
            WindowBackground = "#F9F9F9",
            Foreground = "#101010",
            MenuBackground = "#F0F0F0",
            MenuForeground = "#101010",
            TabBarBackground = "#EDEDED",
            TabItemActiveBackground = "#F9F9F9",
            TabItemInactiveBackground = "#E2E2E2",
            TabItemActiveForeground = "#101010",
            TabItemInactiveForeground = "#666666",
            EditorBackground = "#F9F9F9",
            EditorForeground = "#101010",
            LineNumbersForeground = "#888888",
            SelectionBackground = "#ADD6FF",
            StatusBarBackground = "#007ACC",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#007ACC",
            BorderColor = "#DCDCDC",
            CodeKeyword = "#0000FF",
            CodeComment = "#008000",
            CodeString = "#A31515",
            CodeNumber = "#098658",
            CodeType = "#267F99",
            CodeMethod = "#795E26",
            CodePreprocessor = "#AF00DB",
            CodePunctuation = "#101010"
        });

        _themes.Add(new ColorTheme
        {
            Id = "dark-plus",
            Name = "Dark+ (VS Code)",
            IsDark = true,
            WindowBackground = "#1E1E1E",
            Foreground = "#D4D4D4",
            MenuBackground = "#252526",
            MenuForeground = "#CCCCCC",
            TabBarBackground = "#252526",
            TabItemActiveBackground = "#1E1E1E",
            TabItemInactiveBackground = "#2D2D2D",
            TabItemActiveForeground = "#FFFFFF",
            TabItemInactiveForeground = "#969696",
            EditorBackground = "#1E1E1E",
            EditorForeground = "#D4D4D4",
            LineNumbersForeground = "#5A5A5A",
            SelectionBackground = "#264F78",
            StatusBarBackground = "#007ACC",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#007ACC",
            BorderColor = "#181818",
            CodeKeyword = "#569CD6",
            CodeComment = "#6A9955",
            CodeString = "#CE9178",
            CodeNumber = "#B5CEA8",
            CodeType = "#4EC9B0",
            CodeMethod = "#DCDCAA",
            CodePreprocessor = "#9B9B9B",
            CodePunctuation = "#D4D4D4"
        });

        _themes.Add(new ColorTheme
        {
            Id = "one-dark",
            Name = "One Dark Pro",
            IsDark = true,
            WindowBackground = "#282C34",
            Foreground = "#ABB2BF",
            MenuBackground = "#21252B",
            MenuForeground = "#9DA5B4",
            TabBarBackground = "#21252B",
            TabItemActiveBackground = "#282C34",
            TabItemInactiveBackground = "#2C313A",
            TabItemActiveForeground = "#D7DAE0",
            TabItemInactiveForeground = "#7A828E",
            EditorBackground = "#282C34",
            EditorForeground = "#ABB2BF",
            LineNumbersForeground = "#4B5263",
            SelectionBackground = "#3E4451",
            StatusBarBackground = "#21252B",
            StatusBarForeground = "#98C379",
            AccentColor = "#61AFEF",
            BorderColor = "#181A1F",
            CodeKeyword = "#C678DD",
            CodeComment = "#5C6370",
            CodeString = "#98C379",
            CodeNumber = "#D19A66",
            CodeType = "#E5C07B",
            CodeMethod = "#61AFEF",
            CodePreprocessor = "#E06C75",
            CodePunctuation = "#ABB2BF"
        });

        _themes.Add(new ColorTheme
        {
            Id = "monokai",
            Name = "Monokai",
            IsDark = true,
            WindowBackground = "#272822",
            Foreground = "#F8F8F2",
            MenuBackground = "#1E1F1C",
            MenuForeground = "#CCCCCC",
            TabBarBackground = "#1E1F1C",
            TabItemActiveBackground = "#272822",
            TabItemInactiveBackground = "#3E3D32",
            TabItemActiveForeground = "#F8F8F2",
            TabItemInactiveForeground = "#75715E",
            EditorBackground = "#272822",
            EditorForeground = "#F8F8F2",
            LineNumbersForeground = "#75715E",
            SelectionBackground = "#49483E",
            StatusBarBackground = "#FD971F",
            StatusBarForeground = "#272822",
            AccentColor = "#A6E22E",
            BorderColor = "#1B1D16",
            CodeKeyword = "#F92672",
            CodeComment = "#75715E",
            CodeString = "#E6DB74",
            CodeNumber = "#AE81FF",
            CodeType = "#66D9EF",
            CodeMethod = "#A6E22E",
            CodePreprocessor = "#FD971F",
            CodePunctuation = "#F8F8F2"
        });

        _themes.Add(new ColorTheme
        {
            Id = "dracula",
            Name = "Dracula",
            IsDark = true,
            WindowBackground = "#282A36",
            Foreground = "#F8F8F2",
            MenuBackground = "#1E1F29",
            MenuForeground = "#F8F8F2",
            TabBarBackground = "#1E1F29",
            TabItemActiveBackground = "#282A36",
            TabItemInactiveBackground = "#44475A",
            TabItemActiveForeground = "#F8F8F2",
            TabItemInactiveForeground = "#6272A4",
            EditorBackground = "#282A36",
            EditorForeground = "#F8F8F2",
            LineNumbersForeground = "#6272A4",
            SelectionBackground = "#44475A",
            StatusBarBackground = "#6272A4",
            StatusBarForeground = "#F8F8F2",
            AccentColor = "#BD93F9",
            BorderColor = "#191A21",
            CodeKeyword = "#FF79C6",
            CodeComment = "#6272A4",
            CodeString = "#F1FA8C",
            CodeNumber = "#BD93F9",
            CodeType = "#8BE9FD",
            CodeMethod = "#50FA7B",
            CodePreprocessor = "#FFB86C",
            CodePunctuation = "#F8F8F2"
        });

        _themes.Add(new ColorTheme
        {
            Id = "solarized-dark",
            Name = "Solarized Dark",
            IsDark = true,
            WindowBackground = "#002B36",
            Foreground = "#839496",
            MenuBackground = "#073642",
            MenuForeground = "#93A1A1",
            TabBarBackground = "#073642",
            TabItemActiveBackground = "#002B36",
            TabItemInactiveBackground = "#073642",
            TabItemActiveForeground = "#93A1A1",
            TabItemInactiveForeground = "#586E75",
            EditorBackground = "#002B36",
            EditorForeground = "#839496",
            LineNumbersForeground = "#586E75",
            SelectionBackground = "#073642",
            StatusBarBackground = "#268BD2",
            StatusBarForeground = "#002B36",
            AccentColor = "#2AA198",
            BorderColor = "#001F27",
            CodeKeyword = "#859900",
            CodeComment = "#586E75",
            CodeString = "#2AA198",
            CodeNumber = "#D33682",
            CodeType = "#B58900",
            CodeMethod = "#268BD2",
            CodePreprocessor = "#CB4B16",
            CodePunctuation = "#839496"
        });

        _themes.Add(new ColorTheme
        {
            Id = "github-light",
            Name = "GitHub Light",
            IsDark = false,
            WindowBackground = "#FFFFFF",
            Foreground = "#24292E",
            MenuBackground = "#F6F8FA",
            MenuForeground = "#24292E",
            TabBarBackground = "#F6F8FA",
            TabItemActiveBackground = "#FFFFFF",
            TabItemInactiveBackground = "#E1E4E8",
            TabItemActiveForeground = "#24292E",
            TabItemInactiveForeground = "#586069",
            EditorBackground = "#FFFFFF",
            EditorForeground = "#24292E",
            LineNumbersForeground = "#959DA5",
            SelectionBackground = "#B3D7FF",
            StatusBarBackground = "#0366D6",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#0366D6",
            BorderColor = "#D1D5DA",
            CodeKeyword = "#D73A49",
            CodeComment = "#6A737D",
            CodeString = "#032F62",
            CodeNumber = "#005CC5",
            CodeType = "#6F42C1",
            CodeMethod = "#6F42C1",
            CodePreprocessor = "#D73A49",
            CodePunctuation = "#24292E"
        });

        _themes.Add(new ColorTheme
        {
            Id = "tokyo-night",
            Name = "Tokyo Night",
            IsDark = true,
            WindowBackground = "#1A1B26",
            Foreground = "#A9B1D6",
            MenuBackground = "#16161E",
            MenuForeground = "#C0CAF5",
            TabBarBackground = "#16161E",
            TabItemActiveBackground = "#1A1B26",
            TabItemInactiveBackground = "#1F2335",
            TabItemActiveForeground = "#7AA2F7",
            TabItemInactiveForeground = "#565F89",
            EditorBackground = "#1A1B26",
            EditorForeground = "#A9B1D6",
            LineNumbersForeground = "#3B4261",
            SelectionBackground = "#283457",
            StatusBarBackground = "#7AA2F7",
            StatusBarForeground = "#15161E",
            AccentColor = "#7AA2F7",
            BorderColor = "#101014",
            CodeKeyword = "#BB9AF7",
            CodeComment = "#565F89",
            CodeString = "#9ECE6A",
            CodeNumber = "#FF9E64",
            CodeType = "#2AC3DE",
            CodeMethod = "#7AA2F7",
            CodePreprocessor = "#F7768E",
            CodePunctuation = "#C0CAF5"
        });

        _themes.Add(new ColorTheme
        {
            Id = "nord",
            Name = "Nord",
            IsDark = true,
            WindowBackground = "#2E3440",
            Foreground = "#D8DEE9",
            MenuBackground = "#242933",
            MenuForeground = "#ECEFF4",
            TabBarBackground = "#242933",
            TabItemActiveBackground = "#2E3440",
            TabItemInactiveBackground = "#3B4252",
            TabItemActiveForeground = "#88C0D0",
            TabItemInactiveForeground = "#4C566A",
            EditorBackground = "#2E3440",
            EditorForeground = "#D8DEE9",
            LineNumbersForeground = "#4C566A",
            SelectionBackground = "#434C5E",
            StatusBarBackground = "#5E81AC",
            StatusBarForeground = "#ECEFF4",
            AccentColor = "#88C0D0",
            BorderColor = "#1D2128",
            CodeKeyword = "#81A1C1",
            CodeComment = "#616E88",
            CodeString = "#A3BE8C",
            CodeNumber = "#B48EAD",
            CodeType = "#8FBCBB",
            CodeMethod = "#88C0D0",
            CodePreprocessor = "#D08770",
            CodePunctuation = "#ECEFF4"
        });

        _themes.Add(new ColorTheme
        {
            Id = "catppuccin-mocha",
            Name = "Catppuccin Mocha",
            IsDark = true,
            WindowBackground = "#1E1E2E",
            Foreground = "#CDD6F4",
            MenuBackground = "#181825",
            MenuForeground = "#CDD6F4",
            TabBarBackground = "#181825",
            TabItemActiveBackground = "#1E1E2E",
            TabItemInactiveBackground = "#313244",
            TabItemActiveForeground = "#CBA6F7",
            TabItemInactiveForeground = "#6C7086",
            EditorBackground = "#1E1E2E",
            EditorForeground = "#CDD6F4",
            LineNumbersForeground = "#585B70",
            SelectionBackground = "#45475A",
            StatusBarBackground = "#CBA6F7",
            StatusBarForeground = "#11111B",
            AccentColor = "#CBA6F7",
            BorderColor = "#11111B",
            CodeKeyword = "#CBA6F7",
            CodeComment = "#6C7086",
            CodeString = "#A6E3A1",
            CodeNumber = "#FAB387",
            CodeType = "#F9E2AF",
            CodeMethod = "#89B4FA",
            CodePreprocessor = "#F38BA8",
            CodePunctuation = "#9399B2"
        });

        _themes.Add(new ColorTheme
        {
            Id = "github-dark",
            Name = "GitHub Dark",
            IsDark = true,
            WindowBackground = "#0D1117",
            Foreground = "#C9D1D9",
            MenuBackground = "#161B22",
            MenuForeground = "#C9D1D9",
            TabBarBackground = "#161B22",
            TabItemActiveBackground = "#0D1117",
            TabItemInactiveBackground = "#21262D",
            TabItemActiveForeground = "#58A6FF",
            TabItemInactiveForeground = "#8B949E",
            EditorBackground = "#0D1117",
            EditorForeground = "#C9D1D9",
            LineNumbersForeground = "#484F58",
            SelectionBackground = "#1F6FEB",
            StatusBarBackground = "#1F6FEB",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#58A6FF",
            BorderColor = "#010409",
            CodeKeyword = "#FF7B72",
            CodeComment = "#8B949E",
            CodeString = "#A5D6FF",
            CodeNumber = "#79C0FF",
            CodeType = "#FFA657",
            CodeMethod = "#D2A8FF",
            CodePreprocessor = "#FF7B72",
            CodePunctuation = "#C9D1D9"
        });

        _themes.Add(new ColorTheme
        {
            Id = "solarized-light",
            Name = "Solarized Light",
            IsDark = false,
            WindowBackground = "#FDF6E3",
            Foreground = "#657B83",
            MenuBackground = "#EEE8D5",
            MenuForeground = "#586E75",
            TabBarBackground = "#EEE8D5",
            TabItemActiveBackground = "#FDF6E3",
            TabItemInactiveBackground = "#E0D9C5",
            TabItemActiveForeground = "#268BD2",
            TabItemInactiveForeground = "#93A1A1",
            EditorBackground = "#FDF6E3",
            EditorForeground = "#657B83",
            LineNumbersForeground = "#93A1A1",
            SelectionBackground = "#EEE8D5",
            StatusBarBackground = "#268BD2",
            StatusBarForeground = "#FDF6E3",
            AccentColor = "#2AA198",
            BorderColor = "#D33682",
            CodeKeyword = "#859900",
            CodeComment = "#93A1A1",
            CodeString = "#2AA198",
            CodeNumber = "#D33682",
            CodeType = "#B58900",
            CodeMethod = "#268BD2",
            CodePreprocessor = "#CB4B16",
            CodePunctuation = "#657B83"
        });

        _themes.Add(new ColorTheme
        {
            Id = "catppuccin-latte",
            Name = "Catppuccin Latte",
            IsDark = false,
            WindowBackground = "#EFF1F5",
            Foreground = "#4C4F69",
            MenuBackground = "#E6E9EF",
            MenuForeground = "#4C4F69",
            TabBarBackground = "#E6E9EF",
            TabItemActiveBackground = "#EFF1F5",
            TabItemInactiveBackground = "#CCD0DA",
            TabItemActiveForeground = "#8839EF",
            TabItemInactiveForeground = "#9CA0B0",
            EditorBackground = "#EFF1F5",
            EditorForeground = "#4C4F69",
            LineNumbersForeground = "#9CA0B0",
            SelectionBackground = "#BCC0CC",
            StatusBarBackground = "#8839EF",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#8839EF",
            BorderColor = "#DCE0E8",
            CodeKeyword = "#8839EF",
            CodeComment = "#9CA0B0",
            CodeString = "#40A02B",
            CodeNumber = "#FE640B",
            CodeType = "#DF8E1D",
            CodeMethod = "#1E66F5",
            CodePreprocessor = "#EA76CB",
            CodePunctuation = "#5C5F77"
        });

        _themes.Add(new ColorTheme
        {
            Id = "cyberpunk",
            Name = "Synthwave '84 (Cyberpunk)",
            IsDark = true,
            WindowBackground = "#262335",
            Foreground = "#ECEFF4",
            MenuBackground = "#1F1D2B",
            MenuForeground = "#FE4450",
            TabBarBackground = "#1F1D2B",
            TabItemActiveBackground = "#262335",
            TabItemInactiveBackground = "#2A2139",
            TabItemActiveForeground = "#FF7EDB",
            TabItemInactiveForeground = "#727086",
            EditorBackground = "#262335",
            EditorForeground = "#ECEFF4",
            LineNumbersForeground = "#49455B",
            SelectionBackground = "#3D2C5A",
            StatusBarBackground = "#FE4450",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#FF7EDB",
            BorderColor = "#1A1824",
            CodeKeyword = "#FF7EDB",
            CodeComment = "#614D85",
            CodeString = "#FFD400",
            CodeNumber = "#F97E72",
            CodeType = "#36F9F6",
            CodeMethod = "#72F1B8",
            CodePreprocessor = "#FE4450",
            CodePunctuation = "#FE4450"
        });

        _themes.Add(new ColorTheme
        {
            Id = "gruvbox-dark",
            Name = "Gruvbox Dark",
            IsDark = true,
            WindowBackground = "#282828",
            Foreground = "#EBDBB2",
            MenuBackground = "#1D2021",
            MenuForeground = "#D5C4A1",
            TabBarBackground = "#1D2021",
            TabItemActiveBackground = "#282828",
            TabItemInactiveBackground = "#3C3836",
            TabItemActiveForeground = "#FABD2F",
            TabItemInactiveForeground = "#7C6F64",
            EditorBackground = "#282828",
            EditorForeground = "#EBDBB2",
            LineNumbersForeground = "#665C54",
            SelectionBackground = "#504945",
            StatusBarBackground = "#D79921",
            StatusBarForeground = "#282828",
            AccentColor = "#FABD2F",
            BorderColor = "#1D2021",
            CodeKeyword = "#FB4934",
            CodeComment = "#928374",
            CodeString = "#B8BB26",
            CodeNumber = "#D3869B",
            CodeType = "#FABD2F",
            CodeMethod = "#8EC07C",
            CodePreprocessor = "#FE8019",
            CodePunctuation = "#EBDBB2"
        });

        _themes.Add(new ColorTheme
        {
            Id = "monokai-pro",
            Name = "Monokai Pro",
            IsDark = true,
            WindowBackground = "#2D2A2E",
            Foreground = "#FCFCFA",
            MenuBackground = "#221F22",
            MenuForeground = "#C1C0C0",
            TabBarBackground = "#221F22",
            TabItemActiveBackground = "#2D2A2E",
            TabItemInactiveBackground = "#3A3539",
            TabItemActiveForeground = "#FFD866",
            TabItemInactiveForeground = "#727072",
            EditorBackground = "#2D2A2E",
            EditorForeground = "#FCFCFA",
            LineNumbersForeground = "#5B595C",
            SelectionBackground = "#403E41",
            StatusBarBackground = "#FFD866",
            StatusBarForeground = "#2D2A2E",
            AccentColor = "#FF6188",
            BorderColor = "#19181A",
            CodeKeyword = "#FF6188",
            CodeComment = "#727072",
            CodeString = "#FFD866",
            CodeNumber = "#AB9DF2",
            CodeType = "#78DCE8",
            CodeMethod = "#A9DC76",
            CodePreprocessor = "#FC9867",
            CodePunctuation = "#FCFCFA"
        });

        _themes.Add(new ColorTheme
        {
            Id = "rose-pine",
            Name = "Rosé Pine",
            IsDark = true,
            WindowBackground = "#191724",
            Foreground = "#E0DEF4",
            MenuBackground = "#12101F",
            MenuForeground = "#E0DEF4",
            TabBarBackground = "#12101F",
            TabItemActiveBackground = "#191724",
            TabItemInactiveBackground = "#211F35",
            TabItemActiveForeground = "#C4A7E7",
            TabItemInactiveForeground = "#6E6A86",
            EditorBackground = "#191724",
            EditorForeground = "#E0DEF4",
            LineNumbersForeground = "#524F67",
            SelectionBackground = "#2A283E",
            StatusBarBackground = "#EB6F92",
            StatusBarForeground = "#191724",
            AccentColor = "#C4A7E7",
            BorderColor = "#0E0C15",
            CodeKeyword = "#31748F",
            CodeComment = "#6E6A86",
            CodeString = "#F6C177",
            CodeNumber = "#EBBCBA",
            CodeType = "#9CCFD8",
            CodeMethod = "#C4A7E7",
            CodePreprocessor = "#EB6F92",
            CodePunctuation = "#E0DEF4"
        });

        _themes.Add(new ColorTheme
        {
            Id = "aura-dark",
            Name = "Aura Dark",
            IsDark = true,
            WindowBackground = "#15141B",
            Foreground = "#EDECEE",
            MenuBackground = "#111015",
            MenuForeground = "#EDECEE",
            TabBarBackground = "#111015",
            TabItemActiveBackground = "#15141B",
            TabItemInactiveBackground = "#21202E",
            TabItemActiveForeground = "#A277FF",
            TabItemInactiveForeground = "#6D6D6D",
            EditorBackground = "#15141B",
            EditorForeground = "#EDECEE",
            LineNumbersForeground = "#4D4D4D",
            SelectionBackground = "#2D2B3D",
            StatusBarBackground = "#A277FF",
            StatusBarForeground = "#15141B",
            AccentColor = "#61FFCA",
            BorderColor = "#0E0D12",
            CodeKeyword = "#A277FF",
            CodeComment = "#6D6D6D",
            CodeString = "#61FFCA",
            CodeNumber = "#FFCA85",
            CodeType = "#82E2FF",
            CodeMethod = "#FF6767",
            CodePreprocessor = "#ED8274",
            CodePunctuation = "#EDECEE"
        });

        _themes.Add(new ColorTheme
        {
            Id = "one-light",
            Name = "One Light",
            IsDark = false,
            WindowBackground = "#FAFAFA",
            Foreground = "#383A42",
            MenuBackground = "#F0F0F0",
            MenuForeground = "#383A42",
            TabBarBackground = "#F0F0F0",
            TabItemActiveBackground = "#FAFAFA",
            TabItemInactiveBackground = "#E5E5E6",
            TabItemActiveForeground = "#4078F2",
            TabItemInactiveForeground = "#A0A1A7",
            EditorBackground = "#FAFAFA",
            EditorForeground = "#383A42",
            LineNumbersForeground = "#9D9D9F",
            SelectionBackground = "#E5E5E6",
            StatusBarBackground = "#4078F2",
            StatusBarForeground = "#FFFFFF",
            AccentColor = "#50A14F",
            BorderColor = "#DBDBDC",
            CodeKeyword = "#A626A4",
            CodeComment = "#A0A1A7",
            CodeString = "#50A14F",
            CodeNumber = "#986801",
            CodeType = "#C18401",
            CodeMethod = "#4078F2",
            CodePreprocessor = "#E45649",
            CodePunctuation = "#383A42"
        });
    }

    private void EnsureDirectoriesAndSamples()
    {
        try
        {
            if (!Directory.Exists(_themesDirectory))
            {
                Directory.CreateDirectory(_themesDirectory);
            }

            var samplePath = Path.Combine(_themesDirectory, "SampleCustomTheme.json");
            if (!File.Exists(samplePath))
            {
                var sample = new ColorTheme
                {
                    Id = "nordic-frost",
                    Name = "Nordic Frost",
                    IsDark = true,
                    WindowBackground = "#2E3440",
                    Foreground = "#D8DEE9",
                    MenuBackground = "#242933",
                    MenuForeground = "#D8DEE9",
                    TabBarBackground = "#242933",
                    TabItemActiveBackground = "#2E3440",
                    TabItemInactiveBackground = "#3B4252",
                    TabItemActiveForeground = "#ECEFF4",
                    TabItemInactiveForeground = "#98A2B3",
                    EditorBackground = "#2E3440",
                    EditorForeground = "#D8DEE9",
                    LineNumbersForeground = "#4C566A",
                    SelectionBackground = "#434C5E",
                    StatusBarBackground = "#434C5E",
                    StatusBarForeground = "#ECEFF4",
                    AccentColor = "#88C0D0",
                    BorderColor = "#1E222A",
                    CodeKeyword = "#81A1C1",
                    CodeComment = "#616E88",
                    CodeString = "#A3BE8C",
                    CodeNumber = "#B48EAD",
                    CodeType = "#8FBCBB",
                    CodeMethod = "#88C0D0",
                    CodePreprocessor = "#D08770",
                    CodePunctuation = "#ECEFF4"
                };
                var json = JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(samplePath, json);
            }
        }
        catch
        {
            // Directory creation fallback handled
        }
    }

    private void LoadUserThemes()
    {
        if (!Directory.Exists(_themesDirectory)) return;

        try
        {
            var files = Directory.GetFiles(_themesDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var theme = JsonSerializer.Deserialize<ColorTheme>(json);
                    if (theme != null)
                    {
                        if (string.IsNullOrWhiteSpace(theme.Id))
                        {
                            theme.Id = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace(' ', '-');
                        }
                        if (string.IsNullOrWhiteSpace(theme.Name))
                        {
                            theme.Name = Path.GetFileNameWithoutExtension(file);
                        }

                        lock (_themes)
                        {
                            var existingIndex = _themes.FindIndex(t => string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase));
                            if (existingIndex >= 0)
                            {
                                _themes[existingIndex] = theme;
                            }
                            else
                            {
                                _themes.Add(theme);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore invalid theme file
                }
            }

            // Also auto-register any user .xshd syntax files in the themes folder
            var xshdFiles = Directory.GetFiles(_themesDirectory, "*.xshd");
            foreach (var xshdFile in xshdFiles)
            {
                try
                {
                    RegisterXshdFile(xshdFile);
                }
                catch
                {
                    // Ignore invalid syntax file
                }
            }
        }
        catch
        {
            // Ignored
        }
    }

    public void ApplyTheme(string themeIdOrName)
    {
        ColorTheme? target;
        lock (_themes)
        {
            target = _themes.FirstOrDefault(t => string.Equals(t.Id, themeIdOrName, StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(t.Name, themeIdOrName, StringComparison.OrdinalIgnoreCase));
        }
        if (target != null)
        {
            ApplyTheme(target);
        }
    }

    public void ApplyTheme(ColorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _currentTheme = theme;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

            SetColorResource("ThemeWindowBackground", theme.WindowBackground);
            SetColorResource("ThemeWindowForeground", theme.Foreground);
            SetColorResource("ThemeMenuBackground", theme.MenuBackground);
            SetColorResource("ThemeMenuForeground", theme.MenuForeground);
            SetColorResource("ThemeTabBarBackground", theme.TabBarBackground);
            SetColorResource("ThemeTabItemActiveBackground", theme.TabItemActiveBackground);
            SetColorResource("ThemeTabItemInactiveBackground", theme.TabItemInactiveBackground);
            SetColorResource("ThemeTabItemActiveForeground", theme.TabItemActiveForeground);
            SetColorResource("ThemeTabItemInactiveForeground", theme.TabItemInactiveForeground);
            SetColorResource("ThemeEditorBackground", theme.EditorBackground);
            SetColorResource("ThemeEditorForeground", theme.EditorForeground);
            SetColorResource("ThemeLineNumbersForeground", theme.LineNumbersForeground);
            SetColorResource("ThemeSelectionBackground", theme.SelectionBackground);
            SetColorResource("ThemeStatusBarBackground", theme.StatusBarBackground);
            SetColorResource("ThemeStatusBarForeground", theme.StatusBarForeground);
            SetColorResource("ThemeAccentBrush", theme.AccentColor);
            SetColorResource("ThemeBorderBrush", theme.BorderColor);

            // Code Syntax Highlight Resources
            SetColorResource("ThemeCodeKeyword", theme.CodeKeyword);
            SetColorResource("ThemeCodeComment", theme.CodeComment);
            SetColorResource("ThemeCodeString", theme.CodeString);
            SetColorResource("ThemeCodeNumber", theme.CodeNumber);
            SetColorResource("ThemeCodeType", theme.CodeType);
            SetColorResource("ThemeCodeMethod", theme.CodeMethod);
            SetColorResource("ThemeCodePreprocessor", theme.CodePreprocessor);
            SetColorResource("ThemeCodePunctuation", theme.CodePunctuation);
        }

        SaveThemeName(theme.Id);
        ThemeChanged?.Invoke(theme);
    }

    public bool IsSystemDarkTheme()
    {
        try
        {
            if (Application.Current?.PlatformSettings != null)
            {
                var variant = Application.Current.PlatformSettings.GetColorValues().ThemeVariant;
                return variant == Avalonia.Platform.PlatformThemeVariant.Dark;
            }
        }
        catch { }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
        }

        return true;
    }

    public ColorTheme ResolveEffectiveTheme(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool isDark;
        if (string.Equals(settings.ThemeMode, "Light", StringComparison.OrdinalIgnoreCase))
        {
            isDark = false;
        }
        else if (string.Equals(settings.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            isDark = true;
        }
        else
        {
            isDark = IsSystemDarkTheme();
        }

        ColorTheme? baseTheme = null;
        lock (_themes)
        {
            if (!isDark)
            {
                var targetId = !string.IsNullOrWhiteSpace(settings.LightPresetId) && !string.Equals(settings.LightPresetId, "default-light", StringComparison.OrdinalIgnoreCase)
                    ? settings.LightPresetId
                    : (!string.IsNullOrWhiteSpace(settings.ThemeId) ? settings.ThemeId : (!string.IsNullOrWhiteSpace(settings.LightPresetId) ? settings.LightPresetId : "default-light"));

                baseTheme = _themes.FirstOrDefault(t => !t.IsDark && (string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, targetId, StringComparison.OrdinalIgnoreCase)))
                            ?? _themes.FirstOrDefault(t => !t.IsDark)
                            ?? _themes[0];
            }
            else
            {
                var targetId = !string.IsNullOrWhiteSpace(settings.DarkPresetId) && !string.Equals(settings.DarkPresetId, "default-dark", StringComparison.OrdinalIgnoreCase)
                    ? settings.DarkPresetId
                    : (!string.IsNullOrWhiteSpace(settings.ThemeId) ? settings.ThemeId : (!string.IsNullOrWhiteSpace(settings.DarkPresetId) ? settings.DarkPresetId : "default-dark"));

                baseTheme = _themes.FirstOrDefault(t => t.IsDark && (string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, targetId, StringComparison.OrdinalIgnoreCase)))
                            ?? _themes.FirstOrDefault(t => t.IsDark)
                            ?? _themes[0];
            }
        }

        var effective = baseTheme.Clone();

        if (!isDark)
        {
            var isCustomTheme = !string.Equals(baseTheme.Id, "default-light", StringComparison.OrdinalIgnoreCase);
            if (IsValidHexColor(settings.LightBackgroundHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.LightBackgroundHex), "#F9F9F9", StringComparison.OrdinalIgnoreCase)))
            {
                var bg = NormalizeHex(settings.LightBackgroundHex);
                effective.WindowBackground = bg;
                effective.EditorBackground = bg;
                effective.TabItemActiveBackground = bg;
            }
            if (IsValidHexColor(settings.LightForegroundHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.LightForegroundHex), "#101010", StringComparison.OrdinalIgnoreCase)))
            {
                var fg = NormalizeHex(settings.LightForegroundHex);
                effective.Foreground = fg;
                effective.EditorForeground = fg;
                effective.TabItemActiveForeground = fg;
            }
            if (IsValidHexColor(settings.LightAccentHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.LightAccentHex), "#007ACC", StringComparison.OrdinalIgnoreCase)))
            {
                var acc = NormalizeHex(settings.LightAccentHex);
                effective.AccentColor = acc;
                effective.StatusBarBackground = acc;
            }

            if (string.Equals(settings.ContrastMode, "Strong", StringComparison.OrdinalIgnoreCase))
            {
                effective.WindowBackground = "#FFFFFF";
                effective.EditorBackground = "#FFFFFF";
                effective.Foreground = "#000000";
                effective.EditorForeground = "#000000";
                effective.BorderColor = "#000000";
                effective.LineNumbersForeground = "#000000";
                effective.TabItemActiveForeground = "#000000";
                effective.SelectionBackground = "#99CCFF";
            }
        }
        else
        {
            var isCustomTheme = !string.Equals(baseTheme.Id, "default-dark", StringComparison.OrdinalIgnoreCase);
            if (IsValidHexColor(settings.DarkBackgroundHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.DarkBackgroundHex), "#101010", StringComparison.OrdinalIgnoreCase)))
            {
                var bg = NormalizeHex(settings.DarkBackgroundHex);
                effective.WindowBackground = bg;
                effective.EditorBackground = bg;
                effective.TabItemActiveBackground = bg;
            }
            if (IsValidHexColor(settings.DarkForegroundHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.DarkForegroundHex), "#CCCCCC", StringComparison.OrdinalIgnoreCase)))
            {
                var fg = NormalizeHex(settings.DarkForegroundHex);
                effective.Foreground = fg;
                effective.EditorForeground = fg;
                effective.TabItemActiveForeground = fg;
            }
            if (IsValidHexColor(settings.DarkAccentHex) && (!isCustomTheme || !string.Equals(NormalizeHex(settings.DarkAccentHex), "#007ACC", StringComparison.OrdinalIgnoreCase)))
            {
                var acc = NormalizeHex(settings.DarkAccentHex);
                effective.AccentColor = acc;
                effective.StatusBarBackground = acc;
            }

            if (string.Equals(settings.ContrastMode, "Strong", StringComparison.OrdinalIgnoreCase))
            {
                effective.WindowBackground = "#000000";
                effective.EditorBackground = "#000000";
                effective.Foreground = "#FFFFFF";
                effective.EditorForeground = "#FFFFFF";
                effective.BorderColor = "#555555";
                effective.LineNumbersForeground = "#FFFFFF";
                effective.TabItemActiveForeground = "#FFFFFF";
                effective.SelectionBackground = "#1B4F8B";
            }
        }

        // Apply Custom Code Syntax Highlighting Color Overrides if specified
        if (IsValidHexColor(settings.CustomCodeKeyword))
            effective.CodeKeyword = NormalizeHex(settings.CustomCodeKeyword!);
        if (IsValidHexColor(settings.CustomCodeComment))
            effective.CodeComment = NormalizeHex(settings.CustomCodeComment!);
        if (IsValidHexColor(settings.CustomCodeString))
            effective.CodeString = NormalizeHex(settings.CustomCodeString!);
        if (IsValidHexColor(settings.CustomCodeNumber))
            effective.CodeNumber = NormalizeHex(settings.CustomCodeNumber!);
        if (IsValidHexColor(settings.CustomCodeType))
            effective.CodeType = NormalizeHex(settings.CustomCodeType!);
        if (IsValidHexColor(settings.CustomCodeMethod))
            effective.CodeMethod = NormalizeHex(settings.CustomCodeMethod!);
        if (IsValidHexColor(settings.CustomCodePreprocessor))
            effective.CodePreprocessor = NormalizeHex(settings.CustomCodePreprocessor!);
        if (IsValidHexColor(settings.CustomCodePunctuation))
            effective.CodePunctuation = NormalizeHex(settings.CustomCodePunctuation!);

        return effective;
    }

    public void ApplyTheme(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var effective = ResolveEffectiveTheme(settings);
        ApplyTheme(effective);
    }

    public static bool IsValidHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
        }
        var withHash = "#" + clean;
        return Color.TryParse(withHash, out _);
    }

    public static string NormalizeHex(string hex)
    {
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            clean = $"{clean[0]}{clean[0]}{clean[1]}{clean[1]}{clean[2]}{clean[2]}";
        }
        return "#" + clean.ToUpperInvariant();
    }

    public void ApplyCodeColorsToHighlighting(IHighlightingDefinition? definition, ColorTheme? theme = null)
    {
        if (definition == null) return;
        var t = theme ?? _currentTheme;
        if (t == null) return;

        try
        {
            var kwBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeKeyword));
            var commentBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeComment));
            var stringBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeString));
            var numberBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeNumber));
            var typeBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeType));
            var methodBrush = new SimpleHighlightingBrush(Color.Parse(t.CodeMethod));
            var preprocBrush = new SimpleHighlightingBrush(Color.Parse(t.CodePreprocessor));
            var punctBrush = new SimpleHighlightingBrush(Color.Parse(t.CodePunctuation));

            var varBrush = new SimpleHighlightingBrush(Color.Parse(t.Foreground));

            foreach (var color in definition.NamedHighlightingColors)
            {
                var name = color.Name?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("comment") || (name.Contains("xml") && name.Contains("doc")))
                {
                    color.Foreground = commentBrush;
                }
                else if (name.Contains("string") || name.Contains("char") || name.Contains("regex") || (name.Contains("literal") && !name.Contains("number")))
                {
                    color.Foreground = stringBrush;
                }
                else if (name.Contains("attributevalue") || name.Contains("attrvalue"))
                {
                    color.Foreground = stringBrush;
                }
                else if (name.Contains("keyword") || name.Contains("truefalse") || name.Contains("null") || name.Contains("controlflow") || name.Contains("statement") || name.Contains("modifier") || name.Contains("visibility"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("digit") || name.Contains("number") || name.Contains("float") || name.Contains("hex") || name.Contains("integer"))
                {
                    color.Foreground = numberBrush;
                }
                else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface") || name.Contains("enum") || name.Contains("typedef"))
                {
                    color.Foreground = typeBrush;
                }
                else if (name.Contains("tag") || name.Contains("element"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("attribute") || name.Contains("annotation"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("method") || name.Contains("function") || name.Contains("call") || name.Contains("invoc"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("preprocessor") || name.Contains("directive") || name.Contains("include") || name.Contains("define") || name.Contains("macro"))
                {
                    color.Foreground = preprocBrush;
                }
                else if (name.Contains("heading") || name.Contains("header"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("bold") || name.Contains("strong"))
                {
                    color.Foreground = varBrush;
                }
                else if (name.Contains("italic") || name.Contains("emphasis"))
                {
                    color.Foreground = commentBrush;
                }
                else if (name.Contains("link") || name.Contains("url"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("propertykey") || name.Contains("key"))
                {
                    color.Foreground = typeBrush;
                }
                else if (name.Contains("punctuation") || name.Contains("delimiter") || name.Contains("operator") || name.Contains("parenthes") || name.Contains("bracket") || name.Contains("brace") || name.Contains("colon") || name.Contains("semicolon"))
                {
                    color.Foreground = punctBrush;
                }
                else if (name.Contains("variable") || name.Contains("property") || name.Contains("field") || name.Contains("parameter"))
                {
                    color.Foreground = varBrush;
                }
            }
        }
        catch
        {
            // Graceful fallback
        }
    }

    private static void SetColorResource(string key, string hex)
    {
        if (Application.Current == null) return;

        try
        {
            var color = Color.Parse(hex);
            if (Application.Current.Resources.TryGetResource(key, null, out var existing) && existing is SolidColorBrush brush)
            {
                brush.Color = color;
            }
            else
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Fallback to white/black if parsing fails
        }
    }

    public async Task<ColorTheme> ImportThemeFromJsonAsync(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException("Theme file not found", jsonFilePath);
        }

        var json = await File.ReadAllTextAsync(jsonFilePath);
        var theme = JsonSerializer.Deserialize<ColorTheme>(json) 
            ?? throw new InvalidOperationException("Could not deserialize theme JSON.");

        if (string.IsNullOrWhiteSpace(theme.Id))
        {
            theme.Id = Path.GetFileNameWithoutExtension(jsonFilePath).ToLowerInvariant().Replace(' ', '-');
        }
        if (string.IsNullOrWhiteSpace(theme.Name))
        {
            theme.Name = Path.GetFileNameWithoutExtension(jsonFilePath);
        }

        GetThemesDirectory();

        // Write sanitized theme JSON guaranteeing Id and Name are present on disk
        var targetFile = Path.Combine(_themesDirectory, $"{theme.Id}.json");
        var serialized = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(targetFile, serialized);

        // Add or update in list
        lock (_themes)
        {
            var existingIndex = _themes.FindIndex(t => string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _themes[existingIndex] = theme;
            }
            else
            {
                _themes.Add(theme);
            }
        }

        SaveThemeName(theme.Id);
        ApplyTheme(theme);
        return theme;
    }

    public async Task<string> ImportSyntaxDefinitionAsync(string xshdFilePath)
    {
        if (!File.Exists(xshdFilePath))
        {
            throw new FileNotFoundException("Syntax definition file not found", xshdFilePath);
        }

        var fileName = Path.GetFileName(xshdFilePath);
        var targetFile = Path.Combine(_themesDirectory, fileName);

        if (!string.Equals(Path.GetFullPath(xshdFilePath), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(xshdFilePath, targetFile, overwrite: true);
        }

        return RegisterXshdFile(targetFile);
    }

    private static string RegisterXshdFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = XmlReader.Create(stream);
        var xshd = HighlightingLoader.LoadXshd(reader);
        var definition = HighlightingLoader.Load(xshd, HighlightingManager.Instance);

        var name = xshd.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(filePath);
        }

        var extensions = xshd.Extensions?.ToArray() ?? Array.Empty<string>();
        HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
        return name;
    }

    public string GetThemesDirectory()
    {
        if (!Directory.Exists(_themesDirectory))
        {
            Directory.CreateDirectory(_themesDirectory);
        }
        return _themesDirectory;
    }

    private string LoadSavedThemeName()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var text = File.ReadAllText(_settingsFilePath).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }
        catch
        {
            // Ignored
        }
        return "dark-plus";
    }

    private void SaveThemeName(string themeId)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_settingsFilePath, themeId);
        }
        catch
        {
            // Best effort
        }
    }
}
