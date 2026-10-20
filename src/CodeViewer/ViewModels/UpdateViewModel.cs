using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeViewer.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _updateService;
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private AppUpdateInfo _updateInfo;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public string CurrentVersion => UpdateInfo.CurrentVersion;
    public string LatestVersion => UpdateInfo.LatestVersion;
    public string ReleaseTitle => UpdateInfo.ReleaseTitle;
    public string ReleaseNotes => UpdateInfo.ReleaseNotes;
    public bool HasInstaller => !string.IsNullOrEmpty(UpdateInfo.InstallerUrl);

    public string SizeFormatted
    {
        get
        {
            if (UpdateInfo.InstallerSizeBytes is { } bytes && bytes > 0)
            {
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            }
            return string.Empty;
        }
    }

    public Action? RequestClose { get; set; }

    public UpdateViewModel(AppUpdateInfo updateInfo, IAppUpdateService updateService)
    {
        _updateInfo = updateInfo;
        _updateService = updateService;
        _statusText = HasInstaller ? "Ready to download and install." : "No direct setup installer asset found; click 'Open on GitHub' to view.";
    }

    [RelayCommand]
    public async Task DownloadAndInstallAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateInfo.InstallerUrl))
        {
            OpenReleasePage();
            return;
        }

        IsDownloading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        DownloadProgress = 0;
        StatusText = "Downloading update installer...";

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = Math.Round(p * 100, 1);
                StatusText = $"Downloading update... {DownloadProgress:F0}%";
            });

            var installerPath = await _updateService.DownloadInstallerAsync(UpdateInfo.InstallerUrl, progress, _downloadCts.Token);
            StatusText = "Launching installer and updating...";

            // Launch setup installer and shut down current instance cleanly
            _updateService.LaunchInstallerAndExit(installerPath);
            RequestClose?.Invoke();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download was cancelled.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to download update: {ex.Message}";
            StatusText = "Update failed. You can still download manually from GitHub.";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    public void OpenReleasePage()
    {
        try
        {
            var url = UpdateInfo.ReleasePageUrl ?? "https://github.com/Reza2654/code-viewer/releases";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback handled
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _downloadCts?.Cancel();
        RequestClose?.Invoke();
    }
}
