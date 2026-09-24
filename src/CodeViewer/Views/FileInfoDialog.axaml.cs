using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeViewer.Views;

public partial class FileInfoDialog : Window
{
    public FileInfoDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
