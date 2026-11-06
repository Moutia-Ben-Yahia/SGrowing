using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.SEO;

public record GeoCitabilityAuditRequest(
    string Title,
    string Content,
    string? Url = null,
    string? TargetEngine = "All"
);

public record GeoCitabilityAuditResult(
    double OverallCitabilityScore,
    string CitabilityGrade,
    double PassageConcisenessScore,
    double EntityDensityScore,
    double StructuredFormatRatio,
    double QandAAdjacencyScore,
    int FactualClaimsCount,
    int AmbiguousPronounOccurrences,
    IReadOnlyList<string> ActionableRecommendations,
    string FormattedLlmsSnippet,
    DateTime AuditedAtUtc
);

public record LlmsSectionDto(
    string Title,
    string Url,
    string Summary
);

public record LlmsTxtGenerationRequest(
    string BrandName,
    string BrandTagline,
    string PrimaryDomain,
    List<string> CoreCapabilities,
    List<LlmsSectionDto> KeySections,
    string? OptionalFullDocumentationMarkdown = null
);

public record LlmsTxtGenerationResult(
    string LlmsTxt,
    string LlmsFullTxt,
    DateTime GeneratedAtUtc
);
