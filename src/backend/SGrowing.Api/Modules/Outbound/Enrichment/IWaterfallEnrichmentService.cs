using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound.Enrichment;

public record WaterfallEnrichmentResultDto(
    bool IsMatched,
    string ProspectQuery,
    string? WinningProvider,
    int StepsAttempted,
    decimal ConfidenceScore,
    string? VerifiedEmail,
    string? VerifiedPhone,
    string? JobTitle,
    string? CompanyName,
    int TotalCreditsCharged,
    long TotalLatencyMs,
    List<EnrichmentStepDetailDto> StepAuditTrail
);

public record EnrichmentStepDetailDto(
    int StepIndex,
    string Provider,
    string Status,
    decimal Confidence,
    long LatencyMs
);

public interface IWaterfallEnrichmentService
{
    Task<WaterfallEnrichmentCascade> CreateCascadeAsync(
        Guid tenantId,
        string name,
        List<string> providers,
        string targetDataType = "EmailAndPhone",
        decimal minConfidence = 0.80m);

    Task<WaterfallEnrichmentResultDto> ExecuteWaterfallEnrichmentAsync(
        Guid tenantId,
        string prospectQuery,
        string? companyDomain = null,
        Guid? cascadeId = null,
        Guid? contactId = null);

    Task<List<EnrichmentExecutionLog>> GetExecutionLogsAsync(
        Guid tenantId,
        int limit = 50);
}
