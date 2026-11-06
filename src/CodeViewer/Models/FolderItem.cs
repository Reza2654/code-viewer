using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitStatus))]
    [NotifyPropertyChangedFor(nameof(GitStatusLetter))]
    [NotifyPropertyChangedFor(nameof(GitStatusColor))]
    [NotifyPropertyChangedFor(nameof(GitStatusBrush))]
    [NotifyPropertyChangedFor(nameof(GitStatusToolTip))]
    private GitFileStatus _gitStatus = GitFileStatus.Clean;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitStatus))]
    [NotifyPropertyChangedFor(nameof(GitStatusLetter))]
    [NotifyPropertyChangedFor(nameof(GitStatusColor))]
    [NotifyPropertyChangedFor(nameof(GitStatusBrush))]
    [NotifyPropertyChangedFor(nameof(GitStatusToolTip))]
    private bool _hasModifiedChild;

    public bool HasGitStatus => GitStatus != GitFileStatus.Clean || HasModifiedChild;

    public string GitStatusLetter => GitStatus switch
    {
        GitFileStatus.Modified => "M",
        GitFileStatus.Untracked => "U",
        GitFileStatus.Added => "A",
        GitFileStatus.Deleted => "D",
        GitFileStatus.Renamed => "R",
        GitFileStatus.Conflicted => "!",
        _ => HasModifiedChild ? "●" : string.Empty
    };

    private static readonly IBrush AmberBrush = new SolidColorBrush(Color.Parse("#E2C08D"));
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#73C991"));
    private static readonly IBrush EmeraldBrush = new SolidColorBrush(Color.Parse("#A3BE8C"));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.Parse("#E06C75"));
    private static readonly IBrush BlueBrush = new SolidColorBrush(Color.Parse("#61AFEF"));
    private static readonly IBrush YellowBrush = new SolidColorBrush(Color.Parse("#E5C07B"));
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    public IBrush GitStatusBrush => GitStatus switch
    {
        GitFileStatus.Modified => AmberBrush,
        GitFileStatus.Untracked => GreenBrush,
        GitFileStatus.Added => EmeraldBrush,
        GitFileStatus.Deleted => RedBrush,
        GitFileStatus.Renamed => BlueBrush,
        GitFileStatus.Conflicted => YellowBrush,
        _ => HasModifiedChild ? AmberBrush : TransparentBrush
    };

    public string GitStatusColor => GitStatus switch
    {
        GitFileStatus.Modified => "#E2C08D",    // Amber/orange
        GitFileStatus.Untracked => "#73C991",   // Green
        GitFileStatus.Added => "#A3BE8C",       // Emerald / sage green
        GitFileStatus.Deleted => "#E06C75",     // Red
        GitFileStatus.Renamed => "#61AFEF",     // Blue
        GitFileStatus.Conflicted => "#E5C07B",  // Warning yellow
        _ => HasModifiedChild ? "#E2C08D" : "Transparent"
    };

    public string GitStatusToolTip => GitStatus switch
    {
        GitFileStatus.Modified => "Modified",
        GitFileStatus.Untracked => "Untracked",
        GitFileStatus.Added => "Added / Staged",
        GitFileStatus.Deleted => "Deleted",
        GitFileStatus.Renamed => "Renamed",
        GitFileStatus.Conflicted => "Conflicted",
        _ => HasModifiedChild ? "Contains modified files" : string.Empty
    };

    public ObservableCollection<FolderItem> Children { get; } = new();

    public FolderItem? Parent { get; set; }

    public Action<FolderItem>? ChildrenLoadedCallback { get; set; }

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
                item.ChildrenLoadedCallback = ChildrenLoadedCallback;
                Children.Add(item);
            }

            _hasLoadedChildren = true;
            ChildrenLoadedCallback?.Invoke(this);
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

