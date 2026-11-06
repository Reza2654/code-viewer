using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace CodeViewer.Views;

/// <summary>
/// Handles drag-and-drop operations for opening files and folders in Code Viewer.
/// </summary>
public static class FileDragDropHandler
{
    public static void HandleDragOver(DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files) || e.Data.GetFiles() != null || e.Data.Contains(DataFormats.Text))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    public static async Task HandleDropAsync(DragEventArgs e, Func<string, Task> openFileCallback)
    {
        var pathsToOpen = ResolveDroppedPaths(e.Data);
        if (pathsToOpen.Count > 0)
        {
            e.Handled = true;
            foreach (var path in pathsToOpen)
            {
                if (File.Exists(path))
                {
                    await openFileCallback(path);
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        var subFiles = Directory.GetFiles(path).Take(10);
                        foreach (var sub in subFiles)
                        {
                            await openFileCallback(sub);
                        }
                    }
                    catch
                    {
                        // Directory access fallback
                    }
                }
            }
        }
    }

    public static List<string> ResolveDroppedPaths(IDataObject data)
    {
        var pathsToOpen = new List<string>();

        // 1. Check Avalonia IStorageItem files
        var files = data.GetFiles();
        if (files != null)
        {
            foreach (var item in files)
            {
                var path = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                if (!string.IsNullOrEmpty(path))
                {
                    pathsToOpen.Add(path);
                }
            }
        }

        // 2. Check Windows DataFormats.Files string paths
        if (pathsToOpen.Count == 0 && data.Contains(DataFormats.Files))
        {
            var raw = data.Get(DataFormats.Files);
            if (raw is IEnumerable<string> strList)
            {
                pathsToOpen.AddRange(strList);
            }
            else if (raw is IEnumerable<IStorageItem> storageList)
            {
                foreach (var item in storageList)
                {
                    var path = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                    if (!string.IsNullOrEmpty(path)) pathsToOpen.Add(path);
                }
            }
        }

        // 3. Check Text URI format
        if (pathsToOpen.Count == 0 && data.Contains(DataFormats.Text))
        {
            var text = data.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var clean = line.Trim().Trim('"', '\'');
                    if (clean.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(clean, UriKind.Absolute, out var uri))
                    {
                        clean = uri.LocalPath;
                    }
                    if (File.Exists(clean) || Directory.Exists(clean))
                    {
                        pathsToOpen.Add(clean);
                    }
                }
            }
        }

        return pathsToOpen;
    }
}
