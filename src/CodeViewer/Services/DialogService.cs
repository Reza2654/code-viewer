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

    public async Task<string?> ShowOpenFolderDialogAsync()
    {
        if (_ownerWindow?.StorageProvider == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        };

        var results = await _ownerWindow.StorageProvider.OpenFolderPickerAsync(options);
        var folder = results.FirstOrDefault();
        return folder?.TryGetLocalPath() ?? (folder?.Path.IsFile == false ? folder.Path.LocalPath : null);
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
        var dialog = CreateThemedModalWindow("Unsaved Changes", 420, 170);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        var message = CreateThemedBodyText($"Do you want to save changes to \"{documentTitle}\"?", 14);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var btnSave = CreateModalButton("Save", 80, isPrimary: true);
        btnSave.Click += (_, _) => { tcs.TrySetResult(ConfirmResult.Save); dialog.Close(); };

        var btnDontSave = CreateModalButton("Don't Save", 90);
        btnDontSave.Click += (_, _) => { tcs.TrySetResult(ConfirmResult.DontSave); dialog.Close(); };

        var btnCancel = CreateModalButton("Cancel", 80);
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

        var dialog = CreateThemedModalWindow(title, 420, 180);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        var textBlock = CreateThemedBodyText(message, 13);

        var btnOk = CreateModalButton("OK", 80, isPrimary: true);
        btnOk.HorizontalAlignment = HorizontalAlignment.Right;
        btnOk.Click += (_, _) => dialog.Close();

        panel.Children.Add(textBlock);
        panel.Children.Add(btnOk);

        dialog.Content = panel;
        await dialog.ShowDialog(_ownerWindow);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        if (_ownerWindow == null) return false;

        var tcs = new TaskCompletionSource<bool>();
        var dialog = CreateThemedModalWindow(title, 440, 190);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        var textBlock = CreateThemedBodyText(message, 13);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var btnConfirm = CreateModalButton(confirmText, 80, isPrimary: true);
        btnConfirm.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };

        var btnCancel = CreateModalButton(cancelText, 80);
        btnCancel.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        buttonsPanel.Children.Add(btnConfirm);
        buttonsPanel.Children.Add(btnCancel);

        panel.Children.Add(textBlock);
        panel.Children.Add(buttonsPanel);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(_ownerWindow);
        return await tcs.Task;
    }

    public async Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "", string watermark = "")
    {
        if (_ownerWindow == null) return null;

        var tcs = new TaskCompletionSource<string?>();
        var dialog = CreateThemedModalWindow(title, 440, 200);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        var textBlock = CreateThemedBodyText(message, 13);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Watermark = watermark,
            FontFamily = FontHelper.UiFontFamily,
            FontSize = 13,
            CornerRadius = new CornerRadius(4),
            Background = ResolveThemeBrush("ThemeEditorBackground", "#1E1E1E"),
            Foreground = ResolveThemeBrush("ThemeWindowForeground", "#D4D4D4"),
            BorderBrush = ResolveThemeBrush("ThemeBorderBrush", "#454545"),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var btnOk = CreateModalButton("OK", 80, isPrimary: true);

        void Confirm()
        {
            tcs.TrySetResult(textBox.Text);
            dialog.Close();
        }

        void Cancel()
        {
            tcs.TrySetResult(null);
            dialog.Close();
        }

        btnOk.Click += (_, _) => Confirm();

        var btnCancel = CreateModalButton("Cancel", 80);
        btnCancel.Click += (_, _) => Cancel();

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                Cancel();
                e.Handled = true;
            }
        };

        buttonsPanel.Children.Add(btnOk);
        buttonsPanel.Children.Add(btnCancel);

        panel.Children.Add(textBlock);
        panel.Children.Add(textBox);
        panel.Children.Add(buttonsPanel);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(null);

        dialog.Opened += (_, _) =>
        {
            textBox.Focus();
            if (!string.IsNullOrEmpty(defaultValue))
            {
                textBox.SelectionStart = 0;
                textBox.SelectionEnd = defaultValue.Length;
            }
        };

        await dialog.ShowDialog(_ownerWindow);
        return await tcs.Task;
    }

    public async Task ShowUpdateDialogAsync(AppUpdateInfo updateInfo, IAppUpdateService updateService)
    {
        if (_ownerWindow == null) return;

        var vm = new ViewModels.UpdateViewModel(updateInfo, updateService);
        var dialog = new Views.UpdateDialog(vm);
        await dialog.ShowDialog(_ownerWindow);
    }

    private static IBrush ResolveThemeBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var res) == true
            && res is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static Window CreateThemedModalWindow(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = ResolveThemeBrush("ThemeWindowBackground", "#1E1E1E"),
            Foreground = ResolveThemeBrush("ThemeWindowForeground", "#D4D4D4"),
            FontFamily = FontHelper.UiFontFamily,
            ShowInTaskbar = false
        };
    }

    private static TextBlock CreateThemedBodyText(string text, double fontSize = 13)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = ResolveThemeBrush("ThemeWindowForeground", "#D4D4D4"),
            FontFamily = FontHelper.UiFontFamily,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Button CreateModalButton(string content, double width, bool isPrimary = false)
    {
        var button = new Button
        {
            Content = content,
            Width = width,
            FontFamily = FontHelper.UiFontFamily,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = isPrimary
                ? ResolveThemeBrush("ThemeAccentBrush", "#007ACC")
                : ResolveThemeBrush("ThemeTabItemInactiveBackground", "#2D2D2D"),
            Foreground = isPrimary
                ? ResolveThemeBrush("ThemeAccentForeground", "#FFFFFF")
                : ResolveThemeBrush("ThemeWindowForeground", "#D4D4D4")
        };

        if (!isPrimary)
        {
            button.BorderBrush = ResolveThemeBrush("ThemeBorderBrush", "#454545");
            button.BorderThickness = new Thickness(1);
        }

        return button;
    }
}

