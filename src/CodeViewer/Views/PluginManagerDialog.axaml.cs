using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeViewer.Plugins;

namespace CodeViewer.Views;

public partial class PluginManagerDialog : Window
{
    private readonly string _pluginsFolder;

    public PluginManagerDialog()
    {
        InitializeComponent();
        _pluginsFolder = string.Empty;
    }

    public PluginManagerDialog(IEnumerable<IPlugin> plugins, string pluginsFolder) : this()
    {
        _pluginsFolder = pluginsFolder;
        PluginsList.ItemsSource = plugins;
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_pluginsFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _pluginsFolder,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Best effort
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
