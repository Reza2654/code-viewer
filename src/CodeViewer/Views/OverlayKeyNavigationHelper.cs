using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace CodeViewer.Views;

/// <summary>
/// Helper for handling arrow key navigation in overlay listboxes (Command Palette, Quick Open, Global Search).
/// </summary>
public static class OverlayKeyNavigationHelper
{
    public static bool HandleUpDownNavigation<T>(KeyEventArgs e, ListBox? listBox, IList<T>? items, Action<T> onItemActivated) where T : notnull
    {
        if (listBox == null || items == null || items.Count == 0) return false;

        if (e.Key == Key.Down)
        {
            var next = Math.Min(listBox.SelectedIndex + 1, items.Count - 1);
            listBox.SelectedIndex = next;
            var item = items[next];
            onItemActivated(item);
            if (item != null) listBox.ScrollIntoView(item);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Up)
        {
            var prev = Math.Max(listBox.SelectedIndex - 1, 0);
            listBox.SelectedIndex = prev;
            var item = items[prev];
            onItemActivated(item);
            if (item != null) listBox.ScrollIntoView(item);
            e.Handled = true;
            return true;
        }

        return false;
    }
}
