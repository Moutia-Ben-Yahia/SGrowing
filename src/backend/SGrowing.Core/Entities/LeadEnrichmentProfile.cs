namespace SGrowing.Core.Entities;

public class LeadEnrichmentProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string EmployeeCountRange { get; set; } = string.Empty; // "1-10", "11-50", "51-200", "201-500", "501-1000", "1000+"
    public string EstimatedAnnualRevenue { get; set; } = string.Empty; // "$1M-$5M", "$10M-$50M", "$50M+"
    public string TechStack { get; set; } = string.Empty; // "PostgreSQL, React, AWS, Twilio, Redis"
    public string? LinkedinUrl { get; set; }
    public string? TwitterHandle { get; set; }
    public DateTime EnrichedAtUtc { get; set; } = DateTime.UtcNow;

    public static LeadEnrichmentProfile Create(
        Guid tenantId,
        string domain,
        string companyName,
        string industry,
        string employeeRange,
        string revenueRange,
        string techStack,
        string? linkedinUrl = null,
        string? twitterHandle = null)
    {
        return new LeadEnrichmentProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = domain.Trim().ToLowerInvariant(),
            CompanyName = companyName,
            Industry = industry,
            EmployeeCountRange = employeeRange,
            EstimatedAnnualRevenue = revenueRange,
            TechStack = techStack,
            LinkedinUrl = linkedinUrl,
            TwitterHandle = twitterHandle,
            EnrichedAtUtc = DateTime.UtcNow
        };
    }
}
