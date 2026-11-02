using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.SEO.TopicalAuthority;

public record TopicCluster(
    string ClusterId,
    string PillarTopic,
    string TargetKeyword,
    double TopicalAuthorityScore, // 0 - 100
    int TotalSubArticles,
    int MissingSubTopicsCount,
    List<string> CoveredEntities,
    List<string> RecommendedClusterExpansions
);

public record InternalLinkOpportunity(
    string OpportunityId,
    string SourcePageUrl,
    string SourcePageTitle,
    string TargetPageUrl,
    string TargetPillarKeyword,
    string RecommendedAnchorText,
    string SentenceContextPreview,
    double PredictedRelevanceScore // 0.0 - 1.0
);

public record AnalyzeTopicalMeshRequest(
    string DomainOrBaseUrl,
    List<string> ExistingArticleUrls
);

public record AnalyzeTopicalMeshResult(
    string Domain,
    double OverallTopicalAuthorityScore, // 0 - 100
    int TotalClustersAudited,
    int TotalInternalLinkOpportunities,
    List<TopicCluster> Clusters,
    DateTime AnalyzedAtUtc
);

public record ApplyLinkInjectionRequest(
    string OpportunityId,
    string SourcePageUrl,
    string TargetPageUrl,
    string AnchorText
);

public record ApplyLinkInjectionResult(
    bool Applied,
    string OpportunityId,
    string ModifiedSourcePageUrl,
    string InjectedHtmlTag,
    DateTime AppliedAtUtc
);
