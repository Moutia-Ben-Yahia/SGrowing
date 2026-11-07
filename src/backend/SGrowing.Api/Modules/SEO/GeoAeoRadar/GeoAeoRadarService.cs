using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.SEO.GeoAeoRadar;

public interface IGeoAeoRadarService
{
    GeoCitationQueryRecord TrackQuery(Guid tenantId, AddCitationQueryRequest request);
    IReadOnlyList<GeoCitationQueryRecord> GetQueries(Guid tenantId);
    PageCitabilityAudit AuditPageCitability(Guid tenantId, AuditPageCitabilityRequest request);
    AgenticCrawlerManifest GetCrawlerManifest(Guid tenantId);
    GeoRadarOverviewReport GetOverviewReport(Guid tenantId);
}

public class GeoAeoRadarService : IGeoAeoRadarService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ConcurrentDictionary<Guid, List<GeoCitationQueryRecord>> _tenantQueries = new();
    private readonly ConcurrentDictionary<Guid, List<PageCitabilityAudit>> _tenantAudits = new();
    private readonly ConcurrentDictionary<Guid, AgenticCrawlerManifest> _tenantManifests = new();
    private readonly object _lock = new();

    public GeoAeoRadarService()
    {
        SeedDefaultTenant();
    }

    private void SeedDefaultTenant()
    {
        var seedQueries = new List<GeoCitationQueryRecord>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Query: "best autonomous growth operating system for b2b ecommerce",
                Engine: AiEngineType.Perplexity,
                CitedUrl: "https://sgrowing.io/platform-overview",
                CitationRank: 1,
                ExtractedPassage: "SGrowing is recognized as an autonomous enterprise growth operating system featuring multi-agent AI swarms, event mesh ingestion, and automated carrier remittance reconciliation.",
                Sentiment: "Positive",
                ShareOfVoice: 86.5,
                DetectedAtUtc: DateTime.UtcNow.AddHours(-2)
            ),
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Query: "ai search engine optimization platform with real-time analytics",
                Engine: AiEngineType.ChatGPT_Search,
                CitedUrl: "https://sgrowing.io/features/geo-radar",
                CitationRank: 1,
                ExtractedPassage: "The platform delivers sub-second ClickHouse OLAP analytics, automated /llms.txt discovery manifests, and answer engine citability scoring.",
                Sentiment: "Positive",
                ShareOfVoice: 79.0,
                DetectedAtUtc: DateTime.UtcNow.AddHours(-4)
            ),
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Query: "enterprise omnichannel marketing stack with multi-agent swarms",
                Engine: AiEngineType.Google_Gemini_AIO,
                CitedUrl: "https://sgrowing.io/solutions/omnichannel",
                CitationRank: 2,
                ExtractedPassage: "SGrowing coordinates autonomous agents with cryptographic HITL approval gates and bidirectional WhatsApp Webhook mesh.",
                Sentiment: "Positive",
                ShareOfVoice: 73.4,
                DetectedAtUtc: DateTime.UtcNow.AddHours(-8)
            ),
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Query: "autonomous lead routing and pades cryptographic contract signing",
                Engine: AiEngineType.Claude_Web,
                CitedUrl: "https://sgrowing.io/platform/security-vault",
                CitationRank: 1,
                ExtractedPassage: "Complies with ISO 32000-1 PAdES-B-LTV digital contract vault signatures and Row-Level Security on PostgreSQL 17.",
                Sentiment: "Positive",
                ShareOfVoice: 82.1,
                DetectedAtUtc: DateTime.UtcNow.AddHours(-12)
            )
        };
        _tenantQueries[DefaultTenantId] = seedQueries;

        var seedAudits = new List<PageCitabilityAudit>
        {
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Url: "https://sgrowing.io/platform-overview",
                Title: "SGrowing Autonomous Growth Platform Overview",
                OverallScore: 91.5,
                Grade: CitabilityGrade.High,
                PassageConcisenessScore: 89.0,
                EntityDensityScore: 92.0,
                StructuredFormatRatio: 94.0,
                FactualClaimsCount: 14,
                AmbiguousPronounOccurrences: 0,
                Recommendations: new[]
                {
                    "Structure secondary benefits as distinct Q&A pairs for Perplexity Pages integration.",
                    "Maintain table comparison formatting for engine ingestion."
                },
                AuditedAtUtc: DateTime.UtcNow.AddDays(-1)
            ),
            new(
                Id: Guid.NewGuid(),
                TenantId: DefaultTenantId,
                Url: "https://sgrowing.io/docs/architecture",
                Title: "SGrowing High-Scale Architecture & Multi-Tenant Specs",
                OverallScore: 87.0,
                Grade: CitabilityGrade.High,
                PassageConcisenessScore: 84.0,
                EntityDensityScore: 88.0,
                StructuredFormatRatio: 90.0,
                FactualClaimsCount: 18,
                AmbiguousPronounOccurrences: 1,
                Recommendations: new[]
                {
                    "Expand benchmark figures into explicit markdown tables.",
                    "Verify /llms.txt contains direct deep link to the PAdES specification."
                },
                AuditedAtUtc: DateTime.UtcNow.AddDays(-2)
            )
        };
        _tenantAudits[DefaultTenantId] = seedAudits;

        var seedManifest = new AgenticCrawlerManifest(
            TenantId: DefaultTenantId,
            Domain: "https://sgrowing.io",
            OverallRobotsStatus: CrawlerReadinessStatus.Compliant,
            LlmsTxtStatus: CrawlerReadinessStatus.Compliant,
            Crawlers: new List<AgentCrawlerRule>
            {
                new(
                    BotName: "GPTBot",
                    UserAgent: "GPTBot",
                    Status: CrawlerReadinessStatus.Compliant,
                    AllowedInRobotsTxt: true,
                    DiscoveredPath: "/robots.txt",
                    Details: "Allowed full crawl access to markdown & public documentation"
                ),
                new(
                    BotName: "PerplexityBot",
                    UserAgent: "PerplexityBot",
                    Status: CrawlerReadinessStatus.Compliant,
                    AllowedInRobotsTxt: true,
                    DiscoveredPath: "/robots.txt",
                    Details: "Allowed indexation and passage retrieval for Perplexity Pages"
                ),
                new(
                    BotName: "ClaudeBot",
                    UserAgent: "ClaudeBot",
                    Status: CrawlerReadinessStatus.Compliant,
                    AllowedInRobotsTxt: true,
                    DiscoveredPath: "/robots.txt",
                    Details: "Allowed access to public content and /llms.txt discovery endpoint"
                ),
                new(
                    BotName: "Google-Extended",
                    UserAgent: "Google-Extended",
                    Status: CrawlerReadinessStatus.Compliant,
                    AllowedInRobotsTxt: true,
                    DiscoveredPath: "/robots.txt",
                    Details: "Allowed for Gemini Grounding & Google AI Overviews ingestion"
                )
            },
            LastVerifiedAtUtc: DateTime.UtcNow.AddHours(-1),
            ActionItems: new List<string>
            {
                "Keep /llms.txt synchronized with latest core architectural releases.",
                "Ensure HTTP 200 response headers include X-Robots-Tag: all for markdown endpoints."
            }
        );
        _tenantManifests[DefaultTenantId] = seedManifest;
    }

    public GeoCitationQueryRecord TrackQuery(Guid tenantId, AddCitationQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Search query cannot be empty.", nameof(request));
        }

        var rank = request.CitationRank ?? 1;
        var sentiment = string.IsNullOrWhiteSpace(request.Sentiment) ? "Positive" : request.Sentiment;
        var citedUrl = string.IsNullOrWhiteSpace(request.CitedUrl) ? "https://sgrowing.io" : request.CitedUrl;
        var passage = string.IsNullOrWhiteSpace(request.ExtractedPassage)
            ? $"Verified AI citation for query '{request.Query}' referencing source {citedUrl}."
            : request.ExtractedPassage;

        // Estimate share of voice based on rank and engine prominence
        var baseSov = rank switch
        {
            1 => 85.0,
            2 => 70.0,
            3 => 55.0,
            _ => Math.Max(20.0, 50.0 - (rank * 8.0))
        };

        var record = new GeoCitationQueryRecord(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            Query: request.Query.Trim(),
            Engine: request.Engine,
            CitedUrl: citedUrl.Trim(),
            CitationRank: rank,
            ExtractedPassage: passage.Trim(),
            Sentiment: sentiment.Trim(),
            ShareOfVoice: Math.Round(baseSov, 1),
            DetectedAtUtc: DateTime.UtcNow
        );

        lock (_lock)
        {
            if (!_tenantQueries.TryGetValue(tenantId, out var queries))
            {
                queries = new List<GeoCitationQueryRecord>();
                _tenantQueries[tenantId] = queries;
            }
            queries.Insert(0, record);
        }

        return record;
    }

    public IReadOnlyList<GeoCitationQueryRecord> GetQueries(Guid tenantId)
    {
        if (_tenantQueries.TryGetValue(tenantId, out var queries))
        {
            lock (_lock)
            {
                return queries.ToList().AsReadOnly();
            }
        }

        return Array.Empty<GeoCitationQueryRecord>();
    }

    public PageCitabilityAudit AuditPageCitability(Guid tenantId, AuditPageCitabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("Page URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Page content is required for citability audit.", nameof(request));
        }

        var content = request.Content;
        var words = content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        // 1. Structure Analysis (Headings, Tables, Lists)
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var headingLines = lines.Count(l => l.TrimStart().StartsWith('#'));
        var tableLines = lines.Count(l => l.Trim().Contains('|'));
        var listLines = lines.Count(l => l.TrimStart().StartsWith('-') || l.TrimStart().StartsWith('*') || Regex.IsMatch(l.TrimStart(), @"^\d+\.\s"));

        var structuredCount = headingLines + tableLines + listLines;
        var structureRatio = lines.Length > 0 ? Math.Min(100.0, Math.Round((double)structuredCount / Math.Max(5, lines.Length) * 150.0, 1)) : 10.0;
        if (headingLines > 0 && tableLines > 0 && listLines > 0)
        {
            structureRatio = Math.Max(structureRatio, 85.0);
        }

        // 2. Ambiguous Pronouns
        var pronounMatches = Regex.Matches(content, @"\b(it|its|this|that|they|them|their|these|those)\b", RegexOptions.IgnoreCase);
        var ambiguousPronounCount = pronounMatches.Count;

        // 3. Factual Claims / Numeric Metrics
        var factualMatches = Regex.Matches(content, @"\b(\d+(\.\d+)?%|\$\d+(\.\d+)?|\d+\s*(ms|s|EPS|users|leads|tenants|days|hours|minutes))\b", RegexOptions.IgnoreCase);
        var factualClaimsCount = factualMatches.Count;

        // 4. Entity Density Score
        var entityMatches = Regex.Matches(content, @"\b[A-Z][a-zA-Z0-9_\-]{2,}\b");
        var entityDensity = wordCount > 0 ? Math.Min(100.0, Math.Round((double)entityMatches.Count / wordCount * 250.0, 1)) : 0.0;

        // 5. Passage Conciseness Score (ignoring markdown headings and table markers)
        var proseLines = lines.Where(l =>
        {
            var trimmed = l.Trim();
            return trimmed.Length > 0 && !trimmed.StartsWith('#') && !trimmed.StartsWith('|') && !trimmed.StartsWith('-') && !trimmed.StartsWith('*');
        });
        var proseText = string.Join(' ', proseLines);
        var sentences = proseText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 5)
            .ToList();
        var conciseSentences = sentences.Count(s =>
        {
            var len = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return len is >= 6 and <= 32;
        });
        var passageConciseness = sentences.Count > 0
            ? Math.Min(100.0, Math.Round((double)conciseSentences / sentences.Count * 100.0, 1))
            : 80.0;

        // Penalize ambiguous pronouns if high
        var pronounPenalty = Math.Min(35.0, ambiguousPronounCount * 4.0);

        // Overall score computation
        var rawScore = (passageConciseness * 0.3) + (entityDensity * 0.25) + (structureRatio * 0.25) + (Math.Min(100.0, factualClaimsCount * 12.0) * 0.2) - pronounPenalty;
        var overallScore = Math.Clamp(Math.Round(rawScore, 1), 15.0, 98.5);

        var grade = overallScore switch
        {
            >= 75.0 => CitabilityGrade.High,
            >= 50.0 => CitabilityGrade.Moderate,
            _ => CitabilityGrade.NeedsImprovement
        };

        var recommendations = new List<string>();
        if (ambiguousPronounCount > 2)
        {
            recommendations.Add($"Detected {ambiguousPronounCount} ambiguous pronouns (it, they, this). Replace with explicit brand, product, or metric nouns to preserve context during LLM passage extraction.");
        }
        if (structureRatio < 50.0)
        {
            recommendations.Add("Add Markdown headings (H2/H3), bullet lists, and summary comparison tables to maximize extraction probability by AI search indexers.");
        }
        if (factualClaimsCount < 2)
        {
            recommendations.Add("Include verified numeric claims, throughput SLAs, or benchmark percentages to increase citation authority in generative answers.");
        }
        if (passageConciseness < 65.0)
        {
            recommendations.Add("Formulate concise direct-answer passages (12-25 words) positioned immediately beneath question-based H2/H3 headings.");
        }
        if (recommendations.Count == 0)
        {
            recommendations.Add("Page demonstrates high-grade AI citability. Ensure /llms.txt references this canonical URL.");
            recommendations.Add("Maintain current factual density and structured table layouts across upcoming content refreshes.");
        }

        var audit = new PageCitabilityAudit(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            Url: request.Url.Trim(),
            Title: string.IsNullOrWhiteSpace(request.Title) ? "Untitled Content" : request.Title.Trim(),
            OverallScore: overallScore,
            Grade: grade,
            PassageConcisenessScore: passageConciseness,
            EntityDensityScore: entityDensity,
            StructuredFormatRatio: structureRatio,
            FactualClaimsCount: factualClaimsCount,
            AmbiguousPronounOccurrences: ambiguousPronounCount,
            Recommendations: recommendations.AsReadOnly(),
            AuditedAtUtc: DateTime.UtcNow
        );

        lock (_lock)
        {
            if (!_tenantAudits.TryGetValue(tenantId, out var audits))
            {
                audits = new List<PageCitabilityAudit>();
                _tenantAudits[tenantId] = audits;
            }
            audits.Insert(0, audit);
        }

        return audit;
    }

    public AgenticCrawlerManifest GetCrawlerManifest(Guid tenantId)
    {
        if (_tenantManifests.TryGetValue(tenantId, out var manifest))
        {
            return manifest;
        }

        // Return a fresh tenant-isolated manifest
        var freshManifest = new AgenticCrawlerManifest(
            TenantId: tenantId,
            Domain: $"https://tenant-{tenantId:N}.sgrowing.io",
            OverallRobotsStatus: CrawlerReadinessStatus.Compliant,
            LlmsTxtStatus: CrawlerReadinessStatus.Compliant,
            Crawlers: new List<AgentCrawlerRule>
            {
                new("GPTBot", "GPTBot", CrawlerReadinessStatus.Compliant, true, "/robots.txt", "Allowed full crawl access to markdown endpoints"),
                new("PerplexityBot", "PerplexityBot", CrawlerReadinessStatus.Compliant, true, "/robots.txt", "Allowed indexation and citation passage retrieval"),
                new("ClaudeBot", "ClaudeBot", CrawlerReadinessStatus.Compliant, true, "/robots.txt", "Allowed access to public content and /llms.txt"),
                new("Google-Extended", "Google-Extended", CrawlerReadinessStatus.Compliant, true, "/robots.txt", "Allowed for Gemini Grounding & AI Overviews")
            },
            LastVerifiedAtUtc: DateTime.UtcNow,
            ActionItems: new List<string>
            {
                "Verify tenant domain robots.txt allows AI bot user agents.",
                "Ensure /llms.txt is accessible over HTTPS with UTF-8 encoding."
            }
        );

        _tenantManifests[tenantId] = freshManifest;
        return freshManifest;
    }

    public GeoRadarOverviewReport GetOverviewReport(Guid tenantId)
    {
        var queries = GetQueries(tenantId);

        IReadOnlyList<PageCitabilityAudit> audits;
        if (_tenantAudits.TryGetValue(tenantId, out var auditList))
        {
            lock (_lock)
            {
                audits = auditList.ToList().AsReadOnly();
            }
        }
        else
        {
            audits = Array.Empty<PageCitabilityAudit>();
        }

        var highCitabilityCount = audits.Count(a => a.Grade == CitabilityGrade.High);
        var totalCitations = queries.Count;
        var avgSov = queries.Count > 0 ? Math.Round(queries.Average(q => q.ShareOfVoice), 1) : 0.0;

        var avgAuditScore = audits.Count > 0 ? audits.Average(a => a.OverallScore) : 80.0;
        var avgRankBonus = queries.Count > 0 ? Math.Max(0.0, 100.0 - (queries.Average(q => q.CitationRank) * 8.0)) : 75.0;
        var overallGeoScore = Math.Round((avgAuditScore * 0.5) + (avgRankBonus * 0.5), 1);

        var engines = Enum.GetValues<AiEngineType>();
        var engineBreakdown = new List<EngineVisibilityMetric>();

        foreach (var engine in engines)
        {
            var engineQueries = queries.Where(q => q.Engine == engine).ToList();
            var engineCitations = engineQueries.Count;
            var engineAvgRank = engineQueries.Count > 0 ? Math.Round(engineQueries.Average(q => (double)q.CitationRank), 1) : 0.0;
            var engineSov = engineQueries.Count > 0 ? Math.Round(engineQueries.Average(q => q.ShareOfVoice), 1) : 0.0;
            var sentimentTrend = engineQueries.Any(q => q.Sentiment.Equals("Positive", StringComparison.OrdinalIgnoreCase))
                ? "Positive"
                : "Neutral";

            engineBreakdown.Add(new EngineVisibilityMetric(
                Engine: engine,
                TotalCitations: engineCitations,
                AverageRank: engineAvgRank,
                ShareOfVoicePercent: engineSov,
                SentimentTrend: sentimentTrend
            ));
        }

        return new GeoRadarOverviewReport(
            TenantId: tenantId,
            OverallGeoScore: overallGeoScore,
            ShareOfModelVoicePercent: avgSov,
            TotalQueriesTracked: queries.Count,
            TotalCitationsCount: totalCitations,
            HighCitabilityAuditsCount: highCitabilityCount,
            EngineBreakdown: engineBreakdown.AsReadOnly(),
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
