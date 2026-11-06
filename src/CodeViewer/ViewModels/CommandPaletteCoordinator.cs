using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CodeViewer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

/// <summary>
/// Coordinates command palette state, search filtering, selection, and command execution.
/// </summary>
public partial class CommandPaletteCoordinator : ViewModelBase
{
    private readonly List<CommandPaletteItem> _allCommands = [];

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CommandPaletteItem> _filteredCommands = [];

    [ObservableProperty]
    private CommandPaletteItem? _selectedItem;

    public IReadOnlyList<CommandPaletteItem> AllCommands => _allCommands;

    public void RegisterCommand(CommandPaletteItem command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _allCommands.Add(command);
    }

    public void RegisterCommands(IEnumerable<CommandPaletteItem> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        foreach (var cmd in commands)
        {
            RegisterCommand(cmd);
        }
    }

    public void ClearCommands()
    {
        _allCommands.Clear();
        FilteredCommands.Clear();
        SelectedItem = null;
    }

    [RelayCommand]
    public void Show()
    {
        Query = string.Empty;
        Filter(string.Empty);
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    partial void OnQueryChanged(string value)
    {
        Filter(value);
    }

    public void Filter(string query)
    {
        FilteredCommands.Clear();
        var q = query?.Trim() ?? string.Empty;
        var matches = string.IsNullOrEmpty(q)
            ? _allCommands
            : _allCommands.Where(c =>
                c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.ShortcutText.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var cmd in matches)
        {
            FilteredCommands.Add(cmd);
        }

        SelectedItem = FilteredCommands.FirstOrDefault();
    }

    [RelayCommand]
    public void ExecuteSelected()
    {
        var target = SelectedItem;
        IsOpen = false;
        target?.Action?.Invoke();
    }

    public bool ExecuteCommandById(string commandId)
    {
        var cmd = _allCommands.FirstOrDefault(c => string.Equals(c.Id, commandId, StringComparison.OrdinalIgnoreCase));
        if (cmd?.Action != null)
        {
            cmd.Action.Invoke();
            return true;
        }
        return false;
    }
}
