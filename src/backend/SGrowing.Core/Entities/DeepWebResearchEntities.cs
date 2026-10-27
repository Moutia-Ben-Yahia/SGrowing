namespace SGrowing.Core.Entities;

public class WebResearchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Engine { get; set; } = "MultiEngine"; // Tavily, Firecrawl, Exa, MultiEngine
    public string CorporateSummary { get; set; } = string.Empty;
    public string ExecutiveBiosJson { get; set; } = "[]";
    public string TechTagsJson { get; set; } = "[]";
    public string SalesHooksJson { get; set; } = "[]";
    public int ConfidenceScore { get; set; } = 92;
    public DateTime ResearchedAtUtc { get; set; } = DateTime.UtcNow;
}

public record ExecutiveBio(
    string Name,
    string Title,
    string? LinkedInUrl,
    string? Summary);

public record TechnologyTag(
    string Category,
    string Name,
    string Confidence);

public record WebResearchCrawlRequest(
    string Domain,
    string? CompanyName = null,
    string? Engine = null,
    bool ExtractBios = true,
    bool ExtractTechStack = true,
    bool GenerateSalesHooks = true);

public record WebResearchCrawlResult(
    bool Success,
    string Domain,
    string CompanyName,
    string CorporateSummary,
    List<ExecutiveBio> ExecutiveBios,
    List<TechnologyTag> TechTags,
    List<string> SalesHooks,
    string EngineUsed,
    int ScrapedPageCount,
    double ExecutionTimeMs);

public record ProspectResearchBrief(
    Guid Id,
    Guid TenantId,
    string Domain,
    string CompanyName,
    string Engine,
    string CorporateSummary,
    List<ExecutiveBio> ExecutiveBios,
    List<TechnologyTag> TechTags,
    List<string> SalesHooks,
    int ConfidenceScore,
    DateTime ResearchedAtUtc);
