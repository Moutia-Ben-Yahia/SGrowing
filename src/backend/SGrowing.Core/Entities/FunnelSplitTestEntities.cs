using SGrowing.Core.Common;

namespace SGrowing.Core.Entities;

public class FunnelSplitTestRecord : TenantScopedEntity
{
    public Guid FunnelRunId { get; set; }
    public string Status { get; set; } = "Active";

    // Variant A (Control)
    public string VariantA_Name { get; set; } = "Variant A (Benefit-First)";
    public string VariantA_Headline { get; set; } = string.Empty;
    public double VariantA_TrafficRatio { get; set; } = 50.0;
    public int VariantA_Views { get; set; }
    public int VariantA_Conversions { get; set; }

    // Variant B (Challenger)
    public string VariantB_Name { get; set; } = "Variant B (Urgency & COD)";
    public string VariantB_Headline { get; set; } = string.Empty;
    public double VariantB_TrafficRatio { get; set; } = 50.0;
    public int VariantB_Views { get; set; }
    public int VariantB_Conversions { get; set; }

    // Derived Bayesian evaluation
    public string? WinnerVariant { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateFunnelSplitTestRequest(
    string? VariantA_Name,
    string? VariantA_Headline,
    double? VariantA_TrafficRatio,
    string? VariantB_Name,
    string? VariantB_Headline,
    double? VariantB_TrafficRatio
);

public record RecordSplitTestEventRequest(
    string VariantName, // "A" or "B" or matching name
    string EventType    // "view" or "conversion"
);

public record FunnelSplitTestSummaryDto(
    Guid TestId,
    Guid FunnelRunId,
    string Status,
    string VariantA_Name,
    string VariantA_Headline,
    double VariantA_TrafficRatio,
    int VariantA_Views,
    int VariantA_Conversions,
    double VariantA_ConversionRate,
    string VariantB_Name,
    string VariantB_Headline,
    double VariantB_TrafficRatio,
    int VariantB_Views,
    int VariantB_Conversions,
    double VariantB_ConversionRate,
    double ConversionRateDeltaPercent,
    string? WinnerVariant,
    double ConfidenceScore,
    string Recommendation
);
