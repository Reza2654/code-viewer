using System;
using System.IO;

namespace CodeViewer.Configuration;

/// <summary>
/// Safe loader for client-side configuration (.env or environment variables).
/// </summary>
public static class EnvConfigLoader
{
    public static AppConfig LoadConfig()
    {
        var config = new AppConfig();
        var envFilePath = FindEnvFile();

        if (string.IsNullOrEmpty(envFilePath) || !File.Exists(envFilePath))
        {
            return config;
        }

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            // Strip optional quotes
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1].Trim();
            }

            if (string.Equals(key, "DEFAULT_FONT_SIZE", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, out var fontSize))
            {
                config.DefaultFontSize = fontSize;
            }
            else if (string.Equals(key, "WORD_WRAP", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var wordWrap))
            {
                config.WordWrap = wordWrap;
            }
            else if (string.Equals(key, "SHOW_LINE_NUMBERS", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var showLineNumbers))
            {
                config.ShowLineNumbers = showLineNumbers;
            }
            else if (string.Equals(key, "MAX_RECENT_FILES", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var maxRecent))
            {
                config.MaxRecentFiles = maxRecent;
            }
        }

        return config;
    }

    private static string? FindEnvFile()
    {
        // 1. Current working directory
        var cwdEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(cwdEnv))
        {
            return cwdEnv;
        }

        // 2. Application base directory
        var appBaseEnv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        if (File.Exists(appBaseEnv))
        {
            return appBaseEnv;
        }

        // 3. Search up parent directories
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            currentDir = currentDir.Parent;
        }

        return null;
    }
}
