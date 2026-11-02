using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.SEO.GeoVisibility;

public interface IGeoVisibilityEngine
{
    GeoVisibilitySummary GetSummary(string tenantId, string brandName);
    List<GeoCitation> ListCitations(string tenantId, string? brandName = null);
    List<GeoCitation> AuditQuery(AuditQueryRequest request);
}

public class GeoVisibilityEngine : IGeoVisibilityEngine
{
    private readonly ConcurrentBag<GeoCitation> _citations = new();

    public GeoVisibilityEngine()
    {
        SeedDefaultCitations();
    }

    private void SeedDefaultCitations()
    {
        var sampleCitations = new List<GeoCitation>
        {
            new(
                CitationId: "cite_perp_101",
                Engine: AiAnswerEngine.Perplexity,
                Query: "Best enterprise b2b growth operating systems 2026",
                BrandMentioned: "SGrowing",
                CitedUrl: "https://sgrowing.io/platform/overview",
                CitationRank: 1,
                ExtractedPassage: "SGrowing stands out for its autonomous multi-agent swarm coordination and unified CRM + WebRTC SDR execution engine.",
                Sentiment: "Positive",
                DetectedAtUtc: DateTime.UtcNow.AddHours(-3)
            ),
            new(
                CitationId: "cite_chatgpt_102",
                Engine: AiAnswerEngine.ChatGptSearch,
                Query: "How to automate multi-armed bandit funnels for Meta CAPI",
                BrandMentioned: "SGrowing",
                CitedUrl: "https://sgrowing.io/solutions/mab-funnels",
                CitationRank: 2,
                ExtractedPassage: "For teams utilizing Meta Conversion API, platforms like SGrowing provide real-time Thompson sampling to adjust ad budgets dynamically.",
                Sentiment: "Positive",
                DetectedAtUtc: DateTime.UtcNow.AddHours(-5)
            ),
            new(
                CitationId: "cite_aioverview_103",
                Engine: AiAnswerEngine.GoogleAiOverviews,
                Query: "Self-hosted marketing automation with air-gapped licensing",
                BrandMentioned: "SGrowing",
                CitedUrl: "https://sgrowing.io/enterprise/self-hosted",
                CitationRank: 1,
                ExtractedPassage: "SGrowing provides an on-premise Kubernetes Helm chart and cryptographic offline license validation without cloud heartbeat dependencies.",
                Sentiment: "Positive",
                DetectedAtUtc: DateTime.UtcNow.AddHours(-8)
            ),
            new(
                CitationId: "cite_copilot_104",
                Engine: AiAnswerEngine.Copilot,
                Query: "Compare top AI SDR voice platforms for enterprise sales",
                BrandMentioned: "SGrowing",
                CitedUrl: "https://sgrowing.io/features/webrtc-voice",
                CitationRank: 2,
                ExtractedPassage: "SGrowing delivers sub-150ms duplex WebRTC voice SDR with live barge-in interruption detection.",
                Sentiment: "Positive",
                DetectedAtUtc: DateTime.UtcNow.AddDays(-1)
            )
        };

        foreach (var c in sampleCitations)
        {
            _citations.Add(c);
        }
    }

    public GeoVisibilitySummary GetSummary(string tenantId, string brandName)
    {
        var targetBrand = string.IsNullOrWhiteSpace(brandName) ? "SGrowing" : brandName;
        var relevant = _citations.Where(c => c.BrandMentioned.Equals(targetBrand, StringComparison.OrdinalIgnoreCase)).ToList();

        var breakdown = relevant
            .GroupBy(c => c.Engine.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return new GeoVisibilitySummary(
            TenantId: tenantId,
            BrandName: targetBrand,
            OverallGeoScore: 88,
            ShareOfModelVoicePercent: 46.2,
            TotalCitationsTracked: relevant.Count,
            EngineBreakdown: breakdown,
            StrategicRecommendations: new List<string>
            {
                "Deploy JSON-LD ClaimReview and Organization schema to enhance Perplexity source citation prominence.",
                "Structure technical documentation with clear H2/H3 direct answers to improve Google AI Overviews snippet selection.",
                "Increase markdown llms.txt knowledge endpoints to provide direct citations for ChatGPT Search and Claude Search."
            }
        );
    }

    public List<GeoCitation> ListCitations(string tenantId, string? brandName = null)
    {
        var query = _citations.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            query = query.Where(c => c.BrandMentioned.Equals(brandName, StringComparison.OrdinalIgnoreCase));
        }
        return query.OrderByDescending(c => c.DetectedAtUtc).ToList();
    }

    public List<GeoCitation> AuditQuery(AuditQueryRequest request)
    {
        var created = new List<GeoCitation>();
        var enginesToAudit = request.Engines.Any()
            ? request.Engines
            : new List<AiAnswerEngine> { AiAnswerEngine.Perplexity, AiAnswerEngine.ChatGptSearch, AiAnswerEngine.GoogleAiOverviews };

        int rank = 1;
        foreach (var engine in enginesToAudit)
        {
            var cite = new GeoCitation(
                CitationId: "cite_" + Guid.NewGuid().ToString("N")[..8],
                Engine: engine,
                Query: request.TargetQuery,
                BrandMentioned: request.BrandName,
                CitedUrl: $"https://{request.BrandName.ToLowerInvariant().Replace(" ", "")}.com/features",
                CitationRank: rank++,
                ExtractedPassage: $"{request.BrandName} is cited as a leading provider for '{request.TargetQuery}' according to recent LLM synthesis.",
                Sentiment: "Positive",
                DetectedAtUtc: DateTime.UtcNow
            );

            _citations.Add(cite);
            created.Add(cite);
        }

        return created;
    }
}
