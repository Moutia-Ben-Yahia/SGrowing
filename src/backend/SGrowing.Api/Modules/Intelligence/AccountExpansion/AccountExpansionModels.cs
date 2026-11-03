using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.AccountExpansion;

public record EntitlementUtilization(
    string MetricName,
    int CurrentUsed,
    int AllocatedLimit,
    double UtilizationPct
);

public record ExpansionOpportunity(
    string OpportunityId,
    string TenantId,
    string AccountName,
    int ExpansionPropensityScore,
    decimal CurrentArrUsd,
    decimal ProjectedExpansionArrUsd,
    string RecommendedTierOrAddon,
    List<EntitlementUtilization> SaturatedMetrics,
    bool IsQuoteGenerated
);

public record GenerateExpansionQuoteRequest(
    string OpportunityId,
    decimal DiscountPercentage
);

public record IngestUtilizationSignalRequest(
    string TenantId,
    string MetricName,
    int UnitsAdded
);
