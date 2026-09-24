using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CodeViewer.Services;

/// <summary>
/// Native Avalonia dialog implementation using StorageProvider and custom dialog windows.
/// </summary>
public class DialogService : IDialogService
{
    private Window? _ownerWindow;

    public void Initialize(Window window)
    {
        _ownerWindow = window;
    }

    public async Task<string?> ShowOpenFileDialogAsync()
    {
        if (_ownerWindow?.StorageProvider == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = "Open Source File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("All Supported Code Files")
                {
                    Patterns = new[]
                    {
                        "*.dart", "*.cs", "*.csx", "*.c", "*.h", "*.cpp", "*.hpp", "*.cxx",
                        "*.java", "*.kt", "*.kts", "*.js", "*.jsx", "*.ts", "*.tsx",
                        "*.py", "*.pyw", "*.html", "*.htm", "*.css", "*.scss", "*.sass",
                        "*.json", "*.xml", "*.axaml", "*.xaml", "*.yaml", "*.yml",
                        "*.md", "*.markdown", "*.sql", "*.ps1", "*.sh", "*.bash", "*.rs", "*.go"
                    }
                },
                new("All Files") { Patterns = new[] { "*.*" } }
            }
        };

        var results = await _ownerWindow.StorageProvider.OpenFilePickerAsync(options);
        var file = results.FirstOrDefault();
        return file?.TryGetLocalPath() ?? (file?.Path.IsFile == true ? file.Path.LocalPath : null);
    }

    public async Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions)
    {
        if (_ownerWindow?.StorageProvider == null) return null;

        var patterns = extensions.Select(e => e.StartsWith("*.") ? e : (e.StartsWith('.') ? $"*{e}" : $"*.{e}")).ToArray();

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new(filterName) { Patterns = patterns },
                new("All Files") { Patterns = new[] { "*.*" } }
            }
        };

        var results = await _ownerWindow.StorageProvider.OpenFilePickerAsync(options);
        var file = results.FirstOrDefault();
        return file?.TryGetLocalPath() ?? (file?.Path.IsFile == true ? file.Path.LocalPath : null);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string defaultFileName)
    {
        if (_ownerWindow?.StorageProvider == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = "Save File As",
            SuggestedFileName = defaultFileName,
            DefaultExtension = Path.GetExtension(defaultFileName)
        };

        var file = await _ownerWindow.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath() ?? (file?.Path.IsFile == true ? file.Path.LocalPath : null);
    }

    public async Task<ConfirmResult> ShowSaveConfirmationAsync(string documentTitle)
    {
        if (_ownerWindow == null) return ConfirmResult.Cancel;

        var tcs = new TaskCompletionSource<ConfirmResult>();

        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            ShowInTaskbar = false
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        var message = new TextBlock
        {
            Text = $"Do you want to save changes to \"{documentTitle}\"?",
            Foreground = Brushes.White,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var btnSave = new Button
        {
            Content = "Save",
            Width = 80,
            Background = new SolidColorBrush(Color.Parse("#007ACC")),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        btnSave.Click += (_, _) => { tcs.TrySetResult(ConfirmResult.Save); dialog.Close(); };

        var btnDontSave = new Button
        {
            Content = "Don't Save",
            Width = 90,
            Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        btnDontSave.Click += (_, _) => { tcs.TrySetResult(ConfirmResult.DontSave); dialog.Close(); };

        var btnCancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        btnCancel.Click += (_, _) => { tcs.TrySetResult(ConfirmResult.Cancel); dialog.Close(); };

        buttonsPanel.Children.Add(btnSave);
        buttonsPanel.Children.Add(btnDontSave);
        buttonsPanel.Children.Add(btnCancel);

        panel.Children.Add(message);
        panel.Children.Add(buttonsPanel);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(ConfirmResult.Cancel);

        await dialog.ShowDialog(_ownerWindow);
        return await tcs.Task;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        if (_ownerWindow == null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            ShowInTaskbar = false
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        var textBlock = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };

        var btnOk = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new SolidColorBrush(Color.Parse("#007ACC")),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        btnOk.Click += (_, _) => dialog.Close();

        panel.Children.Add(textBlock);
        panel.Children.Add(btnOk);

        dialog.Content = panel;
        await dialog.ShowDialog(_ownerWindow);
    }
}
