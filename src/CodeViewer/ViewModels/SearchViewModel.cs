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

    [ObservableProperty]
    private int _totalMatches = 0;

    [ObservableProperty]
    private int _currentMatchIndex = 0;

    public event Action? RequestFindNext;
    public event Action? RequestFindPrevious;
    public event Action? RequestClose;
    public event Action? RequestUpdateMatches;

    partial void OnSearchTextChanged(string value)
    {
        RequestUpdateMatches?.Invoke();
    }

    partial void OnMatchCaseChanged(bool value)
    {
        RequestUpdateMatches?.Invoke();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            RequestUpdateMatches?.Invoke();
        }
        else
        {
            StatusText = string.Empty;
            RequestClose?.Invoke();
        }
    }

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
