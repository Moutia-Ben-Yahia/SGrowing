using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound.Research;

public interface IDeepWebResearchService
{
    Task<WebResearchCrawlResult> CrawlAndExtractAsync(Guid tenantId, WebResearchCrawlRequest req, CancellationToken ct = default);
    Task<ProspectResearchBrief?> GetBriefAsync(Guid tenantId, string domain, CancellationToken ct = default);
    Task<IEnumerable<ProspectResearchBrief>> ListBriefsAsync(Guid tenantId, CancellationToken ct = default);
}
