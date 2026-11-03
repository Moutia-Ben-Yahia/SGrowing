using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Personalization.ContentPersonalization;

public sealed record PersonalizationOverview(
    int ActiveRulesCount,
    int SegmentCount,
    double AverageConversionLiftPct,
    long MonthlyPersonalizedImpressions,
    IReadOnlyList<FirmographicSegment> Segments,
    IReadOnlyList<PersonalizationRule> ActiveRules
);

public sealed record FirmographicSegment(
    string SegmentId,
    string Name,
    string TargetIndustry,
    string EmployeeRange,
    string RevenueTier,
    int ActiveAccountsCount
);

public sealed record PersonalizationRule(
    string RuleId,
    string RuleName,
    string SegmentId,
    string TargetUrlPattern,
    string SelectedHeroHeadline,
    string CallToActionText,
    double ConversionRatePct,
    int SampleSize
);

public sealed record EvaluatePersonalizationRequest(
    string VisitorIp,
    string? VisitorDomain,
    string Industry,
    int EstimatedEmployees,
    IReadOnlyList<string> IntentKeywords,
    string RequestedUrl
);

public sealed record PersonalizationEvaluationResult(
    string DecisionId,
    string MatchedSegmentId,
    string TailoredHeroHeadline,
    string TailoredSubheadline,
    string CallToActionText,
    string CallToActionUrl,
    IReadOnlyList<string> TailoredCustomerProofLogos,
    string TailoredPricingTier,
    double ConfidenceScore,
    long EvaluationLatencyMs
);

public sealed record RecordConversionRequest(
    string DecisionId,
    string RuleId,
    string ConversionType,
    decimal AttributedValueUsd
);

public sealed record ConversionReceipt(
    string ReceiptId,
    string DecisionId,
    string RuleId,
    string ConversionType,
    decimal AttributedValueUsd,
    double UpdatedVariantWeight,
    DateTime Timestamp
);
