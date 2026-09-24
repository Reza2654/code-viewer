using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CodeViewer.ShellExtension;

[ComVisible(true)]
[Guid("3D5D7BF0-2394-4B2D-86BE-8A6A2C29B7F1")]
public class CodeViewerCommand : IExplorerCommand, IObjectWithSelection
{
    public static readonly Guid CommandGuid = Guid.Parse("3D5D7BF0-2394-4B2D-86BE-8A6A2C29B7F1");
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int E_FAIL = unchecked((int)0x80004005);

    private IShellItemArray? _selection;

    public int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName)
    {
        ppszName = Marshal.StringToCoTaskMemUni("Open with Code Viewer");
        return S_OK;
    }

    public int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon)
    {
        var exePath = FindCodeViewerExecutable();
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            ppszIcon = Marshal.StringToCoTaskMemUni($"{exePath},0");
            return S_OK;
        }

        ppszIcon = IntPtr.Zero;
        return S_OK;
    }

    public int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfo)
    {
        ppszInfo = IntPtr.Zero;
        return S_OK;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = CommandGuid;
        return S_OK;
    }

    public int GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pdwCmdState)
    {
        pdwCmdState = EXPCMDSTATE.ECS_ENABLED;
        return S_OK;
    }

    public int Invoke(IShellItemArray? psiItemArray, object? pbc)
    {
        try
        {
            var targetArray = psiItemArray ?? _selection;
            var paths = ExtractFilePaths(targetArray);

            var exePath = FindCodeViewerExecutable();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return E_FAIL;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty
            };

            if (paths.Count > 0)
            {
                // Format quoted arguments to preserve spaces and Unicode characters
                startInfo.Arguments = string.Join(" ", paths.Select(p => $"\"{p}\""));
            }

            Process.Start(startInfo);
            return S_OK;
        }
        catch
        {
            return E_FAIL;
        }
    }

    public int GetFlags(out EXPCMDFLAGS pdwFlags)
    {
        pdwFlags = EXPCMDFLAGS.ECF_DEFAULT;
        return S_OK;
    }

    public int EnumSubCommands(out IntPtr ppEnum)
    {
        ppEnum = IntPtr.Zero;
        return S_FALSE;
    }

    public int SetSelection(object? punk)
    {
        _selection = punk as IShellItemArray;
        return S_OK;
    }

    public int GetSelection(ref Guid riid, out object? ppv)
    {
        ppv = _selection;
        return S_OK;
    }

    private static List<string> ExtractFilePaths(IShellItemArray? itemArray)
    {
        var paths = new List<string>();
        if (itemArray == null) return paths;

        try
        {
            if (itemArray.GetCount(out var count) == S_OK)
            {
                for (uint i = 0; i < count; i++)
                {
                    if (itemArray.GetItemAt(i, out var shellItem) == S_OK && shellItem != null)
                    {
                        if (shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var ppszName) == S_OK && ppszName != IntPtr.Zero)
                        {
                            try
                            {
                                var path = Marshal.PtrToStringUni(ppszName);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    paths.Add(path);
                                }
                            }
                            finally
                            {
                                Marshal.FreeCoTaskMem(ppszName);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently handle shell enumeration issues
        }

        return paths;
    }

    public static string? FindCodeViewerExecutable()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 1. Same directory as the shell extension
        var candidate = Path.Combine(baseDir, "CodeViewer.exe");
        if (File.Exists(candidate)) return candidate;

        // 2. Parent directory (e.g. if extension is placed in a subfolder)
        var parentDir = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parentDir))
        {
            candidate = Path.Combine(parentDir, "CodeViewer.exe");
            if (File.Exists(candidate)) return candidate;
        }

        // 3. Check dist directory in development
        if (!string.IsNullOrEmpty(parentDir))
        {
            var grandParent = Path.GetDirectoryName(parentDir);
            if (!string.IsNullOrEmpty(grandParent))
            {
                candidate = Path.Combine(grandParent, "dist", "CodeViewer.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        // 4. Registry check for installed CodeViewer.exe
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Applications\CodeViewer.exe\shell\open\command");
            var cmd = key?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(cmd))
            {
                var trimmed = cmd.Trim();
                if (trimmed.StartsWith('"'))
                {
                    var endQuote = trimmed.IndexOf('"', 1);
                    if (endQuote > 1)
                    {
                        var path = trimmed.Substring(1, endQuote - 1);
                        if (File.Exists(path)) return path;
                    }
                }
            }
        }
        catch
        {
            // Ignore registry errors
        }

        return null;
    }
}
