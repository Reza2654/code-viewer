using System;
using System.IO;

namespace CodeViewer.Services;

/// <summary>
/// Manages Windows startup registration to enable Code Viewer background pre-warming.
/// When enabled, Code Viewer starts silently in the background at Windows login,
/// providing instantaneous sub-10ms cold launch times when files or shortcuts are clicked.
/// </summary>
public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodeViewerBackground";

    public static bool IsRunOnStartupEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetRunOnStartup(bool enable)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CodeViewer.exe");
                key.SetValue(ValueName, $"\"{exePath}\" --background");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
