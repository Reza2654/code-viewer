using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeViewer.Models;

public partial class CommandPaletteItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _shortcutText = string.Empty;

    [ObservableProperty]
    private string _icon = "⚡";

    public Action? Action { get; set; }

    public string DisplayText => string.IsNullOrEmpty(Category) ? Title : $"{Category}: {Title}";

    public CommandPaletteItem()
    {
    }

    public CommandPaletteItem(string id, string title, string category, string shortcutText, string icon, Action action)
    {
        Id = id;
        Title = title;
        Category = category;
        ShortcutText = shortcutText;
        Icon = icon;
        Action = action;
    }
}
