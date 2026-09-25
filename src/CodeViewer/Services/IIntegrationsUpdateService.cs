using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeViewer.Services;

public sealed record IntegrationStatus(
    string LanguageName,
    string InstalledVersion,
    string? RemoteVersion,
    string RepositoryUrl,
    bool IsUpToDate,
    string StatusSummary
);

public interface IIntegrationsUpdateService
{
    Task<IReadOnlyList<IntegrationStatus>> CheckIntegrationsAsync();
}
