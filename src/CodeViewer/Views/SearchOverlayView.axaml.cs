using System;
using Avalonia.Controls;
using Avalonia.Input;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

public partial class SearchOverlayView : UserControl
{
    public SearchOverlayView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SearchViewModel.IsOpen) && vm.IsOpen)
                {
                    if (vm.IsReplaceOpen && !string.IsNullOrEmpty(vm.SearchText))
                    {
                        ReplaceTextBox.Focus();
                        ReplaceTextBox.SelectAll();
                    }
                    else
                    {
                        SearchTextBox.Focus();
                        SearchTextBox.SelectAll();
                    }
                }
                else if (args.PropertyName == nameof(SearchViewModel.IsReplaceOpen) && vm.IsReplaceOpen && vm.IsOpen)
                {
                    ReplaceTextBox.Focus();
                    ReplaceTextBox.SelectAll();
                }
            };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not SearchViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (ReplaceTextBox.IsFocused)
            {
                vm.Replace();
            }
            else
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    vm.FindPrevious();
                }
                else
                {
                    vm.FindNext();
                }
            }
            e.Handled = true;
        }
        else if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (vm.IsReplaceOpen)
            {
                vm.ReplaceAll();
                e.Handled = true;
            }
        }
    }
}
