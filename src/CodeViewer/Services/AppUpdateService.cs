using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace CodeViewer.Services;

public class AppUpdateService : IAppUpdateService
{
    private const string RepoOwner = "Reza2654";
    private const string RepoName = "code-viewer";

    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;

    public string CurrentVersion => _currentVersion;

    public AppUpdateService(HttpClient? httpClient = null, string? currentVersion = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CodeViewer-Desktop-Updater");
        }

        _currentVersion = currentVersion ?? GetCurrentAppVersion();
    }

    public static string GetCurrentAppVersion()
    {
        try
        {
            var asm = typeof(AppUpdateService).Assembly;
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                // Strip git commit hash if present (e.g. 1.2.0-beta.11+commitHash)
                var plusIdx = infoVer.IndexOf('+');
                return plusIdx > 0 ? infoVer[..plusIdx] : infoVer;
            }

            var ver = asm.GetName().Version;
            if (ver != null)
            {
                return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
        }
        catch
        {
            // Fallback handled
        }

        return "1.2.0-beta.14";
    }

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(bool isManualCheck = false, CancellationToken cancellationToken = default)
    {
        var releasesUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=5";

        try
        {
            using var response = await _httpClient.GetAsync(releasesUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AppUpdateInfo(
                    IsUpdateAvailable: false,
                    CurrentVersion: _currentVersion,
                    LatestVersion: _currentVersion,
                    ReleaseTitle: "Code Viewer",
                    ReleaseNotes: isManualCheck ? $"Unable to check for updates (HTTP {response.StatusCode})." : string.Empty,
                    InstallerUrl: null,
                    ReleasePageUrl: $"https://github.com/{RepoOwner}/{RepoName}/releases",
                    InstallerSizeBytes: null,
                    PublishedAt: null
                );
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return new AppUpdateInfo(
                    IsUpdateAvailable: false,
                    CurrentVersion: _currentVersion,
                    LatestVersion: _currentVersion,
                    ReleaseTitle: "Code Viewer",
                    ReleaseNotes: isManualCheck ? "No releases found on GitHub." : string.Empty,
                    InstallerUrl: null,
                    ReleasePageUrl: $"https://github.com/{RepoOwner}/{RepoName}/releases",
                    InstallerSizeBytes: null,
                    PublishedAt: null
                );
            }

            // Inspect releases to find newest valid release
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("tag_name", out var tagElem)) continue;
                var tag = tagElem.GetString();
                if (string.IsNullOrWhiteSpace(tag)) continue;

                var isNewer = CompareVersions(tag, _currentVersion) > 0;
                var title = release.TryGetProperty("name", out var nameElem) && !string.IsNullOrWhiteSpace(nameElem.GetString())
                    ? nameElem.GetString()!
                    : $"Code Viewer {tag}";
                var notes = release.TryGetProperty("body", out var bodyElem)
                    ? bodyElem.GetString() ?? string.Empty
                    : string.Empty;
                var htmlUrl = release.TryGetProperty("html_url", out var htmlElem)
                    ? htmlElem.GetString()
                    : $"https://github.com/{RepoOwner}/{RepoName}/releases/tag/{tag}";

                DateTimeOffset? publishedAt = null;
                if (release.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTimeOffset(out var dt))
                {
                    publishedAt = dt;
                }

                // Search assets for Setup.exe
                string? installerUrl = null;
                long? installerSize = null;

                if (release.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var assetNameElem))
                        {
                            var assetName = assetNameElem.GetString() ?? string.Empty;
                            if (assetName.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                                assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                if (asset.TryGetProperty("browser_download_url", out var downloadElem))
                                {
                                    installerUrl = downloadElem.GetString();
                                }
                                if (asset.TryGetProperty("size", out var sizeElem) && sizeElem.TryGetInt64(out var sizeVal))
                                {
                                    installerSize = sizeVal;
                                }
                                break;
                            }
                        }
                    }
                }

                return new AppUpdateInfo(
                    IsUpdateAvailable: isNewer,
                    CurrentVersion: _currentVersion,
                    LatestVersion: tag,
                    ReleaseTitle: title,
                    ReleaseNotes: notes,
                    InstallerUrl: installerUrl,
                    ReleasePageUrl: htmlUrl,
                    InstallerSizeBytes: installerSize,
                    PublishedAt: publishedAt
                );
            }
        }
        catch (Exception ex)
        {
            return new AppUpdateInfo(
                IsUpdateAvailable: false,
                CurrentVersion: _currentVersion,
                LatestVersion: _currentVersion,
                ReleaseTitle: "Code Viewer",
                ReleaseNotes: isManualCheck ? $"Network error checking updates: {ex.Message}" : string.Empty,
                InstallerUrl: null,
                ReleasePageUrl: $"https://github.com/{RepoOwner}/{RepoName}/releases",
                InstallerSizeBytes: null,
                PublishedAt: null
            );
        }

        return new AppUpdateInfo(
            IsUpdateAvailable: false,
            CurrentVersion: _currentVersion,
            LatestVersion: _currentVersion,
            ReleaseTitle: "Code Viewer",
            ReleaseNotes: isManualCheck ? "You are on the latest version." : string.Empty,
            InstallerUrl: null,
            ReleasePageUrl: $"https://github.com/{RepoOwner}/{RepoName}/releases",
            InstallerSizeBytes: null,
            PublishedAt: null
        );
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("Download URL cannot be empty.", nameof(downloadUrl));
        }

        var tempDir = Path.GetTempPath();
        var tempFile = Path.Combine(tempDir, $"CodeViewer-Update-{Guid.NewGuid():N}.exe");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

        var buffer = new byte[65536];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                var percentage = Math.Clamp((double)totalRead / totalBytes, 0.0, 1.0);
                progress.Report(percentage);
            }
        }

        progress?.Report(1.0);
        return tempFile;
    }

    public void LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer executable was not found on disk.", installerPath);
        }

        // Launch installer
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };
        Process.Start(psi);

        // Terminate running Code Viewer instance cleanly
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SingleInstanceService.Stop();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        });
    }

    /// <summary>
    /// Compares two semver / beta version strings (e.g., "v1.2.0-beta.11" vs "v1.2.0-beta.10").
    /// Returns 1 if v1 > v2, -1 if v1 &lt; v2, and 0 if equal.
    /// </summary>
    public static int CompareVersions(string v1, string v2)
    {
        if (string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase)) return 0;

        var clean1 = v1.Trim().TrimStart('v', 'V');
        var clean2 = v2.Trim().TrimStart('v', 'V');

        var parsed1 = ParseVersion(clean1);
        var parsed2 = ParseVersion(clean2);

        // Compare Major, Minor, Patch, Build
        var core1 = new Version(parsed1.Major, parsed1.Minor, parsed1.Patch, parsed1.Build);
        var core2 = new Version(parsed2.Major, parsed2.Minor, parsed2.Patch, parsed2.Build);

        var coreCompare = core1.CompareTo(core2);
        if (coreCompare != 0)
        {
            return coreCompare;
        }

        // When core versions are equal, a release with NO pre-release suffix is NEWER than a pre-release
        // e.g. "1.2.0" > "1.2.0-beta.11"
        if (string.IsNullOrEmpty(parsed1.Prerelease) && !string.IsNullOrEmpty(parsed2.Prerelease))
        {
            return 1;
        }
        if (!string.IsNullOrEmpty(parsed1.Prerelease) && string.IsNullOrEmpty(parsed2.Prerelease))
        {
            return -1;
        }

        // Both are pre-releases (e.g. beta.11 vs beta.10)
        var preCompare = string.Compare(parsed1.Prerelease, parsed2.Prerelease, StringComparison.OrdinalIgnoreCase);
        if (preCompare != 0)
        {
            return preCompare;
        }

        return parsed1.PrereleaseNum.CompareTo(parsed2.PrereleaseNum);
    }

    private static (int Major, int Minor, int Patch, int Build, string Prerelease, int PrereleaseNum) ParseVersion(string raw)
    {
        int major = 0, minor = 0, patch = 0, build = 0;
        string prerelease = string.Empty;
        int prereleaseNum = 0;

        var dashIdx = raw.IndexOf('-');
        var corePart = dashIdx >= 0 ? raw[..dashIdx] : raw;
        var prePart = dashIdx >= 0 ? raw[(dashIdx + 1)..] : string.Empty;

        var parts = corePart.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out var m)) major = m;
        if (parts.Length > 1 && int.TryParse(parts[1], out var n)) minor = n;
        if (parts.Length > 2 && int.TryParse(parts[2], out var p)) patch = p;
        if (parts.Length > 3 && int.TryParse(parts[3], out var b)) build = b;

        if (!string.IsNullOrEmpty(prePart))
        {
            var match = Regex.Match(prePart, @"^([a-zA-Z]+)(?:\.(\d+))?");
            if (match.Success)
            {
                prerelease = match.Groups[1].Value.ToLowerInvariant();
                if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var num))
                {
                    prereleaseNum = num;
                }
            }
            else
            {
                prerelease = prePart.ToLowerInvariant();
            }
        }

        return (major, minor, patch, build, prerelease, prereleaseNum);
    }
}
