using Avalonia.Controls;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

public partial class UpdateDialog : Window
{
    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(UpdateViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
    }
}
