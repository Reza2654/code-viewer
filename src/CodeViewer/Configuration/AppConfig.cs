using System;

namespace CodeViewer.Configuration;

/// <summary>
/// Application-wide configuration settings for Code Viewer.
/// </summary>
public class AppConfig
{
    // Editor defaults
    public double DefaultFontSize { get; set; } = 14.0;
    public double MinFontSize { get; set; } = 8.0;
    public double MaxFontSize { get; set; } = 36.0;
    public string DefaultFontFamily { get; set; } = "Cascadia Code, Consolas, Courier New, monospace";
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public int MaxRecentFiles { get; set; } = 20;

    // Large file handling threshold (5 MB)
    public long LargeFileThresholdBytes { get; set; } = 5 * 1024 * 1024;
}
