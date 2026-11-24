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
        lock (_themes)
        {
            _themes.AddRange(BuiltInThemes.GetAll());
        }
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
                var sample = BuiltInThemes.CreateSampleCustomTheme();
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
            SetColorResource("ThemeCodeVariable", theme.CodeVariable);
        }

        ApplyThemeToAllHighlightings(theme);
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
                string targetId;
                if (!string.IsNullOrWhiteSpace(settings.ThemeId) &&
                    !string.Equals(settings.ThemeId, "default-light", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(settings.ThemeId, "dark-plus", StringComparison.OrdinalIgnoreCase) &&
                    _themes.Any(t => !t.IsDark && (string.Equals(t.Id, settings.ThemeId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, settings.ThemeId, StringComparison.OrdinalIgnoreCase))))
                {
                    targetId = settings.ThemeId;
                }
                else if (!string.IsNullOrWhiteSpace(settings.LightPresetId))
                {
                    targetId = settings.LightPresetId;
                }
                else
                {
                    targetId = !string.IsNullOrWhiteSpace(settings.ThemeId) ? settings.ThemeId : "default-light";
                }

                baseTheme = _themes.FirstOrDefault(t => !t.IsDark && (string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, targetId, StringComparison.OrdinalIgnoreCase)))
                            ?? _themes.FirstOrDefault(t => !t.IsDark)
                            ?? _themes[0];
            }
            else
            {
                string targetId;
                if (!string.IsNullOrWhiteSpace(settings.ThemeId) &&
                    !string.Equals(settings.ThemeId, "default-dark", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(settings.ThemeId, "dark-plus", StringComparison.OrdinalIgnoreCase) &&
                    _themes.Any(t => t.IsDark && (string.Equals(t.Id, settings.ThemeId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, settings.ThemeId, StringComparison.OrdinalIgnoreCase))))
                {
                    targetId = settings.ThemeId;
                }
                else if (!string.IsNullOrWhiteSpace(settings.DarkPresetId))
                {
                    targetId = settings.DarkPresetId;
                }
                else
                {
                    targetId = !string.IsNullOrWhiteSpace(settings.ThemeId) ? settings.ThemeId : "default-dark";
                }

                baseTheme = _themes.FirstOrDefault(t => t.IsDark && (string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, targetId, StringComparison.OrdinalIgnoreCase)))
                            ?? _themes.FirstOrDefault(t => t.IsDark)
                            ?? _themes[0];
            }
        }

        var effective = baseTheme.Clone();

        if (!isDark)
        {
            var isDefaultLight = string.Equals(baseTheme.Id, "default-light", StringComparison.OrdinalIgnoreCase);
            var isPresetMatch = string.Equals(settings.LightPresetId, baseTheme.Id, StringComparison.OrdinalIgnoreCase);

            if (IsValidHexColor(settings.LightBackgroundHex))
            {
                var bg = NormalizeHex(settings.LightBackgroundHex);
                bool shouldApply = isDefaultLight
                    ? !string.Equals(bg, "#F9F9F9", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(bg, "#F9F9F9", StringComparison.OrdinalIgnoreCase) && !string.Equals(bg, NormalizeHex(baseTheme.WindowBackground), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.WindowBackground = bg;
                    effective.EditorBackground = bg;
                    effective.TabItemActiveBackground = bg;
                }
            }
            if (IsValidHexColor(settings.LightForegroundHex))
            {
                var fg = NormalizeHex(settings.LightForegroundHex);
                bool shouldApply = isDefaultLight
                    ? !string.Equals(fg, "#101010", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(fg, "#101010", StringComparison.OrdinalIgnoreCase) && !string.Equals(fg, NormalizeHex(baseTheme.Foreground), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.Foreground = fg;
                    effective.EditorForeground = fg;
                    effective.TabItemActiveForeground = fg;
                }
            }
            if (IsValidHexColor(settings.LightAccentHex))
            {
                var acc = NormalizeHex(settings.LightAccentHex);
                bool shouldApply = isDefaultLight
                    ? !string.Equals(acc, "#007ACC", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(acc, "#007ACC", StringComparison.OrdinalIgnoreCase) && !string.Equals(acc, NormalizeHex(baseTheme.AccentColor), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.AccentColor = acc;
                    effective.StatusBarBackground = acc;
                }
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
            var isDefaultDark = string.Equals(baseTheme.Id, "default-dark", StringComparison.OrdinalIgnoreCase);
            var isPresetMatch = string.Equals(settings.DarkPresetId, baseTheme.Id, StringComparison.OrdinalIgnoreCase);

            if (IsValidHexColor(settings.DarkBackgroundHex))
            {
                var bg = NormalizeHex(settings.DarkBackgroundHex);
                bool shouldApply = isDefaultDark
                    ? !string.Equals(bg, "#101010", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(bg, "#101010", StringComparison.OrdinalIgnoreCase) && !string.Equals(bg, NormalizeHex(baseTheme.WindowBackground), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.WindowBackground = bg;
                    effective.EditorBackground = bg;
                    effective.TabItemActiveBackground = bg;
                }
            }
            if (IsValidHexColor(settings.DarkForegroundHex))
            {
                var fg = NormalizeHex(settings.DarkForegroundHex);
                bool shouldApply = isDefaultDark
                    ? !string.Equals(fg, "#CCCCCC", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(fg, "#CCCCCC", StringComparison.OrdinalIgnoreCase) && !string.Equals(fg, NormalizeHex(baseTheme.Foreground), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.Foreground = fg;
                    effective.EditorForeground = fg;
                    effective.TabItemActiveForeground = fg;
                }
            }
            if (IsValidHexColor(settings.DarkAccentHex))
            {
                var acc = NormalizeHex(settings.DarkAccentHex);
                bool shouldApply = isDefaultDark
                    ? !string.Equals(acc, "#007ACC", StringComparison.OrdinalIgnoreCase)
                    : isPresetMatch && !string.Equals(acc, "#007ACC", StringComparison.OrdinalIgnoreCase) && !string.Equals(acc, NormalizeHex(baseTheme.AccentColor), StringComparison.OrdinalIgnoreCase);

                if (shouldApply)
                {
                    effective.AccentColor = acc;
                    effective.StatusBarBackground = acc;
                }
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
        if (IsValidHexColor(settings.CustomCodeVariable))
            effective.CodeVariable = NormalizeHex(settings.CustomCodeVariable!);

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
        var target = theme ?? _currentTheme ?? _themes.FirstOrDefault();
        HighlightingThemeApplicator.Apply(definition, target);
    }

    public void ApplyThemeToAllHighlightings(ColorTheme? theme = null)
    {
        var target = theme ?? _currentTheme ?? _themes.FirstOrDefault();
        HighlightingThemeApplicator.ApplyToAll(target);
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
