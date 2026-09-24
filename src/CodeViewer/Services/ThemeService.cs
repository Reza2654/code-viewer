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

    public IReadOnlyList<ColorTheme> AvailableThemes => _themes.AsReadOnly();
    public ColorTheme CurrentTheme => _currentTheme;
    public event Action<ColorTheme>? ThemeChanged;

    public ThemeService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDir = Path.Combine(appData, "CodeViewer");
        _themesDirectory = Path.Combine(baseDir, "Themes");
        _settingsFilePath = Path.Combine(baseDir, "current_theme.txt");

        EnsureDirectoriesAndSamples();
        RegisterBuiltInThemes();
        LoadUserThemes();

        // Restore last chosen theme or default to Dark+
        var savedTheme = LoadSavedThemeName();
        _currentTheme = _themes.FirstOrDefault(t => string.Equals(t.Id, savedTheme, StringComparison.OrdinalIgnoreCase) ||
                                                    string.Equals(t.Name, savedTheme, StringComparison.OrdinalIgnoreCase))
                        ?? _themes[0];
    }

    private void RegisterBuiltInThemes()
    {
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
                    if (theme != null && !string.IsNullOrWhiteSpace(theme.Id) && !string.IsNullOrWhiteSpace(theme.Name))
                    {
                        if (!_themes.Any(t => string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            _themes.Add(theme);
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
        var target = _themes.FirstOrDefault(t => string.Equals(t.Id, themeIdOrName, StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(t.Name, themeIdOrName, StringComparison.OrdinalIgnoreCase));
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

            foreach (var color in definition.NamedHighlightingColors)
            {
                var name = color.Name?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("comment") || name.Contains("xml") && name.Contains("doc"))
                {
                    color.Foreground = commentBrush;
                }
                else if (name.Contains("string") || name.Contains("char") || name.Contains("literal") && !name.Contains("number"))
                {
                    color.Foreground = stringBrush;
                }
                else if (name.Contains("keyword") || name.Contains("truefalse") || name.Contains("null") || name.Contains("controlflow") || name.Contains("statement"))
                {
                    color.Foreground = kwBrush;
                }
                else if (name.Contains("digit") || name.Contains("number"))
                {
                    color.Foreground = numberBrush;
                }
                else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface"))
                {
                    color.Foreground = typeBrush;
                }
                else if (name.Contains("method") || name.Contains("function") || name.Contains("call"))
                {
                    color.Foreground = methodBrush;
                }
                else if (name.Contains("preprocessor") || name.Contains("directive"))
                {
                    color.Foreground = preprocBrush;
                }
                else if (name.Contains("punctuation") || name.Contains("delimiter"))
                {
                    color.Foreground = punctBrush;
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

        // Copy file to themes directory
        var targetFile = Path.Combine(_themesDirectory, $"{theme.Id}.json");
        if (!string.Equals(Path.GetFullPath(jsonFilePath), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(targetFile, json);
        }

        // Add or update in list
        var existingIndex = _themes.FindIndex(t => string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _themes[existingIndex] = theme;
        }
        else
        {
            _themes.Add(theme);
        }

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
