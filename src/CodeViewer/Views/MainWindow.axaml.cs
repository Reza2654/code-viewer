using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CodeViewer.Services;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Editor caret position tracking
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        // Window closing prompt for unsaved changes
        Closing += OnWindowClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Wire search events to editor actions
            vm.SearchViewModel.RequestFindNext += OnFindNext;
            vm.SearchViewModel.RequestFindPrevious += OnFindPrevious;
            vm.SearchViewModel.RequestClose += () => Editor.Focus();

            // Wire plugin and editor interaction callbacks
            vm.RequestSelectedText = () => Editor.SelectedText ?? string.Empty;
            vm.RequestReplaceSelection = (newText) =>
            {
                var start = Editor.SelectionStart;
                var len = Editor.SelectionLength;
                if (len > 0)
                {
                    Editor.Document.Replace(start, len, newText);
                }
                else
                {
                    Editor.Document.Insert(start, newText);
                }
            };
            vm.RequestReplaceAll = (newText) =>
            {
                Editor.Document.Text = newText;
            };
            vm.RequestInsertText = (text) =>
            {
                Editor.Document.Insert(Editor.CaretOffset, text);
            };
            vm.RequestOpenPluginManager = async () =>
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeViewer", "Plugins");
                var dialog = new PluginManagerDialog(vm.Plugins, folder);
                await dialog.ShowDialog(this);
            };
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.ActiveDocument != null)
        {
            var caret = Editor.TextArea.Caret;
            vm.ActiveDocument.UpdateCaretPosition(caret.Line, caret.Column);
        }
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DocumentViewModel doc && DataContext is MainViewModel vm)
        {
            vm.ActiveDocument = doc;
            Editor.Focus();
        }
    }

    #region Drag & Drop Support

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = e.Data.GetFiles();
        if (files != null)
        {
            foreach (var item in files)
            {
                var path = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    await vm.OpenFileInternalAsync(path);
                }
            }
        }
    }

    #endregion

    #region Search Implementation

    private void OnFindNext()
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(vm.SearchViewModel.SearchText))
        {
            return;
        }

        var text = Editor.Text;
        if (string.IsNullOrEmpty(text)) return;

        var query = vm.SearchViewModel.SearchText;
        var comparison = vm.SearchViewModel.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var startIndex = Editor.SelectionStart + Editor.SelectionLength;
        if (startIndex >= text.Length)
        {
            startIndex = 0;
        }

        var index = text.IndexOf(query, startIndex, comparison);
        if (index == -1 && startIndex > 0)
        {
            // Wrap around to start
            index = text.IndexOf(query, 0, comparison);
        }

        if (index != -1)
        {
            Editor.Select(index, query.Length);
            var loc = Editor.Document.GetLocation(index);
            Editor.ScrollTo(loc.Line, loc.Column);
            vm.SearchViewModel.StatusText = string.Empty;
        }
        else
        {
            vm.SearchViewModel.StatusText = "No match";
        }
    }

    private void OnFindPrevious()
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(vm.SearchViewModel.SearchText))
        {
            return;
        }

        var text = Editor.Text;
        if (string.IsNullOrEmpty(text)) return;

        var query = vm.SearchViewModel.SearchText;
        var comparison = vm.SearchViewModel.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var startIndex = Editor.SelectionStart - 1;
        if (startIndex < 0)
        {
            startIndex = text.Length - 1;
        }

        var index = text.LastIndexOf(query, startIndex, comparison);
        if (index == -1 && startIndex < text.Length - 1)
        {
            // Wrap around to end
            index = text.LastIndexOf(query, text.Length - 1, comparison);
        }

        if (index != -1)
        {
            Editor.Select(index, query.Length);
            var loc = Editor.Document.GetLocation(index);
            Editor.ScrollTo(loc.Line, loc.Column);
            vm.SearchViewModel.StatusText = string.Empty;
        }
        else
        {
            vm.SearchViewModel.StatusText = "No match";
        }
    }

    #endregion

    #region Window Closing Verification

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var unsavedDocs = vm.Documents.Where(d => d.IsModified).ToList();
        if (unsavedDocs.Count == 0) return;

        e.Cancel = true;

        foreach (var doc in unsavedDocs)
        {
            vm.ActiveDocument = doc;
            await vm.CloseTabAsync(doc);
            if (vm.Documents.Contains(doc) && doc.IsModified)
            {
                // User cancelled closing
                return;
            }
        }

        // All saved or discarded, now safe to close
        Closing -= OnWindowClosing;
        Close();
    }

    #endregion

    #region Tools & Plugins Menu Handlers

    private void ExecutePluginById(string pluginId)
    {
        if (DataContext is MainViewModel vm)
        {
            var plugin = vm.Plugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin != null)
            {
                _ = vm.ExecutePluginCommand.ExecuteAsync(plugin);
            }
        }
    }

    private void OnJsonFormatClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-json-format");
    private void OnJsonMinifyClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-json-minify");

    private void OnSortAscClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-sort-lines-asc");
    private void OnSortDescClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-sort-lines-desc");
    private void OnRemoveDuplicatesClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-remove-duplicate-lines");
    private void OnReverseLinesClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-reverse-lines");

    private void OnBase64EncodeClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-base64-encode");
    private void OnBase64DecodeClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-base64-decode");
    private void OnUrlEncodeClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-url-encode");
    private void OnUrlDecodeClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-url-decode");

    private void OnUpperCaseClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-case-upper");
    private void OnLowerCaseClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-case-lower");
    private void OnTitleCaseClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-case-title");

    private void OnTextStatisticsClick(object? sender, RoutedEventArgs e) => ExecutePluginById("builtin-text-statistics");

    #endregion

    #region Standard Edit & Menu Handlers

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedoClick(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCutClick(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopyClick(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPasteClick(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => Editor.SelectAll();
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnShowFileInfoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.ActiveDocument != null)
        {
            var fileService = new FileService();
            var infoVm = new FileInfoViewModel(vm.ActiveDocument, fileService);
            var dialog = new FileInfoDialog { DataContext = infoVm };
            await dialog.ShowDialog(this);
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutDialog = new Window
        {
            Title = "About Code Viewer",
            Width = 400,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E")),
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "Code Viewer", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Avalonia.Media.Brushes.White, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Version 1.0 (Windows Native & Open Source)", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Fast, lightweight code viewer and editor with themes and plugins.", FontSize = 12, Foreground = Avalonia.Media.Brushes.LightGray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 10) });

        var okBtn = new Button { Content = "OK", Width = 80, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007ACC")), Foreground = Avalonia.Media.Brushes.White, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        okBtn.Click += (_, _) => aboutDialog.Close();
        panel.Children.Add(okBtn);

        aboutDialog.Content = panel;
        await aboutDialog.ShowDialog(this);
    }

    private void OnGitHubRepoClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignored
        }
    }

    #endregion
}
