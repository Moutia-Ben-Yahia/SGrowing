namespace SGrowing.Core.Entities;

public class WaterfallEnrichmentCascade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CascadeName { get; set; } = string.Empty;
    public string ProviderChainJson { get; set; } = "[\"Hunter\",\"Apollo\",\"Dropcontact\",\"Prospeo\"]";
    public string TargetDataType { get; set; } = "EmailAndPhone"; // "Email", "Phone", "EmailAndPhone", "Technographics"
    public decimal MinConfidenceScore { get; set; } = 0.80m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static WaterfallEnrichmentCascade Create(
        Guid tenantId,
        string cascadeName,
        string providerChainJson,
        string targetDataType = "EmailAndPhone",
        decimal minConfidence = 0.80m)
    {
        return new WaterfallEnrichmentCascade
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CascadeName = cascadeName.Trim(),
            ProviderChainJson = providerChainJson,
            TargetDataType = targetDataType.Trim(),
            MinConfidenceScore = Math.Clamp(minConfidence, 0.1m, 1.0m),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class EnrichmentExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ContactId { get; set; }
    public string ProspectQuery { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = string.Empty;
    public int StepIndex { get; set; } = 1;
    public string OutcomeStatus { get; set; } = "Matched"; // "Matched", "Skipped", "Failed", "FallbackTriggered"
    public decimal ConfidenceScore { get; set; } = 0.95m;
    public int CreditCostCharged { get; set; } = 1;
    public long LatencyMs { get; set; } = 240;
    public string EnrichedPayloadJson { get; set; } = "{}";
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    public static EnrichmentExecutionLog Create(
        Guid tenantId,
        string prospectQuery,
        string providerUsed,
        int stepIndex,
        string outcomeStatus,
        decimal confidenceScore,
        int credits,
        long latencyMs,
        string enrichedJson,
        Guid? contactId = null)
    {
        return new EnrichmentExecutionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            ProspectQuery = prospectQuery.Trim(),
            ProviderUsed = providerUsed.Trim(),
            StepIndex = stepIndex,
            OutcomeStatus = outcomeStatus.Trim(),
            ConfidenceScore = confidenceScore,
            CreditCostCharged = credits,
            LatencyMs = latencyMs,
            EnrichedPayloadJson = enrichedJson,
            ExecutedAtUtc = DateTime.UtcNow
        };
    }
}
