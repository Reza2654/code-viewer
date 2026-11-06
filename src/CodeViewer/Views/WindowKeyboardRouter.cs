using System;
using Avalonia.Input;
using CodeViewer.ViewModels;

namespace CodeViewer.Views;

/// <summary>
/// Routes global window-level keyboard shortcuts to viewmodel commands and editor actions.
/// </summary>
public static class WindowKeyboardRouter
{
    public interface IKeyboardActionTarget
    {
        void FocusEditor();
        void ToggleComment();
        void DuplicateLineOrSelection();
        void DeleteCurrentLine();
        void MoveSelectedLinesUp();
        void MoveSelectedLinesDown();
    }

    public static bool HandleKeyDown(KeyEventArgs e, MainViewModel vm, IKeyboardActionTarget target)
    {
        // Escape: Close any open palette/overlay
        if (e.Key == Key.Escape)
        {
            if (vm.IsQuickOpenOpen)
            {
                vm.CloseQuickOpen();
                target.FocusEditor();
                e.Handled = true;
                return true;
            }
            if (vm.IsGlobalSearchOpen)
            {
                vm.CloseGlobalSearch();
                target.FocusEditor();
                e.Handled = true;
                return true;
            }
            if (vm.IsGoToLineOpen)
            {
                vm.CloseGoToLine();
                target.FocusEditor();
                e.Handled = true;
                return true;
            }
            if (vm.SearchViewModel.IsOpen)
            {
                vm.SearchViewModel.Close();
                target.FocusEditor();
                e.Handled = true;
                return true;
            }
        }

        // Ctrl + Shift + F: Find in Files (Workspace Search)
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.F)
        {
            vm.OpenGlobalSearch();
            e.Handled = true;
            return true;
        }

        // F5: Run Active Script
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.F5)
        {
            _ = vm.RunActiveScriptCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Shift + F5: Stop Script Runner
        if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.F5)
        {
            if (vm.RunnerViewModel.IsRunning)
            {
                vm.RunnerViewModel.Cancel();
                e.Handled = true;
                return true;
            }
        }

        // Ctrl + ` (OemTilde/Oem3): Toggle Terminal Console
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemTilde || e.Key == Key.Oem3))
        {
            vm.ToggleRunnerConsole();
            e.Handled = true;
            return true;
        }

        // Ctrl + P: Quick Open
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.P)
        {
            _ = vm.ShowQuickOpenCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + G: Go to Line
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.G)
        {
            vm.ShowGoToLine();
            e.Handled = true;
            return true;
        }

        // Ctrl + H: Replace
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.H)
        {
            vm.ShowReplace();
            e.Handled = true;
            return true;
        }

        // Ctrl + / : Toggle Line Comment
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemQuestion || e.Key == Key.Divide))
        {
            target.ToggleComment();
            e.Handled = true;
            return true;
        }

        // Ctrl + D: Duplicate Line or Selection
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.D)
        {
            target.DuplicateLineOrSelection();
            e.Handled = true;
            return true;
        }

        // Ctrl + Shift + K: Delete Line
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.K)
        {
            target.DeleteCurrentLine();
            e.Handled = true;
            return true;
        }

        // Alt + Up: Move Line Up
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Up)
        {
            target.MoveSelectedLinesUp();
            e.Handled = true;
            return true;
        }

        // Alt + Down: Move Line Down
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Down)
        {
            target.MoveSelectedLinesDown();
            e.Handled = true;
            return true;
        }

        // Ctrl + S: Save
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
        {
            _ = vm.SaveCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + Shift + S: Save As
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.S)
        {
            _ = vm.SaveAsCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + N: New File
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N)
        {
            vm.CreateNewDocument();
            e.Handled = true;
            return true;
        }

        // Ctrl + O: Open File
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O)
        {
            _ = vm.OpenFileCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + W: Close Active Tab
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W)
        {
            _ = vm.CloseTabCommand.ExecuteAsync(vm.ActiveDocument);
            e.Handled = true;
            return true;
        }

        // Ctrl + F: Find in File
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
        {
            vm.ShowSearch();
            e.Handled = true;
            return true;
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
            return true;
        }

        // Ctrl + Plus / Ctrl + = : Zoom In
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemPlus || e.Key == Key.Add))
        {
            vm.ZoomIn();
            e.Handled = true;
            return true;
        }

        // Ctrl + Minus: Zoom Out
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
        {
            vm.ZoomOut();
            e.Handled = true;
            return true;
        }

        // Ctrl + 0: Reset Zoom
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.D0 || e.Key == Key.NumPad0))
        {
            vm.ResetZoom();
            e.Handled = true;
            return true;
        }

        // Alt + Z: Toggle Word Wrap
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Z)
        {
            vm.ToggleWordWrap();
            e.Handled = true;
            return true;
        }

        // Ctrl + Shift + T : Reopen Closed Tab
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.T)
        {
            _ = vm.ReopenClosedTabCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + Alt + M : Toggle Minimap
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt) && e.Key == Key.M)
        {
            _ = vm.ToggleMinimapCommand.ExecuteAsync(null);
            e.Handled = true;
            return true;
        }

        // Ctrl + Alt + O : Toggle Document Outline
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt) && e.Key == Key.O)
        {
            vm.ToggleOutline();
            e.Handled = true;
            return true;
        }

        // Ctrl + Shift + E : Focus Explorer Sidebar
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.E)
        {
            vm.ShowExplorerSidebar();
            e.Handled = true;
            return true;
        }

        // Ctrl + , : Settings
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemComma || e.Key == Key.OemPeriod))
        {
            vm.ShowSettings();
            e.Handled = true;
            return true;
        }

        return false;
    }
}
