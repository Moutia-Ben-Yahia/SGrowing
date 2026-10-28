namespace SGrowing.Core.Entities;

public enum AiTaskComplexity
{
    FastAndCheap = 1,
    Balanced = 2,
    DeepReasoning = 3
}

public class DynamicAgentTool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersSchemaJson { get; set; } = "{}";
    public string TargetEndpointUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DynamicAgentTool Create(
        Guid tenantId,
        string name,
        string description,
        string schemaJson,
        string targetUrl,
        string httpMethod = "POST")
    {
        return new DynamicAgentTool
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description.Trim(),
            ParametersSchemaJson = schemaJson,
            TargetEndpointUrl = targetUrl.Trim(),
            HttpMethod = httpMethod.ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class PromptFirewallInspectionResult
{
    public bool IsSafe { get; set; }
    public double RiskScore { get; set; } // 0.0 to 1.0
    public string? ThreatCategory { get; set; }
    public string SanitizedPrompt { get; set; } = string.Empty;
}

public class ModelRoutingDecision
{
    public AiTaskComplexity Complexity { get; set; }
    public string SelectedModel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal EstimatedCostPerMillionTokens { get; set; }
    public int MaxOutputTokens { get; set; }
}

public record AiModelCatalogEntry(
    string ModelId,
    string Provider,
    string DisplayName,
    decimal CostPerMillionTokens,
    int ContextWindowTokens,
    bool IsAvailable,
    string Tier
);

public record ModelRoutingExecutionResult<T>(
    T Result,
    string PrimaryModel,
    string ActualModelUsed,
    string Provider,
    bool FallbackOccurred,
    string? FallbackReason,
    int LatencyMs
);

public record TenantTokenQuotaStatus(
    Guid TenantId,
    long MonthlyBudgetTokens,
    long ConsumedTokens,
    long RemainingTokens,
    decimal UsagePercentage,
    bool QuotaExceeded
);

public record ExecuteRoutedModelRequest(
    string Prompt,
    AiTaskComplexity Complexity = AiTaskComplexity.FastAndCheap,
    string? TaskHint = null
);
