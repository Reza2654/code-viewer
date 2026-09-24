using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

/// <summary>
/// ViewModel for the in-editor search overlay.
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _matchCase = false;

    [ObservableProperty]
    private bool _isOpen = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public event Action? RequestFindNext;
    public event Action? RequestFindPrevious;
    public event Action? RequestClose;

    [RelayCommand]
    public void FindNext()
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            RequestFindNext?.Invoke();
        }
    }

    [RelayCommand]
    public void FindPrevious()
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            RequestFindPrevious?.Invoke();
        }
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        RequestClose?.Invoke();
    }

    public void Open(string? initialText = null)
    {
        if (!string.IsNullOrEmpty(initialText))
        {
            SearchText = initialText;
        }
        IsOpen = true;
    }
}
