using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CodeViewer.Models;
using CodeViewer.Rendering;
using CodeViewer.Services;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

public partial class MainWindow : Window
{
    private readonly CurrentLineHighlighter _currentLineHighlighter;
    private readonly BracketMatchingRenderer _bracketMatchingRenderer;
    private readonly SearchHighlightRenderer _searchHighlightRenderer;
    private readonly List<int> _searchMatchOffsets = new();
    private int _currentSearchMatchIndex = -1;

    public MainWindow()
    {
        InitializeComponent();

        _currentLineHighlighter = new CurrentLineHighlighter(Editor);
        _bracketMatchingRenderer = new BracketMatchingRenderer(Editor);
        _searchHighlightRenderer = new SearchHighlightRenderer(Editor);

        Editor.TextArea.TextView.BackgroundRenderers.Add(_currentLineHighlighter);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_bracketMatchingRenderer);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_searchHighlightRenderer);

        DataContextChanged += OnDataContextChanged;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Editor.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        Editor.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        // Editor caret position tracking
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        // Ctrl + Mouse Wheel Zooming on Editor
        Editor.AddHandler(PointerWheelChangedEvent, (s, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && DataContext is MainViewModel vm)
            {
                if (e.Delta.Y > 0)
                {
                    vm.ZoomIn();
                }
                else if (e.Delta.Y < 0)
                {
                    vm.ZoomOut();
                }
                e.Handled = true;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        // Window closing prompt for unsaved changes
        Closing += OnWindowClosing;
        Closed += (s, e) => (DataContext as IDisposable)?.Dispose();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Wire search events to editor actions
            vm.SearchViewModel.RequestUpdateMatches += UpdateSearchMatches;
            vm.SearchViewModel.RequestFindNext += OnFindNext;
            vm.SearchViewModel.RequestFindPrevious += OnFindPrevious;
            vm.SearchViewModel.RequestClose += () =>
            {
                _searchHighlightRenderer.Clear();
                Editor.TextArea.TextView.InvalidateVisual();
                Editor.Focus();
            };

            // Wire theme change listener for guaranteed instant visual update
            vm.ThemeService.ThemeChanged += OnThemeChanged;
            OnThemeChanged(vm.CurrentTheme);

            // Wire clipboard copy callback
            vm.RequestSetClipboardText = async (text) =>
            {
                if (Clipboard != null)
                {
                    await Clipboard.SetTextAsync(text);
                }
            };

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
            vm.RequestOpenSettings = async () =>
            {
                var dialog = new SettingsDialog(new SettingsViewModel(vm.SettingsService, vm.ThemeService));
                await dialog.ShowDialog(this);
            };

            // Keep word wrap, bracket matching, and search updated on active document change
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.ActiveDocument))
                {
                    if (vm.ActiveDocument != null)
                    {
                        Editor.WordWrap = vm.ActiveDocument.WordWrap;
                        _bracketMatchingRenderer.UpdateMatchingBrackets();
                        UpdateSearchMatches();
                        Editor.TextArea.TextView.InvalidateVisual();
                    }
                }
            };

            vm.SettingsService.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(vm.SettingsService.CurrentSettings);
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var fontName = AppSettings.SanitizeFontFamily(settings.FontFamily);
                var ff = new Avalonia.Media.FontFamily(fontName);

                Editor.FontFamily = ff;
                Editor.TextArea.FontFamily = ff;
                Editor.FontSize = settings.FontSize;
                Editor.WordWrap = settings.WordWrap;
                Editor.ShowLineNumbers = settings.ShowLineNumbers;
                Editor.Options.IndentationSize = settings.TabSize;

                Editor.TextArea.TextView.Redraw();
            }
            catch
            {
                // Fallback handled
            }
        });
    }

    private void OnLanguageItemClick(object? sender, RoutedEventArgs e)
    {
        LanguageButton?.Flyout?.Hide();
    }

    private void OnThemeChanged(ColorTheme theme)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var winBg = new SolidColorBrush(Color.Parse(theme.WindowBackground));
                var editorBg = new SolidColorBrush(Color.Parse(theme.EditorBackground));
                var editorFg = new SolidColorBrush(Color.Parse(theme.Foreground));
                var lineNumbersFg = new SolidColorBrush(Color.Parse(theme.LineNumbersForeground));
                var statusBg = new SolidColorBrush(Color.Parse(theme.StatusBarBackground));
                var tabBg = new SolidColorBrush(Color.Parse(theme.TabBarBackground));

                Background = winBg;
                Editor.Background = editorBg;
                Editor.Foreground = editorFg;
                Editor.LineNumbersForeground = lineNumbersFg;

                if (StatusBarBorder != null) StatusBarBorder.Background = statusBg;
                if (TabBarBorder != null) TabBarBorder.Background = tabBg;

                _currentLineHighlighter?.UpdateTheme(theme.IsDark);
                _bracketMatchingRenderer?.UpdateTheme(theme);

                if (DataContext is MainViewModel vm)
                {
                    vm.ApplyCodeColorsToAllDocuments(theme);
                    Editor.TextArea.TextView.Redraw();
                }
            }
            catch
            {
                // Fallback handled
            }
        });
    }

    private void OnSelectThemeMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string themeId && DataContext is MainViewModel vm)
        {
            vm.SelectThemeById(themeId);
        }
    }

    #region Window Keyboard Shortcuts Handler

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Ctrl + S: Save
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
            {
                _ = vm.SaveCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }

            // Ctrl + Shift + S: Save As
            if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.S)
            {
                _ = vm.SaveAsCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }

            // Ctrl + N: New File
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N)
            {
                vm.CreateNewDocument();
                e.Handled = true;
                return;
            }

            // Ctrl + O: Open File
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O)
            {
                _ = vm.OpenFileCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }

            // Ctrl + W: Close Active Tab
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W)
            {
                _ = vm.CloseTabCommand.ExecuteAsync(vm.ActiveDocument);
                e.Handled = true;
                return;
            }

            // Ctrl + F: Find in File
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
            {
                vm.ShowSearch();
                e.Handled = true;
                return;
            }

            // F3 / Shift + F3: Find Next / Previous
            if (e.Key == Key.F3)
            {
                if (e.KeyModifiers == KeyModifiers.Shift)
                {
                    vm.SearchViewModel.FindPrevious();
                }
                else if (e.KeyModifiers == KeyModifiers.None)
                {
                    vm.SearchViewModel.FindNext();
                }
                e.Handled = true;
                return;
            }

            // Ctrl + Plus / Ctrl + = : Zoom In
            if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                vm.ZoomIn();
                e.Handled = true;
                return;
            }

            // Ctrl + Minus: Zoom Out
            if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                vm.ZoomOut();
                e.Handled = true;
                return;
            }

            // Ctrl + 0: Reset Zoom
            if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                vm.ResetZoom();
                e.Handled = true;
                return;
            }

            // Alt + Z: Toggle Word Wrap
            if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Z)
            {
                vm.ToggleWordWrap();
                e.Handled = true;
                return;
            }

            // Ctrl + , : Settings
            if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemComma || e.Key == Key.OemPeriod))
            {
                vm.ShowSettings();
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    #endregion

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.ActiveDocument != null)
        {
            var caret = Editor.TextArea.Caret;
            vm.ActiveDocument.UpdateCaretPosition(caret.Line, caret.Column);
            _bracketMatchingRenderer.UpdateMatchingBrackets();
            Editor.TextArea.TextView.InvalidateVisual();
        }
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DocumentViewModel doc && DataContext is MainViewModel vm)
        {
            vm.ActiveDocument = doc;
            Editor.WordWrap = doc.WordWrap;
            _bracketMatchingRenderer.UpdateMatchingBrackets();
            UpdateSearchMatches();
            Editor.TextArea.TextView.InvalidateVisual();
            Editor.Focus();
        }
    }

    #region Drag & Drop Support

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files) || e.Data.GetFiles() != null || e.Data.Contains(DataFormats.Text))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var pathsToOpen = new System.Collections.Generic.List<string>();

        // 1. Check Avalonia IStorageItem files
        var files = e.Data.GetFiles();
        if (files != null)
        {
            foreach (var item in files)
            {
                var path = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                if (!string.IsNullOrEmpty(path))
                {
                    pathsToOpen.Add(path);
                }
            }
        }

        // 2. Check Windows DataFormats.Files string paths
        if (pathsToOpen.Count == 0 && e.Data.Contains(DataFormats.Files))
        {
            var raw = e.Data.Get(DataFormats.Files);
            if (raw is System.Collections.Generic.IEnumerable<string> strList)
            {
                pathsToOpen.AddRange(strList);
            }
            else if (raw is System.Collections.Generic.IEnumerable<IStorageItem> storageList)
            {
                foreach (var item in storageList)
                {
                    var path = item.TryGetLocalPath() ?? (item.Path.IsFile ? item.Path.LocalPath : null);
                    if (!string.IsNullOrEmpty(path)) pathsToOpen.Add(path);
                }
            }
        }

        // 3. Check Text URI format
        if (pathsToOpen.Count == 0 && e.Data.Contains(DataFormats.Text))
        {
            var text = e.Data.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var clean = line.Trim().Trim('"', '\'');
                    if (clean.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(clean, UriKind.Absolute, out var uri))
                    {
                        clean = uri.LocalPath;
                    }
                    if (File.Exists(clean) || Directory.Exists(clean))
                    {
                        pathsToOpen.Add(clean);
                    }
                }
            }
        }

        if (pathsToOpen.Count > 0)
        {
            e.Handled = true;
            foreach (var path in pathsToOpen)
            {
                if (File.Exists(path))
                {
                    await vm.OpenFileInternalAsync(path);
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        var subFiles = Directory.GetFiles(path).Take(10);
                        foreach (var sub in subFiles)
                        {
                            await vm.OpenFileInternalAsync(sub);
                        }
                    }
                    catch
                    {
                        // Directory access fallback
                    }
                }
            }
        }
    }

    #endregion

    #region Search Implementation

    private void UpdateSearchMatches()
    {
        if (DataContext is not MainViewModel vm) return;

        var query = vm.SearchViewModel.SearchText;
        _searchMatchOffsets.Clear();
        _currentSearchMatchIndex = -1;

        if (string.IsNullOrEmpty(query) || !vm.SearchViewModel.IsOpen)
        {
            _searchHighlightRenderer.Clear();
            Editor.TextArea.TextView.InvalidateVisual();
            vm.SearchViewModel.StatusText = string.Empty;
            vm.SearchViewModel.TotalMatches = 0;
            vm.SearchViewModel.CurrentMatchIndex = 0;
            return;
        }

        var text = Editor.Text ?? string.Empty;
        if (text.Length == 0)
        {
            _searchHighlightRenderer.Clear();
            Editor.TextArea.TextView.InvalidateVisual();
            vm.SearchViewModel.StatusText = "No results";
            vm.SearchViewModel.TotalMatches = 0;
            vm.SearchViewModel.CurrentMatchIndex = 0;
            return;
        }

        var comparison = vm.SearchViewModel.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(query, index, comparison);
            if (found == -1) break;
            _searchMatchOffsets.Add(found);
            index = found + Math.Max(query.Length, 1);
        }

        vm.SearchViewModel.TotalMatches = _searchMatchOffsets.Count;

        if (_searchMatchOffsets.Count == 0)
        {
            _searchHighlightRenderer.Clear();
            vm.SearchViewModel.StatusText = "No results";
            vm.SearchViewModel.CurrentMatchIndex = 0;
        }
        else
        {
            var caret = Editor.SelectionStart;
            var closest = _searchMatchOffsets.FindIndex(m => m >= caret);
            if (closest == -1) closest = 0;
            _currentSearchMatchIndex = closest;

            vm.SearchViewModel.CurrentMatchIndex = _currentSearchMatchIndex + 1;
            vm.SearchViewModel.StatusText = $"{_currentSearchMatchIndex + 1} of {_searchMatchOffsets.Count}";

            var activeOffset = _searchMatchOffsets[_currentSearchMatchIndex];
            _searchHighlightRenderer.SetMatches(_searchMatchOffsets, query.Length, activeOffset);
        }

        Editor.TextArea.TextView.InvalidateVisual();
    }

    private void OnFindNext()
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(vm.SearchViewModel.SearchText))
        {
            return;
        }

        if (_searchMatchOffsets.Count == 0)
        {
            UpdateSearchMatches();
            if (_searchMatchOffsets.Count == 0) return;
        }

        _currentSearchMatchIndex = (_currentSearchMatchIndex + 1) % _searchMatchOffsets.Count;
        NavigateToMatch(vm, _currentSearchMatchIndex);
    }

    private void OnFindPrevious()
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(vm.SearchViewModel.SearchText))
        {
            return;
        }

        if (_searchMatchOffsets.Count == 0)
        {
            UpdateSearchMatches();
            if (_searchMatchOffsets.Count == 0) return;
        }

        _currentSearchMatchIndex = (_currentSearchMatchIndex - 1 + _searchMatchOffsets.Count) % _searchMatchOffsets.Count;
        NavigateToMatch(vm, _currentSearchMatchIndex);
    }

    private void NavigateToMatch(MainViewModel vm, int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= _searchMatchOffsets.Count) return;

        var offset = _searchMatchOffsets[matchIndex];
        var query = vm.SearchViewModel.SearchText;

        Editor.Select(offset, query.Length);
        var loc = Editor.Document.GetLocation(offset);
        Editor.ScrollTo(loc.Line, loc.Column);

        vm.SearchViewModel.CurrentMatchIndex = matchIndex + 1;
        vm.SearchViewModel.StatusText = $"{matchIndex + 1} of {_searchMatchOffsets.Count}";

        _searchHighlightRenderer.SetActiveMatch(offset);
        Editor.TextArea.TextView.InvalidateVisual();
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
                return;
            }
        }

        Closing -= OnWindowClosing;
        _ = vm.SaveCurrentSessionAsync();
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
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "Code Viewer", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Avalonia.Media.Brushes.White, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Version 1.0.1-rc.1 (Windows Native & Open Source)", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Fast, lightweight code viewer and editor with themes and plugins.", FontSize = 12, Foreground = Avalonia.Media.Brushes.LightGray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 10) });

        var okBtn = new Button { Content = "OK", Width = 80, CornerRadius = new Avalonia.CornerRadius(4), Background = new SolidColorBrush(Color.Parse("#007ACC")), Foreground = Avalonia.Media.Brushes.White, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
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
                FileName = "https://github.com/Reza2654/code-viewer",
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
