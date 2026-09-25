using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public class IntegrationsUpdateService : IIntegrationsUpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    static IntegrationsUpdateService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "CodeViewer-Desktop");
    }

    public async Task<IReadOnlyList<IntegrationStatus>> CheckIntegrationsAsync()
    {
        var results = new List<IntegrationStatus>();

        var agentLangTask = CheckRepoAsync(
            languageName: "AgentLang",
            installedVersion: "v0.2.2",
            repoOwner: "Reza2654",
            repoName: "AgentLang"
        );

        var ps2Task = CheckRepoAsync(
            languageName: "PS2 (PowerScript 2)",
            installedVersion: "v0.4.0",
            repoOwner: "Reza2654",
            repoName: "ps2"
        );

        results.Add(await agentLangTask);
        results.Add(await ps2Task);

        return results;
    }

    private static async Task<IntegrationStatus> CheckRepoAsync(
        string languageName,
        string installedVersion,
        string repoOwner,
        string repoName)
    {
        var repoUrl = $"https://github.com/{repoOwner}/{repoName}";
        string? remoteTag = null;
        string summary;
        bool isUpToDate = true;

        try
        {
            // First try fetching latest release
            var releaseUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
            using var releaseResponse = await HttpClient.GetAsync(releaseUrl).ConfigureAwait(false);

            if (releaseResponse.IsSuccessStatusCode)
            {
                var content = await releaseResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("tag_name", out var tagElem))
                {
                    remoteTag = tagElem.GetString();
                }
            }

            // Also check tags to see the newest development or patch tag
            var tagsUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/tags?per_page=1";
            using var tagsResponse = await HttpClient.GetAsync(tagsUrl).ConfigureAwait(false);
            if (tagsResponse.IsSuccessStatusCode)
            {
                var tagsContent = await tagsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var tagsDoc = JsonDocument.Parse(tagsContent);
                if (tagsDoc.RootElement.ValueKind == JsonValueKind.Array && tagsDoc.RootElement.GetArrayLength() > 0)
                {
                    var firstTag = tagsDoc.RootElement[0];
                    if (firstTag.TryGetProperty("name", out var nameProp))
                    {
                        var latestTag = nameProp.GetString();
                        // If tags is newer than release tag, prefer the newest tag
                        if (!string.IsNullOrEmpty(latestTag))
                        {
                            remoteTag = latestTag;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(remoteTag))
            {
                isUpToDate = string.Equals(installedVersion, remoteTag, StringComparison.OrdinalIgnoreCase) ||
                             CompareVersions(installedVersion, remoteTag) >= 0;

                summary = isUpToDate
                    ? $"Up to date ({installedVersion} installed, {remoteTag} latest on GitHub)"
                    : $"Update available: {remoteTag} available on GitHub (installed: {installedVersion})";
            }
            else
            {
                summary = $"{installedVersion} active (online check returned no release tags)";
            }
        }
        catch
        {
            summary = $"{installedVersion} active (offline - unable to reach GitHub API)";
        }

        return new IntegrationStatus(
            LanguageName: languageName,
            InstalledVersion: installedVersion,
            RemoteVersion: remoteTag,
            RepositoryUrl: repoUrl,
            IsUpToDate: isUpToDate,
            StatusSummary: summary
        );
    }

    private static int CompareVersions(string v1, string v2)
    {
        var clean1 = v1.TrimStart('v', 'V');
        var clean2 = v2.TrimStart('v', 'V');

        // Split pre-release suffixes
        var dash1 = clean1.IndexOf('-');
        if (dash1 >= 0) clean1 = clean1[..dash1];
        var dash2 = clean2.IndexOf('-');
        if (dash2 >= 0) clean2 = clean2[..dash2];

        if (Version.TryParse(clean1, out var ver1) && Version.TryParse(clean2, out var ver2))
        {
            return ver1.CompareTo(ver2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }
}
