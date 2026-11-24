using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public partial class MainWindow : Window, WindowKeyboardRouter.IKeyboardActionTarget
{
    private readonly CurrentLineHighlighter _currentLineHighlighter;
    private readonly BracketMatchingRenderer _bracketMatchingRenderer;
    private readonly WordOccurrenceRenderer _wordOccurrenceRenderer;
    private readonly SearchHighlightRenderer _searchHighlightRenderer;
    private readonly RainbowBracketColorizer _rainbowBracketColorizer;
    private readonly SmoothCaretBlinkController _caretBlinkController;
    private readonly EditorSearchCoordinator _searchCoordinator;
    private AvaloniaEdit.TextEditor? _splitEditor;
    private CancellationTokenSource? _markdownDebounceCts;
    private bool _isSyncingScroll;

    public MainWindow()
    {
        InitializeComponent();

        _currentLineHighlighter = new CurrentLineHighlighter(Editor);
        _bracketMatchingRenderer = new BracketMatchingRenderer(Editor);
        _wordOccurrenceRenderer = new WordOccurrenceRenderer(Editor);
        _searchHighlightRenderer = new SearchHighlightRenderer(Editor);
        _rainbowBracketColorizer = new RainbowBracketColorizer();
        _caretBlinkController = new SmoothCaretBlinkController(Editor);
        _searchCoordinator = new EditorSearchCoordinator(_searchHighlightRenderer);

        Editor.TextArea.TextView.BackgroundRenderers.Add(_currentLineHighlighter);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_bracketMatchingRenderer);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_wordOccurrenceRenderer);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_searchHighlightRenderer);
        Editor.TextArea.TextView.LineTransformers.Add(_rainbowBracketColorizer);

        Minimap.Attach(Editor);

        Editor.TextArea.TextEntering += OnEditorTextEntering;
        Editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);

        DataContextChanged += OnDataContextChanged;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Editor.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        Editor.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        // Editor caret position and selection tracking
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        Editor.TextArea.SelectionChanged += (s, e) => OnCaretPositionChanged(s, EventArgs.Empty);

        // Bi-directional scroll synchronization between Editor and Markdown preview
        Editor.TextArea.TextView.ScrollOffsetChanged += OnEditorScrollChanged;
        if (MarkdownScrollViewer != null)
        {
            MarkdownScrollViewer.ScrollChanged += OnMarkdownScrollViewerScrollChanged;
        }

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
        Closed += (s, e) =>
        {
            _markdownDebounceCts?.Cancel();
            _markdownDebounceCts?.Dispose();
            _caretBlinkController?.Dispose();
            (DataContext as IDisposable)?.Dispose();

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };

        // Code Mode Language Flyout autofocus and scroll reset
        if (LanguageButton?.Flyout is Flyout langFlyout)
        {
            langFlyout.Opened += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.LanguageFilter = string.Empty;
                }
                LanguageScrollViewer.Offset = new Avalonia.Vector(0, 0);
                LanguageSearchBox?.Focus();
                LanguageSearchBox?.SelectAll();
            };
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Wire search and replace events to editor actions
            vm.SearchViewModel.RequestUpdateMatches += UpdateSearchMatches;
            vm.SearchViewModel.RequestFindNext += OnFindNext;
            vm.SearchViewModel.RequestFindPrevious += OnFindPrevious;
            vm.SearchViewModel.RequestReplace += OnReplace;
            vm.SearchViewModel.RequestReplaceAll += OnReplaceAll;
            vm.SearchViewModel.RequestClose += () =>
            {
                _searchHighlightRenderer.Clear();
                Editor.TextArea?.TextView?.InvalidateVisual();
                Editor.Focus();
            };

            // Wire Go to Line and Comment callbacks
            vm.RequestGoToLine = (line, col) => OnGoToLine(line, col);
            vm.RequestToggleComment = ToggleComment;

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
                var dialog = new SettingsDialog(new SettingsViewModel(vm.SettingsService, vm.ThemeService, vm.AppUpdateService, vm.DialogService));
                await dialog.ShowDialog(this);
            };

            // Initial responsive layout update
            UpdateEditorLayoutColumns(vm);
            _rainbowBracketColorizer.IsEnabled = vm.SettingsService.CurrentSettings.EnableRainbowBrackets;

            Editor.TextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel { IsMarkdownPreviewActive: true } mvm)
                {
                    mvm.RefreshMarkdownStats();
                    RequestDebouncedMarkdownPreviewUpdate();
                }
                _wordOccurrenceRenderer.UpdateOccurrences();
                Editor.TextArea?.TextView?.InvalidateVisual();
            };

            // Keep word wrap, bracket matching, search, and palettes updated on property changes
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.ActiveDocument))
                {
                    UpdateEditorLayoutColumns(vm);
            _rainbowBracketColorizer.IsEnabled = vm.SettingsService.CurrentSettings.EnableRainbowBrackets;
                    if (vm.ActiveDocument != null)
                    {
                        Editor.WordWrap = vm.ActiveDocument.WordWrap;
                        FontHelper.ApplyToEditor(Editor, vm.CurrentFontFamily, vm.ActiveDocument.FontSize);
                        _caretBlinkController?.ResetSolid();
                        _bracketMatchingRenderer.UpdateMatchingBrackets();
                        _wordOccurrenceRenderer.UpdateOccurrences();
                        if (vm.SearchViewModel.IsOpen)
                        {
                            UpdateSearchMatches();
                        }
                        else
                        {
                            _searchHighlightRenderer.Clear();
                        }
                        Editor.TextArea?.TextView?.InvalidateVisual();
                    }
                    if (vm.IsMarkdownPreviewActive)
                    {
                        UpdateMarkdownPreview();
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.CurrentFontFamily))
                {
                    var settings = vm.SettingsService.CurrentSettings;
                    var fontSize = vm.ActiveDocument?.FontSize ?? settings.FontSize;
                    FontHelper.ApplyToEditor(Editor, vm.CurrentFontFamily, fontSize);
                }
                else if (args.PropertyName == nameof(MainViewModel.IsQuickOpenOpen))
                {
                    if (vm.IsQuickOpenOpen)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            QuickOpenTextBox?.Focus();
                            QuickOpenTextBox?.SelectAll();
                        });
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.IsGoToLineOpen))
                {
                    if (vm.IsGoToLineOpen)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            GoToLineTextBox?.Focus();
                            GoToLineTextBox?.SelectAll();
                        });
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.IsCommandPaletteOpen))
                {
                    if (vm.IsCommandPaletteOpen)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            CommandPaletteTextBox?.Focus();
                            CommandPaletteTextBox?.SelectAll();
                        });
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.IsGlobalSearchOpen))
                {
                    if (vm.IsGlobalSearchOpen)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            GlobalSearchTextBox?.Focus();
                            GlobalSearchTextBox?.SelectAll();
                        });
                    }
                }
                else if (args.PropertyName is nameof(MainViewModel.IsMarkdownPreviewActive) or nameof(MainViewModel.IsSplitViewActive))

                {
                    UpdateEditorLayoutColumns(vm);
            _rainbowBracketColorizer.IsEnabled = vm.SettingsService.CurrentSettings.EnableRainbowBrackets;
                    if (vm.IsMarkdownPreviewActive)
                    {
                        UpdateMarkdownPreview();
                    }
                }
            };

            vm.RunnerViewModel.RequestScrollToEnd = () =>
            {
                if (RunnerOutputTextBox != null)
                {
                    RunnerOutputTextBox.CaretIndex = RunnerOutputTextBox.Text?.Length ?? 0;
                }
            };

            vm.SettingsService.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(vm.SettingsService.CurrentSettings);
        }
    }

    private void UpdateEditorLayoutColumns(MainViewModel? vm)
    {
        if (vm == null || EditorGrid == null || EditorGrid.ColumnDefinitions.Count < 5) return;

        if (vm.IsSplitViewActive)
        {
            if (EditorGrid.ColumnDefinitions[2].Width.Value <= 0)
            {
                EditorGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        else
        {
            EditorGrid.ColumnDefinitions[2].Width = new GridLength(0);
        }

        if (vm.IsMarkdownPreviewActive)
        {
            if (EditorGrid.ColumnDefinitions[4].Width.Value <= 0)
            {
                EditorGrid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        else
        {
            EditorGrid.ColumnDefinitions[4].Width = new GridLength(0);
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                FontHelper.ApplyToEditor(Editor, settings.FontFamily, settings.FontSize);
                Editor.WordWrap = settings.WordWrap;
                Editor.ShowLineNumbers = settings.ShowLineNumbers;
                Editor.Options.IndentationSize = settings.TabSize;

                if (Editor.Options != null)
                {
                    Editor.Options.ShowSpaces = settings.ShowWhitespace;
                    Editor.Options.ShowTabs = settings.ShowWhitespace;
                }

                if (_currentLineHighlighter != null)
                {
                    _currentLineHighlighter.IsEnabled = settings.HighlightCurrentLine;
                }

                if (_bracketMatchingRenderer != null)
                {
                    _bracketMatchingRenderer.IsEnabled = settings.EnableBracketMatching;
                    _bracketMatchingRenderer.UpdateMatchingBrackets();
                }

                if (_rainbowBracketColorizer != null)
                {
                    _rainbowBracketColorizer.IsEnabled = settings.EnableRainbowBrackets;
                }

                if (_caretBlinkController != null)
                {
                    _caretBlinkController.IsEnabled = settings.SmoothCursorBlink;
                }

                if (RunnerOutputTextBox != null)
                {
                    RunnerOutputTextBox.FontFamily = FontHelper.GetFontFamily(settings.FontFamily);
                }

                _wordOccurrenceRenderer.IsEnabled = settings.HighlightWordOccurrences;
                _wordOccurrenceRenderer.UpdateOccurrences();
                Editor.TextArea?.TextView?.InvalidateVisual();
            }
            catch
            {
                // Fallback handled
            }
        });
    }

    private void OnSplitEditorLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is AvaloniaEdit.TextEditor splitEditor && DataContext is MainViewModel vm)
        {
            _splitEditor = splitEditor;
            var settings = vm.SettingsService.CurrentSettings;
            FontHelper.ApplyToEditor(splitEditor, settings.FontFamily, settings.FontSize);
            var theme = vm.CurrentTheme;
            if (theme != null)
            {
                try
                {
                    var editorBg = new SolidColorBrush(Color.Parse(theme.EditorBackground));
                    var editorFg = new SolidColorBrush(Color.Parse(theme.EditorForeground));
                    var lineNumbersFg = new SolidColorBrush(Color.Parse(theme.LineNumbersForeground));
                    splitEditor.Background = editorBg;
                    splitEditor.Foreground = editorFg;
                    if (splitEditor.TextArea != null)
                    {
                        splitEditor.TextArea.Foreground = editorFg;
                    }
                    splitEditor.LineNumbersForeground = lineNumbersFg;
                }
                catch { }
            }
        }
    }


    private void OnLanguageItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is LanguageOption option && DataContext is MainViewModel vm)
        {
            vm.SetLanguage(option.Name);
            Editor.SyntaxHighlighting = vm.ActiveDocument?.HighlightingDefinition;
            Editor.TextArea.TextView.Redraw();
        }
        LanguageButton?.Flyout?.Hide();
    }

    private void OnLanguageSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ApplyFirstFilteredLanguage();
                Editor.SyntaxHighlighting = vm.ActiveDocument?.HighlightingDefinition;
                Editor.TextArea.TextView.Redraw();
            }
            LanguageButton?.Flyout?.Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            LanguageButton?.Flyout?.Hide();
            e.Handled = true;
        }
    }

    private void OnEditorTextEntering(object? sender, TextInputEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            SmartBracketEditHandler.HandleTextEntering(Editor, e, vm.SettingsService.CurrentSettings.AutoCloseBrackets);
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            SmartBracketEditHandler.HandleKeyDown(Editor, e, vm.SettingsService.CurrentSettings.AutoCloseBrackets);
        }
    }

    private void OnThemeChanged(ColorTheme theme)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var winBg = new SolidColorBrush(Color.Parse(theme.WindowBackground));
                var editorBg = new SolidColorBrush(Color.Parse(theme.EditorBackground));
                var editorFg = new SolidColorBrush(Color.Parse(theme.EditorForeground));
                var lineNumbersFg = new SolidColorBrush(Color.Parse(theme.LineNumbersForeground));
                var statusBg = new SolidColorBrush(Color.Parse(theme.StatusBarBackground));
                var tabBg = new SolidColorBrush(Color.Parse(theme.TabBarBackground));

                Background = winBg;
                Editor.Background = editorBg;
                Editor.Foreground = editorFg;
                if (Editor.TextArea != null)
                {
                    Editor.TextArea.Foreground = editorFg;
                    if (!string.IsNullOrEmpty(theme.SelectionBackground))
                    {
                        Editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse(theme.SelectionBackground));
                    }
                }
                Editor.LineNumbersForeground = lineNumbersFg;

                if (StatusBarBorder != null) StatusBarBorder.Background = statusBg;
                if (TabBarBorder != null) TabBarBorder.Background = tabBg;

                Minimap?.UpdateTheme(theme);
                _currentLineHighlighter?.UpdateTheme(theme.IsDark);
                _bracketMatchingRenderer?.UpdateTheme(theme);
                _wordOccurrenceRenderer?.UpdateTheme(theme);
                _rainbowBracketColorizer?.UpdateTheme(theme.IsDark);

                if (DataContext is MainViewModel vm)
                {
                    vm.ApplyCodeColorsToAllDocuments(theme);
                    // Force AvaloniaEdit to recreate line transformers with updated color brushes
                    var highlight = Editor.SyntaxHighlighting;
                    Editor.SyntaxHighlighting = null;
                    Editor.SyntaxHighlighting = highlight;
                    Editor.TextArea?.TextView?.Redraw();

                    if (_splitEditor != null)
                    {
                        _splitEditor.Background = editorBg;
                        _splitEditor.Foreground = editorFg;
                        if (_splitEditor.TextArea != null)
                        {
                            _splitEditor.TextArea.Foreground = editorFg;
                            if (!string.IsNullOrEmpty(theme.SelectionBackground))
                            {
                                _splitEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse(theme.SelectionBackground));
                            }
                        }
                        _splitEditor.LineNumbersForeground = lineNumbersFg;
                        var splitHighlight = _splitEditor.SyntaxHighlighting;
                        _splitEditor.SyntaxHighlighting = null;
                        _splitEditor.SyntaxHighlighting = splitHighlight;
                        _splitEditor.TextArea?.TextView?.Redraw();
                    }

                    if (vm.IsMarkdownPreviewActive)
                    {
                        UpdateMarkdownPreview();
                    }
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
        if (DataContext is MainViewModel vm && WindowKeyboardRouter.HandleKeyDown(e, vm, this))
        {
            return;
        }

        base.OnKeyDown(e);
    }

    void WindowKeyboardRouter.IKeyboardActionTarget.FocusEditor() => Editor.Focus();
    void WindowKeyboardRouter.IKeyboardActionTarget.ToggleComment() => ToggleComment();
    void WindowKeyboardRouter.IKeyboardActionTarget.DuplicateLineOrSelection() => DuplicateLineOrSelection();
    void WindowKeyboardRouter.IKeyboardActionTarget.DeleteCurrentLine() => DeleteCurrentLine();
    void WindowKeyboardRouter.IKeyboardActionTarget.MoveSelectedLinesUp() => MoveSelectedLinesUp();
    void WindowKeyboardRouter.IKeyboardActionTarget.MoveSelectedLinesDown() => MoveSelectedLinesDown();

    #endregion

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.ActiveDocument != null)
        {
            var caret = Editor.TextArea.Caret;
            var selectionLength = Editor.SelectionLength;
            var selectedLineCount = 0;
            if (selectionLength > 0 && Editor.Document != null)
            {
                var startLine = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
                var endLine = Editor.Document.GetLineByOffset(Editor.SelectionStart + Editor.SelectionLength).LineNumber;
                selectedLineCount = endLine - startLine + 1;
            }

            vm.ActiveDocument.UpdateCaretPosition(caret.Line, caret.Column, selectionLength, selectedLineCount);
            _bracketMatchingRenderer.UpdateMatchingBrackets();
            _wordOccurrenceRenderer.UpdateOccurrences();
            Editor.TextArea?.TextView?.InvalidateVisual();
        }
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DocumentViewModel doc && DataContext is MainViewModel vm)
        {
            var pointerProps = e.GetCurrentPoint(border).Properties;
            if (pointerProps.IsMiddleButtonPressed)
            {
                _ = vm.CloseTabCommand.ExecuteAsync(doc);
                e.Handled = true;
                return;
            }

            vm.ActiveDocument = doc;
            Editor.WordWrap = doc.WordWrap;
            _bracketMatchingRenderer.UpdateMatchingBrackets();
            _wordOccurrenceRenderer.UpdateOccurrences();
            UpdateSearchMatches();
            Editor.TextArea?.TextView?.InvalidateVisual();
            Minimap.InvalidateVisual();
            Editor.Focus();
        }
    }

    private void OnTabBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CreateNewDocument();
        }
    }

    #region Drag & Drop Support

    private void OnDragOver(object? sender, DragEventArgs e) => FileDragDropHandler.HandleDragOver(e);

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await FileDragDropHandler.HandleDropAsync(e, path => vm.OpenFileInternalAsync(path));
        }
    }

    #endregion

    #region Search Implementation

    private void UpdateSearchMatches()
    {
        if (DataContext is MainViewModel vm)
        {
            _searchCoordinator.UpdateSearchMatches(Editor, vm.SearchViewModel);
        }
    }

    private void OnFindNext()
    {
        if (DataContext is MainViewModel vm)
        {
            _searchCoordinator.FindNext(Editor, vm.SearchViewModel);
        }
    }

    private void OnFindPrevious()
    {
        if (DataContext is MainViewModel vm)
        {
            _searchCoordinator.FindPrevious(Editor, vm.SearchViewModel);
        }
    }

    private void OnReplace()
    {
        if (DataContext is MainViewModel vm)
        {
            _searchCoordinator.Replace(Editor, vm.SearchViewModel);
        }
    }

    private void OnReplaceAll()
    {
        if (DataContext is MainViewModel vm)
        {
            _searchCoordinator.ReplaceAll(Editor, vm.SearchViewModel, msg => vm.ShowTemporaryStatusAsync(msg));
        }
    }

    #endregion

    #region Advanced Editor Actions & Palettes

    private void OnGoToLine(int line, int col) => EditorTextManipulator.GoToLine(Editor, line, col);

    private void ToggleComment()
    {
        if (DataContext is MainViewModel vm && vm.ActiveDocument != null)
        {
            var prefix = vm.LanguageService.GetCommentPrefix(vm.ActiveDocument.Language);
            EditorTextManipulator.ToggleComment(Editor, prefix);
        }
    }

    private void DuplicateLineOrSelection() => EditorTextManipulator.DuplicateLineOrSelection(Editor);
    private void DeleteCurrentLine() => EditorTextManipulator.DeleteCurrentLine(Editor);
    private void MoveSelectedLinesUp() => EditorTextManipulator.MoveSelectedLinesUp(Editor);
    private void MoveSelectedLinesDown() => EditorTextManipulator.MoveSelectedLinesDown(Editor);

    private void OnQuickOpenTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseQuickOpen();
            Editor.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = vm.SelectQuickOpenItemCommand.ExecuteAsync(vm.SelectedQuickOpenItem);
            Editor.Focus();
            e.Handled = true;
            return;
        }

        OverlayKeyNavigationHelper.HandleUpDownNavigation(e, QuickOpenListBox, vm.FilteredQuickOpenItems, item => vm.SelectedQuickOpenItem = item);
    }

    private void OnQuickOpenListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            _ = vm.SelectQuickOpenItemCommand.ExecuteAsync(vm.SelectedQuickOpenItem);
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseQuickOpen();
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void OnQuickOpenItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _ = vm.SelectQuickOpenItemCommand.ExecuteAsync(vm.SelectedQuickOpenItem);
            Editor.Focus();
        }
    }

    private void OnGoToLineTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseGoToLine();
            Editor.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.ExecuteGoToLine();
            e.Handled = true;
        }
    }

    private void OnDuplicateLineClick(object? sender, RoutedEventArgs e) => DuplicateLineOrSelection();
    private void OnDeleteLineClick(object? sender, RoutedEventArgs e) => DeleteCurrentLine();

    private void OnFolderItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Control ctrl && ctrl.DataContext is FolderItem item)
        {
            _ = vm.SelectFolderItemCommand.ExecuteAsync(item);
        }
    }

    private void OnCommandPaletteTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseCommandPalette();
            Editor.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.ExecuteSelectedCommandPaletteItem();
            Editor.Focus();
            e.Handled = true;
            return;
        }

        OverlayKeyNavigationHelper.HandleUpDownNavigation(e, CommandPaletteListBox, vm.FilteredCommands, item => vm.SelectedCommandPaletteItem = item);
    }

    private void OnCommandPaletteListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.ExecuteSelectedCommandPaletteItem();
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseCommandPalette();
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void OnCommandPaletteItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ExecuteSelectedCommandPaletteItem();
            Editor.Focus();
        }
    }

    private void OnGlobalSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseGlobalSearch();
            Editor.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = vm.SelectGlobalSearchResultCommand.ExecuteAsync(null);
            Editor.Focus();
            e.Handled = true;
            return;
        }

        OverlayKeyNavigationHelper.HandleUpDownNavigation(e, GlobalSearchListBox, vm.GlobalSearchResults, item => vm.SelectedGlobalSearchResult = item);
    }

    private void OnGlobalSearchListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            _ = vm.SelectGlobalSearchResultCommand.ExecuteAsync(null);
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseGlobalSearch();
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void OnGlobalSearchResultDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is Control ctrl && ctrl.DataContext is WorkspaceSearchMatch match)
        {
            _ = vm.SelectGlobalSearchResultCommand.ExecuteAsync(match);
            Editor.Focus();
        }
    }

    private void OnRunnerInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            _ = vm.RunnerViewModel.SubmitInputAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (vm.RunnerViewModel.NavigateHistory(-1, out var prevCmd))
            {
                vm.RunnerViewModel.CurrentInput = prevCmd;
                if (sender is TextBox tb) tb.CaretIndex = tb.Text?.Length ?? 0;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Down)
        {
            if (vm.RunnerViewModel.NavigateHistory(1, out var nextCmd))
            {
                vm.RunnerViewModel.CurrentInput = nextCmd;
                if (sender is TextBox tb) tb.CaretIndex = tb.Text?.Length ?? 0;
                e.Handled = true;
            }
        }
    }

    private void RequestDebouncedMarkdownPreviewUpdate()
    {
        _markdownDebounceCts?.Cancel();
        _markdownDebounceCts?.Dispose();
        _markdownDebounceCts = new CancellationTokenSource();
        var token = _markdownDebounceCts.Token;

        Task.Delay(180, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        UpdateMarkdownPreview();
                    }
                });
            }
        }, token);
    }

    private void UpdateMarkdownPreview()
    {
        if (DataContext is not MainViewModel vm || !vm.IsMarkdownPreviewActive || MarkdownScrollViewer == null) return;

        string text;
        try
        {
            text = vm.ActiveDocument?.TextDocument.Text ?? vm.ActiveDocument?.Model.Text ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            text = vm.ActiveDocument?.Model.Text ?? string.Empty;
        }

        IBrush? fg = null;
        IBrush? border = null;
        IBrush? codeBg = null;
        try
        {
            if (vm.CurrentTheme != null)
            {
                fg = new SolidColorBrush(Color.Parse(vm.CurrentTheme.EditorForeground));
                border = new SolidColorBrush(Color.Parse(vm.CurrentTheme.BorderColor));
                codeBg = new SolidColorBrush(Color.Parse(vm.CurrentTheme.TabBarBackground));
            }
            else
            {
                if (Resources.TryGetResource("ThemeEditorForeground", null, out var fgRes) && fgRes is IBrush fgBrush) fg = fgBrush;
                if (Resources.TryGetResource("ThemeBorderBrush", null, out var bRes) && bRes is IBrush bBrush) border = bBrush;
                if (Resources.TryGetResource("ThemeTabBarBackground", null, out var cRes) && cRes is IBrush cBrush) codeBg = cBrush;
            }
        }
        catch { }

        var currentOffset = MarkdownScrollViewer.Offset;
        MarkdownScrollViewer.Content = MarkdownRenderer.Render(text, fg, border, codeBg);

        // Preserve scroll offset or sync with current editor position
        if (vm.IsMarkdownScrollSyncEnabled)
        {
            SyncMarkdownScrollFromEditor();
        }
        else
        {
            MarkdownScrollViewer.Offset = currentOffset;
        }
    }

    private void OnEditorScrollChanged(object? sender, EventArgs e)
    {
        if (_isSyncingScroll) return;
        if (DataContext is not MainViewModel vm || !vm.IsMarkdownPreviewActive || !vm.IsMarkdownScrollSyncEnabled) return;
        SyncMarkdownScrollFromEditor();
    }

    private void SyncMarkdownScrollFromEditor()
    {
        if (MarkdownScrollViewer == null || Editor?.TextArea?.TextView == null) return;

        var textView = Editor.TextArea.TextView;
        var scrollY = textView.ScrollOffset.Y;
        var viewportH = textView.Bounds.Height;
        var totalH = Math.Max(viewportH, Editor.ExtentHeight > 0 ? Editor.ExtentHeight : (Editor.Document?.LineCount ?? 0) * 18.0);
        var maxEditorScroll = Math.Max(1.0, totalH - viewportH);
        var ratio = Math.Clamp(scrollY / maxEditorScroll, 0.0, 1.0);

        var mdExtent = MarkdownScrollViewer.Extent.Height;
        var mdViewport = MarkdownScrollViewer.Viewport.Height;
        var maxMdScroll = Math.Max(0.0, mdExtent - mdViewport);

        if (maxMdScroll > 0)
        {
            _isSyncingScroll = true;
            try
            {
                MarkdownScrollViewer.Offset = new Avalonia.Vector(MarkdownScrollViewer.Offset.X, ratio * maxMdScroll);
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }
    }

    private void OnMarkdownScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;
        if (DataContext is not MainViewModel vm || !vm.IsMarkdownPreviewActive || !vm.IsMarkdownScrollSyncEnabled) return;
        if (MarkdownScrollViewer == null || Editor?.TextArea?.TextView == null) return;

        var mdExtent = MarkdownScrollViewer.Extent.Height;
        var mdViewport = MarkdownScrollViewer.Viewport.Height;
        var maxMdScroll = Math.Max(0.0, mdExtent - mdViewport);
        if (maxMdScroll <= 0) return;

        var ratio = Math.Clamp(MarkdownScrollViewer.Offset.Y / maxMdScroll, 0.0, 1.0);

        var textView = Editor.TextArea.TextView;
        var viewportH = textView.Bounds.Height;
        var totalH = Math.Max(viewportH, Editor.ExtentHeight > 0 ? Editor.ExtentHeight : (Editor.Document?.LineCount ?? 0) * 18.0);
        var maxEditorScroll = Math.Max(0.0, totalH - viewportH);

        _isSyncingScroll = true;
        try
        {
            Editor.ScrollToVerticalOffset(ratio * maxEditorScroll);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    #endregion

    #region Window Closing Verification

    private bool _isExplicitExit;

    public void ExplicitExit()
    {
        _isExplicitExit = true;
        Close();
    }

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

        var settings = vm.SettingsService.CurrentSettings;
        if (settings.MinimizeToTrayOnClose && !_isExplicitExit)
        {
            e.Cancel = true;
            _ = vm.SaveCurrentSessionAsync();
            Hide();
            return;
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
        var vm = DataContext as MainViewModel;
        var theme = vm?.CurrentTheme;
        IBrush winBg = theme != null ? new SolidColorBrush(Color.Parse(theme.WindowBackground)) : new SolidColorBrush(Color.Parse("#1E1E1E"));
        IBrush winFg = theme != null ? new SolidColorBrush(Color.Parse(theme.Foreground)) : Avalonia.Media.Brushes.White;
        IBrush subFg = theme != null ? new SolidColorBrush(Color.Parse(theme.TabItemInactiveForeground)) : Avalonia.Media.Brushes.Gray;
        IBrush accentBg = theme != null ? new SolidColorBrush(Color.Parse(theme.StatusBarBackground)) : new SolidColorBrush(Color.Parse("#007ACC"));
        IBrush accentFg = theme != null ? new SolidColorBrush(Color.Parse(theme.StatusBarForeground)) : Avalonia.Media.Brushes.White;

        var aboutDialog = new Window
        {
            Title = "About Code Viewer",
            Width = 420,
            Height = 310,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = winBg,
            FontFamily = FontHelper.UiFontFamily,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };

        try
        {
            var logoUri = new Uri("avares://CodeViewer/Assets/icon.png");
            using var logoStream = Avalonia.Platform.AssetLoader.Open(logoUri);
            var logoBitmap = new Avalonia.Media.Imaging.Bitmap(logoStream);
            panel.Children.Add(new Avalonia.Controls.Image
            {
                Source = logoBitmap,
                Width = 56,
                Height = 56,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 0, 6)
            });
        }
        catch
        {
            // Fallback if asset stream not present
        }

        panel.Children.Add(new TextBlock { Text = "Code Viewer", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = winFg, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, FontFamily = FontHelper.UiFontFamily });
        panel.Children.Add(new TextBlock { Text = $"Version {AppUpdateService.GetCurrentAppVersion()} (Windows Native & Open Source)", FontSize = 12, Foreground = subFg, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, FontFamily = FontHelper.UiFontFamily });
        panel.Children.Add(new TextBlock { Text = "Fast, lightweight code viewer and editor with themes and plugins.", FontSize = 12, Foreground = subFg, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 8, 0, 10), FontFamily = FontHelper.UiFontFamily });

        var okBtn = new Button { Content = "OK", Width = 80, CornerRadius = new Avalonia.CornerRadius(4), Background = accentBg, Foreground = accentFg, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, FontFamily = FontHelper.UiFontFamily };
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
