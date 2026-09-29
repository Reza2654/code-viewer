using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeViewer.Models;

public partial class FolderItem : ObservableObject
{
    private bool _hasLoadedChildren;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _iconGlyph = "📄";

    public ObservableCollection<FolderItem> Children { get; } = new();

    public FolderItem? Parent { get; set; }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnExpandedChanged(value);
            }
        }
    }

    public FolderItem(string fullPath, bool isDirectory, string? name = null)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Name = name ?? (isDirectory ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : Path.GetFileName(fullPath));
        if (string.IsNullOrEmpty(Name))
        {
            Name = fullPath;
        }

        UpdateIconGlyph();

        if (isDirectory)
        {
            // Add lightweight placeholder to enable expander chevron without loading files immediately
            Children.Add(new FolderItem(string.Empty, false, "Loading...") { IsLoading = true });
        }
    }

    private void UpdateIconGlyph()
    {
        if (IsDirectory)
        {
            IconGlyph = IsExpanded ? "📂" : "📁";
            return;
        }

        var ext = Path.GetExtension(FullPath).ToLowerInvariant();
        IconGlyph = ext switch
        {
            ".agent" => "🤖",
            ".ps2" => "📜",
            ".cs" => "🔷",
            ".dart" => "🎯",
            ".py" => "🐍",
            ".js" or ".mjs" or ".cjs" => "🟨",
            ".ts" or ".tsx" => "🟦",
            ".html" or ".htm" => "🌐",
            ".css" or ".scss" or ".less" => "🎨",
            ".json" or ".xml" or ".yaml" or ".yml" or ".toml" => "⚙️",
            ".md" or ".markdown" => "📝",
            ".sql" => "🗄️",
            ".sh" or ".bash" or ".ps1" or ".cmd" or ".bat" => "💻",
            ".png" or ".jpg" or ".jpeg" or ".ico" or ".svg" => "🖼️",
            ".zip" or ".tar" or ".gz" or ".7z" => "📦",
            _ => "📄"
        };
    }

    private async void OnExpandedChanged(bool expanded)
    {
        UpdateIconGlyph();
        if (expanded && IsDirectory && !_hasLoadedChildren)
        {
            await LoadChildrenAsync();
        }
    }

    public async Task LoadChildrenAsync()
    {
        if (!IsDirectory || _hasLoadedChildren) return;

        IsLoading = true;
        try
        {
            var path = FullPath;
            if (!Directory.Exists(path)) return;

            var items = await Task.Run(() =>
            {
                var result = new System.Collections.Generic.List<FolderItem>();
                var dirInfo = new DirectoryInfo(path);

                // Enumerate directories first (filtered for speed)
                try
                {
                    foreach (var subDir in dirInfo.EnumerateDirectories())
                    {
                        var name = subDir.Name;
                        if (name.StartsWith('.') || name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("dist", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        result.Add(new FolderItem(subDir.FullName, true, subDir.Name));
                    }
                }
                catch
                {
                    // Ignore unauthorized directories
                }

                // Enumerate files
                try
                {
                    foreach (var file in dirInfo.EnumerateFiles())
                    {
                        result.Add(new FolderItem(file.FullName, false, file.Name));
                    }
                }
                catch
                {
                    // Ignore unauthorized files
                }

                return result;
            });

            Children.Clear();
            foreach (var item in items)
            {
                item.Parent = this;
                Children.Add(item);
            }

            _hasLoadedChildren = true;
        }
        catch
        {
            Children.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshAsync()
    {
        if (!IsDirectory)
        {
            if (Parent != null)
            {
                await Parent.RefreshAsync();
            }
            return;
        }

        _hasLoadedChildren = false;
        await LoadChildrenAsync();
    }
}

