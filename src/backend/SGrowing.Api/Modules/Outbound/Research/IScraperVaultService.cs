using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound.Research;

public interface IScraperVaultService
{
    Task<CrawlerVaultOverview> GetCrawlerSecretsOverviewAsync(Guid tenantId, CancellationToken ct = default);
    Task<CrawlerEngineHealth> SaveCrawlerSecretAsync(Guid tenantId, SaveCrawlerSecretRequest req, CancellationToken ct = default);
    Task<CrawlerVaultOverview> CheckAllEnginesHealthAsync(Guid tenantId, CancellationToken ct = default);
    Task<string?> GetDecryptedCrawlerSecretAsync(Guid tenantId, string provider, CancellationToken ct = default);
}
