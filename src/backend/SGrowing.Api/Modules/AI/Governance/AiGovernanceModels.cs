using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.AI.Governance;

public enum AiProvider
{
    GeminiPro,
    ClaudeSonnet,
    Gpt4o,
    OllamaLocal
}

public enum SecurityThreatLevel
{
    Clean,
    Low,
    Medium,
    High,
    Critical
}

public enum TaskComplexity
{
    SimpleClassification,
    StandardCopywriting,
    ComplexReasoning,
    CodeOrSqlGeneration
}

public record PromptInspectionResult(
    bool IsBlocked,
    SecurityThreatLevel ThreatLevel,
    List<string> DetectedThreats,
    string SanitizedPrompt,
    int RedactedPiiTokensCount,
    DateTime EvaluatedAtUtc
);

public record ModelRouteDecision(
    AiProvider PrimaryProvider,
    AiProvider FallbackProvider,
    int MaxTokens,
    decimal EstimatedCostPer1kTokens,
    int EstimatedLatencyMs,
    string Reason
);

public record OutputAuditResult(
    bool PassedCompliance,
    double ToxicityScore,
    double HallucinationRiskScore,
    List<string> FlaggedTerms,
    string AuditId,
    DateTime AuditedAtUtc
);

public record InspectPromptRequest(
    string TenantId,
    string RawPrompt,
    bool EnablePiiRedaction = true
);

public record RouteModelRequest(
    string TenantId,
    TaskComplexity Complexity,
    int MaxLatencyMsAllowed = 2000,
    bool PreferLocal = false
);

public record AuditOutputRequest(
    string TenantId,
    string GeneratedOutput,
    decimal? MaxPriceCommitmentAllowed = null
);
