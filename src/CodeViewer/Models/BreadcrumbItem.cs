using System.Windows.Input;

namespace CodeViewer.Models;

/// <summary>
/// Model representing a single segment in the path breadcrumb navigation bar.
/// </summary>
public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsLast { get; set; }
    public ICommand? ClickCommand { get; set; }
}
