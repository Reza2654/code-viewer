using System;
using System.IO;
using CodeViewer.Models;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeViewer.ViewModels;

/// <summary>
/// ViewModel displaying detailed statistics and metadata about a document.
/// </summary>
public partial class FileInfoViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _fileName;

    [ObservableProperty]
    private string _fullPath;

    [ObservableProperty]
    private string _directory;

    [ObservableProperty]
    private string _fileSize;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private string _encoding;

    [ObservableProperty]
    private string _lineEndings;

    [ObservableProperty]
    private int _lineCount;

    [ObservableProperty]
    private int _characterCount;

    [ObservableProperty]
    private string _lastModified;

    public FileInfoViewModel(DocumentViewModel doc, IFileService fileService)
    {
        _fileName = doc.Title;
        _fullPath = doc.FilePath ?? "Not saved to disk";
        _directory = string.IsNullOrEmpty(doc.FilePath) ? "-" : (Path.GetDirectoryName(doc.FilePath) ?? "-");
        _fileSize = fileService.FormatFileSize(doc.Model.FileSizeBytes);
        _language = doc.Language;
        _encoding = doc.EncodingName;
        _lineEndings = doc.LineEndingDisplay;
        _lineCount = doc.TextDocument.LineCount;
        _characterCount = doc.TextDocument.TextLength;
        _lastModified = doc.Model.LastModifiedOnDisk?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Unsaved";
    }
}
