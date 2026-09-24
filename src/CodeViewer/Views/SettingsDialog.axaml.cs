using System;
using Avalonia.Controls;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public SettingsDialog(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
