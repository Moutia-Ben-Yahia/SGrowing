using SGrowing.Api.Modules.Outbound.EmailVerification;

namespace SGrowing.Api.Modules.Outbound.Enrichment;

public record ContactEnrichmentRequest(
    string Email,
    string? FirstName = null,
    string? LastName = null,
    string? JobTitle = null,
    string? CompanyName = null,
    string? Website = null
);

public record EnrichedContactDto(
    string Email,
    string FirstName,
    string LastName,
    string JobTitle,
    string SeniorityLevel, // "C-Level", "VP", "Director", "Manager", "Senior", "Individual Contributor"
    string CompanyName,
    string Domain,
    string Industry,
    string EmployeeRange,
    string RevenueRange,
    string TechStack,
    string LinkedinUrl,
    EmailVerificationResult Deliverability,
    int OverallDataConfidenceScore // 0-100
);

public record BatchEnrichmentResultDto(
    int TotalProcessed,
    int DeliverableCount,
    int RiskyOrUndeliverableCount,
    List<EnrichedContactDto> EnrichedContacts
);

public interface IContactEnrichmentPipeline
{
    Task<EnrichedContactDto> EnrichAndVerifyContactAsync(Guid tenantId, ContactEnrichmentRequest request, CancellationToken ct = default);
    Task<BatchEnrichmentResultDto> BatchEnrichContactsAsync(Guid tenantId, List<ContactEnrichmentRequest> requests, CancellationToken ct = default);
}
