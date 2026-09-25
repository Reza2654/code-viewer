using System.Threading.Tasks;
using Avalonia.Controls;

namespace CodeViewer.Services;

public enum ConfirmResult
{
    Save,
    DontSave,
    Cancel
}

/// <summary>
/// Service abstraction for native Avalonia file dialogs, prompts, and alerts.
/// </summary>
public interface IDialogService
{
    void Initialize(Window window);

    /// <summary>
    /// Prompts user to select a source code file to open.
    /// </summary>
    Task<string?> ShowOpenFileDialogAsync();

    /// <summary>
    /// Prompts user to select a specific type of file (e.g. JSON themes, XSHD syntax, DLL plugins).
    /// </summary>
    Task<string?> ShowOpenSpecificFileDialogAsync(string title, string filterName, string[] extensions);

    /// <summary>
    /// Prompts user to select a target path to save a file.
    /// </summary>
    Task<string?> ShowSaveFileDialogAsync(string defaultFileName);

    /// <summary>
    /// Prompts user when closing an unsaved tab: Save / Don't Save / Cancel.
    /// </summary>
    Task<ConfirmResult> ShowSaveConfirmationAsync(string documentTitle);

    /// <summary>
    /// Displays a message box or alert dialog.
    /// </summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>
    /// Displays a confirmation dialog returning true if confirmed.
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");
}

