namespace CodeViewer.Models;

/// <summary>
/// Status of a file or folder in Git version control.
/// </summary>
public enum GitFileStatus
{
    Clean,
    Modified,
    Untracked,
    Added,
    Deleted,
    Renamed,
    Ignored,
    Conflicted
}
