namespace SGrowing.Api.Modules.Marketing.Copywriting;

/// <summary>
/// Request to generate a resolved spintax copy variation.
/// </summary>
public sealed record SpintaxGenerateRequest(
    string Template,
    int? Seed = null,
    IDictionary<string, string>? Variables = null,
    string? TemplateKey = null);

/// <summary>
/// Response containing the resolved spintax copy.
/// </summary>
public sealed record SpintaxGenerateResponse(
    bool Success,
    string? Message,
    long PermutationSpace,
    string? Error = null);

/// <summary>
/// Request to perform combinatorial and entropy analysis on a spintax template.
/// </summary>
public sealed record SpintaxAnalyzeRequest(
    string Template,
    int SampleSize = 100);

/// <summary>
/// Response holding the spintax combinatorial analysis results.
/// </summary>
public sealed record SpintaxAnalyzeResponse(
    bool Success,
    SpintaxAnalysisResult? Analysis,
    string? Error = null);

/// <summary>
/// Detailed mathematical and structural analysis of a spintax template.
/// </summary>
public sealed record SpintaxAnalysisResult(
    string Template,
    long TotalPermutations,
    double ShannonEntropy,
    int SampleSize,
    IReadOnlyList<string> SampleOutputs);

/// <summary>
/// Request to evaluate copywriting text against voice guidelines, dialect rules, and spam filters.
/// </summary>
public sealed record CopyAuditRequest(
    string Text,
    string Channel = "SMS",
    bool IsCommercial = true,
    int? MinLength = null,
    int? MaxLength = null,
    double? MinFormality = null,
    double? MaxFormality = null,
    double? MaxUrgency = null);

/// <summary>
/// Result of multi-lingual dialect detection and classification.
/// </summary>
public sealed record DialectClassification(
    string PrimaryDialect,
    double Confidence,
    IReadOnlyList<string> DetectedKeywords,
    IReadOnlyDictionary<string, double> DialectScores);

/// <summary>
/// Result of telecom spam filtering and high-risk trigger analysis.
/// </summary>
public sealed record SpamAuditResult(
    bool IsSpamSuspected,
    IReadOnlyList<string> Violations,
    double CapsRatio,
    int ExclamationCount,
    IReadOnlyList<string> MatchedSpamPhrases);

/// <summary>
/// Brand voice dimension scores and length check.
/// </summary>
public sealed record VoiceScoreResult(
    double FormalityScore,
    double UrgencyScore,
    int CharacterCount,
    bool LengthCompliant,
    string? LengthViolation = null);

/// <summary>
/// Regulatory compliance verification (e.g. opt-out footer).
/// </summary>
public sealed record RegulatoryComplianceResult(
    bool IsCompliant,
    bool RequiresOptOut,
    bool HasOptOut,
    string? ViolationDetails = null);

/// <summary>
/// Comprehensive copywriting audit result.
/// </summary>
public sealed record CopyAuditResult(
    bool IsApproved,
    VoiceScoreResult VoiceScores,
    DialectClassification Dialect,
    SpamAuditResult SpamAudit,
    RegulatoryComplianceResult Compliance,
    IReadOnlyList<string> Recommendations);

/// <summary>
/// API response wrapping copy audit result.
/// </summary>
public sealed record CopyAuditResponse(
    bool Success,
    CopyAuditResult? Audit,
    string? Error = null);

/// <summary>
/// Ingested interaction event for Bayesian bandit learning.
/// </summary>
public sealed record CopyInteractionEvent(
    string EventType,
    DateTime TimestampUtc,
    double Weight = 1.0,
    IDictionary<string, string>? Metadata = null);

/// <summary>
/// Represents an arm in the Thompson Sampling bandit distribution.
/// </summary>
public sealed record VariantArm(
    string VariantId,
    double Alpha,
    double Beta,
    int Impressions,
    int Opens,
    int Clicks,
    int Conversions,
    int Unsubscribes,
    DateTime LastUpdatedUtc);

/// <summary>
/// Bandit arm selection decision for a campaign delivery.
/// </summary>
public sealed record VariantSelectionResult(
    string CampaignId,
    string SelectedVariantId,
    double SampledTheta,
    double ExpectedValue,
    double Variance,
    DateTime SelectedAtUtc);

/// <summary>
/// Result of recording feedback on a bandit arm.
/// </summary>
public sealed record VariantFeedbackResult(
    bool Success,
    string CampaignId,
    string VariantId,
    double NewAlpha,
    double NewBeta,
    double NewExpectedValue,
    string Message);

/// <summary>
/// Statistical metrics for an individual copy variant.
/// </summary>
public sealed record VariantPerformanceMetric(
    string VariantId,
    double Alpha,
    double Beta,
    double ExpectedValue,
    double Variance,
    double ConversionRate,
    int Impressions,
    int Conversions,
    double Regret);

/// <summary>
/// Overall campaign bandit performance and regret statistics.
/// </summary>
public sealed record CampaignBanditPerformance(
    string CampaignId,
    string BestVariantId,
    double BestExpectedValue,
    double TotalEstimatedRegret,
    int TotalInteractions,
    IReadOnlyList<VariantPerformanceMetric> Variants);

/// <summary>
/// Request to select the optimal variant for a campaign.
/// </summary>
public sealed record BanditSelectRequest(
    string CampaignId,
    IReadOnlyList<string> VariantIds);

/// <summary>
/// Response containing the selected variant.
/// </summary>
public sealed record BanditSelectResponse(
    bool Success,
    VariantSelectionResult? Selection,
    string? Error = null);

/// <summary>
/// Request to record feedback on a copy variant.
/// </summary>
public sealed record BanditFeedbackRequest(
    string CampaignId,
    string VariantId,
    string EventType,
    double? Weight = null);

/// <summary>
/// Response confirming recorded bandit feedback.
/// </summary>
public sealed record BanditFeedbackResponse(
    bool Success,
    VariantFeedbackResult? Result,
    string? Error = null);

/// <summary>
/// Response containing campaign bandit performance stats.
/// </summary>
public sealed record BanditStatsResponse(
    bool Success,
    CampaignBanditPerformance? Performance,
    string? Error = null);
