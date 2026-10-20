using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public sealed record AppUpdateInfo(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseTitle,
    string ReleaseNotes,
    string? InstallerUrl,
    string? ReleasePageUrl,
    long? InstallerSizeBytes,
    DateTimeOffset? PublishedAt
);

public interface IAppUpdateService
{
    string CurrentVersion { get; }
    Task<AppUpdateInfo> CheckForUpdatesAsync(bool isManualCheck = false, CancellationToken cancellationToken = default);
    Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    void LaunchInstallerAndExit(string installerPath);
}
