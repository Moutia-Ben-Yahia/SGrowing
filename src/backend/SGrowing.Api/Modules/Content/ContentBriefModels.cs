using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Content;

public enum SearchIntent
{
    Informational,
    Commercial,
    Transactional,
    Navigational
}

public record ContentBriefRequest(
    string PrimaryKeyword,
    List<string>? SecondaryKeywords = null,
    string? TargetAudience = null,
    SearchIntent? Intent = null,
    string? BrandVoiceGuidelines = null,
    int? MinWordCount = null,
    int? MaxWordCount = null
);

public record OutlineSectionDto(
    string HeadingLevel,
    string Title,
    List<string> TargetSubtopics
);

public record SemanticKeywordDto(
    string Keyword,
    double RecommendedDensityPercent,
    bool IsMandatory
);

public record CompetitorBenchmarkDto(
    int AverageWordCount,
    int RecommendedHeadingsCount,
    int RecommendedImagesCount
);

public record ContentBrief(
    Guid BriefId,
    string PrimaryKeyword,
    string SearchIntent,
    int TargetWordCountMin,
    int TargetWordCountMax,
    string TargetReadingGrade,
    IReadOnlyList<OutlineSectionDto> OutlineSections,
    IReadOnlyList<SemanticKeywordDto> SemanticKeywords,
    IReadOnlyList<string> MandatoryQuestionsToAnswer,
    CompetitorBenchmarkDto CompetitorBenchmarks,
    IReadOnlyList<string> BrandVoiceRules,
    DateTime CreatedAtUtc
);

public record DraftGradeRequest(
    string PrimaryKeyword,
    List<string>? SecondaryKeywords,
    string DraftTitle,
    string DraftBody,
    Guid? BriefId = null
);

public record DraftGradeReport(
    double OverallScore,
    string Grade,
    int TotalWordCount,
    double WordCountScore,
    double PrimaryKeywordDensityPercent,
    double KeywordDensityScore,
    double FleschReadingEase,
    string ReadabilityLevel,
    double OutlineCoverageScore,
    IReadOnlyList<string> CoveredKeywords,
    IReadOnlyList<string> MissingKeywords,
    IReadOnlyList<string> QuestionsAnswered,
    IReadOnlyList<string> MissingQuestions,
    IReadOnlyList<string> ActionableRecommendations,
    DateTime GradedAtUtc
);
