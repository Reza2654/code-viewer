using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

/// <summary>
/// Coordinates workspace folder tree state, explorer sidebar, and file/folder manipulation.
/// </summary>
public partial class WorkspaceCoordinator : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly IRecentWorkspacesService _recentWorkspacesService;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private FolderItem? _rootFolder;

    [ObservableProperty]
    private FolderItem? _selectedFolderItem;

    [ObservableProperty]
    private ObservableCollection<RecentWorkspaceItem> _recentWorkspaces = [];

    public bool HasRecentWorkspaces => RecentWorkspaces.Count > 0;

    // Callbacks for orchestration
    public Func<string, Task>? RequestOpenFile { get; set; }
    public Func<string, Task>? RequestCloseFile { get; set; }
    public Action<string, string>? NotifyFileRenamed { get; set; }
    public Func<string, Task>? RequestStatusMessage { get; set; }
    public Func<string, Task>? RequestSetClipboardText { get; set; }
    public Action? WorkspaceChanged { get; set; }

    public WorkspaceCoordinator(
        IDialogService dialogService,
        IRecentWorkspacesService recentWorkspacesService)
    {
        _dialogService = dialogService;
        _recentWorkspacesService = recentWorkspacesService;
    }

    public async Task LoadRecentWorkspacesAsync()
    {
        var items = await _recentWorkspacesService.GetRecentWorkspacesAsync();
        RecentWorkspaces.Clear();
        foreach (var item in items)
        {
            item.OpenCommand = new AsyncRelayCommand(() => OpenRecentWorkspaceAsync(item.FolderPath));
            RecentWorkspaces.Add(item);
        }
        OnPropertyChanged(nameof(HasRecentWorkspaces));
    }

    public async Task AddRecentWorkspaceAsync(string folderPath)
    {
        await _recentWorkspacesService.AddRecentWorkspaceAsync(folderPath);
        await LoadRecentWorkspacesAsync();
    }

    [RelayCommand]
    public async Task OpenRecentWorkspaceAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;
        if (Directory.Exists(folderPath))
        {
            OpenFolder(folderPath);
        }
        else
        {
            await _dialogService.ShowMessageAsync("Folder Not Found", $"The workspace folder '{folderPath}' no longer exists.");
            await _recentWorkspacesService.RemoveRecentWorkspaceAsync(folderPath);
            await LoadRecentWorkspacesAsync();
        }
    }

    [RelayCommand]
    public async Task ClearRecentWorkspacesAsync()
    {
        await _recentWorkspacesService.ClearAsync();
        RecentWorkspaces.Clear();
        OnPropertyChanged(nameof(HasRecentWorkspaces));
    }

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        var folderPath = await _dialogService.ShowOpenFolderDialogAsync();
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            OpenFolder(folderPath);
        }
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;
        var fullPath = Path.GetFullPath(folderPath);
        var root = new FolderItem(fullPath, true)
        {
            IsExpanded = true
        };
        RootFolder = root;
        IsSidebarOpen = true;
        _ = AddRecentWorkspaceAsync(fullPath);
        WorkspaceChanged?.Invoke();
        if (RequestStatusMessage != null)
        {
            _ = RequestStatusMessage($"Opened workspace: {root.Name}");
        }
    }

    [RelayCommand]
    public void CloseFolder()
    {
        RootFolder = null;
        WorkspaceChanged?.Invoke();
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    public async Task SelectFolderItemAsync(FolderItem? item)
    {
        if (item == null) return;
        SelectedFolderItem = item;
        if (!item.IsDirectory && !string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
        {
            if (RequestOpenFile != null)
            {
                await RequestOpenFile(item.FullPath);
            }
        }
        else if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    [RelayCommand]
    public async Task CreateFileInFolderAsync(FolderItem? item)
    {
        var targetFolder = item?.IsDirectory == true ? item : (item?.Parent ?? RootFolder);
        if (targetFolder == null || string.IsNullOrEmpty(targetFolder.FullPath) || !Directory.Exists(targetFolder.FullPath))
        {
            await _dialogService.ShowMessageAsync("New File", "Please open a workspace folder first.");
            return;
        }

        var fileName = await _dialogService.ShowPromptAsync("New File", $"Create new file in '{targetFolder.Name}':", "untitled.txt", "e.g. main.dart");
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var fullPath = Path.Combine(targetFolder.FullPath, fileName.Trim());
        try
        {
            if (File.Exists(fullPath))
            {
                await _dialogService.ShowMessageAsync("File Exists", $"A file named '{fileName}' already exists in this folder.");
                return;
            }

            File.WriteAllText(fullPath, string.Empty);
            await targetFolder.RefreshAsync();
            if (RequestOpenFile != null)
            {
                await RequestOpenFile(fullPath);
            }
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"Created {fileName}");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error", $"Could not create file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CreateFolderInFolderAsync(FolderItem? item)
    {
        var targetFolder = item?.IsDirectory == true ? item : (item?.Parent ?? RootFolder);
        if (targetFolder == null || string.IsNullOrEmpty(targetFolder.FullPath) || !Directory.Exists(targetFolder.FullPath))
        {
            await _dialogService.ShowMessageAsync("New Folder", "Please open a workspace folder first.");
            return;
        }

        var folderName = await _dialogService.ShowPromptAsync("New Folder", $"Create new folder in '{targetFolder.Name}':", "NewFolder", "e.g. src");
        if (string.IsNullOrWhiteSpace(folderName)) return;

        var fullPath = Path.Combine(targetFolder.FullPath, folderName.Trim());
        try
        {
            if (Directory.Exists(fullPath))
            {
                await _dialogService.ShowMessageAsync("Folder Exists", $"A folder named '{folderName}' already exists.");
                return;
            }

            Directory.CreateDirectory(fullPath);
            await targetFolder.RefreshAsync();
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"Created folder {folderName}");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Error", $"Could not create folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task RenameFolderItemAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var oldName = target.Name;
        var newName = await _dialogService.ShowPromptAsync("Rename", $"Enter new name for '{oldName}':", oldName);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), oldName, StringComparison.Ordinal)) return;

        newName = newName.Trim();
        var parentDir = Path.GetDirectoryName(target.FullPath);
        if (string.IsNullOrEmpty(parentDir)) return;

        var newFullPath = Path.Combine(parentDir, newName);
        try
        {
            if (target.IsDirectory)
            {
                if (Directory.Exists(newFullPath))
                {
                    await _dialogService.ShowMessageAsync("Rename Error", $"A folder named '{newName}' already exists.");
                    return;
                }
                Directory.Move(target.FullPath, newFullPath);
            }
            else
            {
                if (File.Exists(newFullPath))
                {
                    await _dialogService.ShowMessageAsync("Rename Error", $"A file named '{newName}' already exists.");
                    return;
                }
                File.Move(target.FullPath, newFullPath);
                NotifyFileRenamed?.Invoke(target.FullPath, newFullPath);
            }

            if (target.Parent != null)
            {
                await target.Parent.RefreshAsync();
            }
            else if (RootFolder != null)
            {
                await RootFolder.RefreshAsync();
            }

            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"Renamed to {newName}");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Rename Failed", ex.Message);
        }
    }

    [RelayCommand]
    public async Task DeleteFolderItemAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var itemType = target.IsDirectory ? "folder" : "file";
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirm Delete",
            $"Are you sure you want to delete the {itemType} '{target.Name}'?\nThis action cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            if (target.IsDirectory)
            {
                if (Directory.Exists(target.FullPath))
                {
                    Directory.Delete(target.FullPath, recursive: true);
                }
            }
            else
            {
                if (File.Exists(target.FullPath))
                {
                    File.Delete(target.FullPath);
                }

                if (RequestCloseFile != null)
                {
                    await RequestCloseFile(target.FullPath);
                }
            }

            if (target.Parent != null)
            {
                await target.Parent.RefreshAsync();
            }
            else if (RootFolder != null)
            {
                await RootFolder.RefreshAsync();
            }

            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"Deleted {target.Name}");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Delete Failed", ex.Message);
        }
    }

    [RelayCommand]
    public void RevealFolderItemInExplorer(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        var fullPath = target?.FullPath ?? RootFolder?.FullPath;
        if (string.IsNullOrEmpty(fullPath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                }
                else if (Directory.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"\"{fullPath}\"");
                }
            }
            else
            {
                var dir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : fullPath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
            }
        }
        catch
        {
            // Ignore
        }
    }

    [RelayCommand]
    public async Task CopyItemPathAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        var path = target?.FullPath ?? RootFolder?.FullPath;
        if (string.IsNullOrEmpty(path)) return;

        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(path);
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"✓ Copied path: {path}");
            }
        }
    }

    [RelayCommand]
    public async Task CopyItemRelativePathAsync(FolderItem? item)
    {
        var target = item ?? SelectedFolderItem;
        if (target == null || string.IsNullOrEmpty(target.FullPath)) return;

        var root = RootFolder?.FullPath ?? Environment.CurrentDirectory;
        var rel = Path.GetRelativePath(root, target.FullPath);

        if (RequestSetClipboardText != null)
        {
            await RequestSetClipboardText(rel);
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage($"✓ Copied relative path: {rel}");
            }
        }
    }

    [RelayCommand]
    public async Task RefreshFolderItemAsync(FolderItem? item)
    {
        var target = item ?? RootFolder;
        if (target != null)
        {
            await target.RefreshAsync();
            if (RequestStatusMessage != null)
            {
                _ = RequestStatusMessage("✓ Refreshed workspace");
            }
        }
    }
}
