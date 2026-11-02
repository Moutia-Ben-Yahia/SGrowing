using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.SEO.TopicalAuthority;

public interface ITopicalAuthorityEngine
{
    AnalyzeTopicalMeshResult AnalyzeTopicalMesh(AnalyzeTopicalMeshRequest request);
    IReadOnlyList<InternalLinkOpportunity> GetInternalLinkOpportunities();
    ApplyLinkInjectionResult ApplyLinkInjection(ApplyLinkInjectionRequest request);
}

public class TopicalAuthorityEngine : ITopicalAuthorityEngine
{
    private readonly ConcurrentDictionary<string, TopicCluster> _clusters = new();
    private readonly ConcurrentDictionary<string, InternalLinkOpportunity> _opportunities = new();

    public TopicalAuthorityEngine()
    {
        // Seed initial clusters
        var c1 = new TopicCluster(
            ClusterId: "cluster_growth_marketing",
            PillarTopic: "Autonomous B2B Growth Engine",
            TargetKeyword: "b2b growth automation",
            TopicalAuthorityScore: 88.5,
            TotalSubArticles: 14,
            MissingSubTopicsCount: 2,
            CoveredEntities: new List<string> { "CDP Identity Resolution", "Dynamic Funnels", "Multi-Touch Attribution", "Reverse IP De-Anonymization" },
            RecommendedClusterExpansions: new List<string> { "Account-Based Marketing orchestration metrics", "Zero-party data capture playbooks" }
        );

        var c2 = new TopicCluster(
            ClusterId: "cluster_voice_ai",
            PillarTopic: "Enterprise Conversational Voice AI",
            TargetKeyword: "voice ai sdr",
            TopicalAuthorityScore: 74.0,
            TotalSubArticles: 9,
            MissingSubTopicsCount: 4,
            CoveredEntities: new List<string> { "WebRTC Audio Streaming", "Objection Classification", "Turn-taking Latency Optimization" },
            RecommendedClusterExpansions: new List<string> { "Enterprise telephony SIP trunking benchmarks", "SOC2 HIPAA compliant voice recording" }
        );

        _clusters[c1.ClusterId] = c1;
        _clusters[c2.ClusterId] = c2;

        // Seed initial internal link opportunities
        var opp1 = new InternalLinkOpportunity(
            OpportunityId: "opp_link_001",
            SourcePageUrl: "/blog/how-to-scale-b2b-outbound",
            SourcePageTitle: "How to Scale B2B Outbound Without Burning Reps",
            TargetPageUrl: "/solutions/b2b-growth-automation",
            TargetPillarKeyword: "b2b growth automation",
            RecommendedAnchorText: "autonomous B2B growth automation",
            SentenceContextPreview: "Companies implementing ...autonomous B2B growth automation... typically see a 4x surge in qualified pipeline meetings.",
            PredictedRelevanceScore: 0.96
        );

        var opp2 = new InternalLinkOpportunity(
            OpportunityId: "opp_link_002",
            SourcePageUrl: "/blog/what-is-reverse-ip-lookup",
            SourcePageTitle: "The Comprehensive Guide to Reverse IP De-Anonymization",
            TargetPageUrl: "/platform/cdp-identity-graph",
            TargetPillarKeyword: "cdp identity resolution",
            RecommendedAnchorText: "enterprise CDP identity resolution graph",
            SentenceContextPreview: "When paired with an ...enterprise CDP identity resolution graph..., anonymous web sessions resolve into high-intent accounts.",
            PredictedRelevanceScore: 0.93
        );

        _opportunities[opp1.OpportunityId] = opp1;
        _opportunities[opp2.OpportunityId] = opp2;
    }

    public AnalyzeTopicalMeshResult AnalyzeTopicalMesh(AnalyzeTopicalMeshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domain = string.IsNullOrWhiteSpace(request.DomainOrBaseUrl) ? "sgrowing.io" : request.DomainOrBaseUrl.Trim();
        var clustersList = _clusters.Values.ToList();
        var avgScore = clustersList.Any() ? clustersList.Average(c => c.TopicalAuthorityScore) : 75.0;

        return new AnalyzeTopicalMeshResult(
            Domain: domain,
            OverallTopicalAuthorityScore: Math.Round(avgScore, 1),
            TotalClustersAudited: clustersList.Count,
            TotalInternalLinkOpportunities: _opportunities.Count,
            Clusters: clustersList,
            AnalyzedAtUtc: DateTime.UtcNow
        );
    }

    public IReadOnlyList<InternalLinkOpportunity> GetInternalLinkOpportunities()
    {
        return _opportunities.Values.OrderByDescending(o => o.PredictedRelevanceScore).ToList();
    }

    public ApplyLinkInjectionResult ApplyLinkInjection(ApplyLinkInjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tag = $"<a href=\"{request.TargetPageUrl}\" title=\"{request.AnchorText}\" class=\"internal-pillar-link\">{request.AnchorText}</a>";

        return new ApplyLinkInjectionResult(
            Applied: true,
            OpportunityId: request.OpportunityId,
            ModifiedSourcePageUrl: request.SourcePageUrl,
            InjectedHtmlTag: tag,
            AppliedAtUtc: DateTime.UtcNow
        );
    }
}
