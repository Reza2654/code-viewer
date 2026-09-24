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
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                }
            };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            if (DataContext is SearchViewModel vm)
            {
                vm.Close();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            if (DataContext is SearchViewModel vm)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    vm.FindPrevious();
                }
                else
                {
                    vm.FindNext();
                }
                e.Handled = true;
            }
        }
    }
}
