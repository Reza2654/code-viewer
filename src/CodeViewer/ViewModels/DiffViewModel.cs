using System.Collections.ObjectModel;
using System.Linq;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

public partial class DiffViewModel : ViewModelBase
{
    private readonly IDiffService _diffService;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _oldTitle = "Original";

    [ObservableProperty]
    private string _newTitle = "Modified";

    [ObservableProperty]
    private int _additionsCount;

    [ObservableProperty]
    private int _deletionsCount;

    [ObservableProperty]
    private int _unchangedCount;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public ObservableCollection<DiffLine> DiffLines { get; } = new();

    public DiffViewModel(IDiffService? diffService = null)
    {
        _diffService = diffService ?? new DiffService();
    }

    public void LoadDiff(string oldText, string newText, string oldTitle = "Original", string newTitle = "Modified")
    {
        OldTitle = oldTitle;
        NewTitle = newTitle;

        var lines = _diffService.ComputeDiff(oldText, newText);
        DiffLines.Clear();
        int added = 0;
        int removed = 0;
        int unchanged = 0;

        foreach (var line in lines)
        {
            DiffLines.Add(line);
            if (line.Type == DiffLineType.Added) added++;
            else if (line.Type == DiffLineType.Removed) removed++;
            else unchanged++;
        }

        AdditionsCount = added;
        DeletionsCount = removed;
        UnchangedCount = unchanged;
        SummaryText = $"+{added} additions, -{removed} deletions ({unchanged} unchanged)";
        IsActive = true;
    }

    [RelayCommand]
    public void CloseDiff()
    {
        IsActive = false;
        DiffLines.Clear();
    }
}
