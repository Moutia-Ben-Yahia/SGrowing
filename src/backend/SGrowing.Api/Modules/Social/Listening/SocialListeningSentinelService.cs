using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Social.Listening;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SocialPlatform
{
    TwitterX,
    Instagram,
    Facebook,
    LinkedIn,
    Trustpilot,
    GoogleReviews
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrisisThreatLevel
{
    NormalGreen,
    ElevatedYellow,
    HighAlertOrange,
    CrisisRedEmergency
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SentinelActionType
{
    AlertPrLeadership,
    PauseMarketingBroadcasts,
    DeployCannedResponse,
    EscalateToExecutive
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MentionSentiment
{
    Positive,
    Neutral,
    Negative,
    SevereHostile
}

#endregion

#region Models & DTOs

public record SocialMentionRecord
{
    public Guid MentionId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public SocialPlatform Platform { get; init; }
    public string AuthorHandle { get; init; } = string.Empty;
    public int AuthorFollowersCount { get; init; }
    public string ContentText { get; init; } = string.Empty;
    public MentionSentiment Sentiment { get; set; } = MentionSentiment.Neutral;
    public double SentimentScore { get; set; } = 0.0;
    public List<string> DetectedCrisisKeywords { get; init; } = new();
    public CrisisThreatLevel ThreatLevel { get; set; } = CrisisThreatLevel.NormalGreen;
    public DateTime PublishedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime IngestedAtUtc { get; init; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; }
}

public record CrisisRule(
    Guid RuleId,
    Guid TenantId,
    string Name,
    List<string> TriggerKeywords,
    int NegativeVelocityThresholdPerHour,
    List<SentinelActionType> AutoActions,
    bool IsActive
);

public record CrisisIncident(
    Guid IncidentId,
    Guid TenantId,
    DateTime TriggeredAtUtc,
    CrisisThreatLevel ThreatLevel,
    string TriggerReason,
    List<Guid> TriggerMentionIds,
    bool IsBroadcastPaused,
    List<string> ActionsExecuted,
    DateTime? ResolvedAtUtc = null,
    string? MitigationNotes = null
);

public record SentimentTrendPoint(
    DateTime HourUtc,
    int PositiveCount,
    int NeutralCount,
    int NegativeCount,
    double AverageSentiment
);

public record SocialListeningSummary(
    Guid TenantId,
    CrisisThreatLevel CurrentThreatLevel,
    int TotalMentions24h,
    double NegativeVelocityRatio,
    bool IsMarketingBroadcastHalted,
    int ActiveCrisisCount,
    List<SocialMentionRecord> RecentCrisisMentions
);

public record IngestSocialMentionRequest(
    Guid TenantId,
    SocialPlatform Platform,
    string AuthorHandle,
    int AuthorFollowersCount,
    string ContentText,
    DateTime? PublishedAtUtc = null
);

public record CreateCrisisRuleRequest(
    Guid TenantId,
    string Name,
    List<string> TriggerKeywords,
    int NegativeVelocityThresholdPerHour = 5,
    List<SentinelActionType>? AutoActions = null
);

public record ResolveCrisisIncidentRequest(
    Guid TenantId,
    Guid IncidentId,
    string MitigationNotes
);

#endregion

public interface ISocialListeningSentinelService
{
    Task<SocialMentionRecord> IngestMentionAsync(IngestSocialMentionRequest request, CancellationToken ct = default);
    Task<List<SocialMentionRecord>> ListMentionsAsync(Guid tenantId, SocialPlatform? platform = null, MentionSentiment? sentiment = null, CancellationToken ct = default);

    Task<CrisisRule> CreateCrisisRuleAsync(CreateCrisisRuleRequest request, CancellationToken ct = default);
    Task<List<CrisisRule>> GetCrisisRulesAsync(Guid tenantId, CancellationToken ct = default);

    Task<CrisisIncident?> ResolveCrisisIncidentAsync(ResolveCrisisIncidentRequest request, CancellationToken ct = default);
    Task<List<CrisisIncident>> GetActiveCrisisIncidentsAsync(Guid tenantId, CancellationToken ct = default);

    Task<List<SentimentTrendPoint>> GetSentimentTrendsAsync(Guid tenantId, int hours = 24, CancellationToken ct = default);
    Task<SocialListeningSummary> GetSocialListeningSummaryAsync(Guid tenantId, CancellationToken ct = default);
}

public class SocialListeningSentinelService : ISocialListeningSentinelService
{
    private readonly ConcurrentDictionary<Guid, SocialMentionRecord> _mentions = new();
    private readonly ConcurrentDictionary<Guid, CrisisRule> _rules = new();
    private readonly ConcurrentDictionary<Guid, CrisisIncident> _incidents = new();
    private readonly ConcurrentDictionary<Guid, bool> _broadcastHaltedState = new();

    private static readonly string[] SevereNegativeKeywords = { "scam", "fraud", "arnaque", "boycott", "lawsuit", "police", "vol", "thief", "steal", "outage", "catastrophe" };
    private static readonly string[] ModerateNegativeKeywords = { "broken", "terrible", "worst", "unacceptable", "delayed", "angry", "furious", "bad", "slow" };
    private static readonly string[] PositiveKeywords = { "love", "great", "excellent", "fast", "bravo", "perfect", "recommend", "best", "thanks", "chokran" };

    public SocialListeningSentinelService()
    {
        // Seed default enterprise crisis rule
        var defaultRule = new CrisisRule(
            Guid.NewGuid(),
            Guid.Empty,
            "Brand Defense Sentinel",
            SevereNegativeKeywords.ToList(),
            5,
            new List<SentinelActionType> { SentinelActionType.AlertPrLeadership, SentinelActionType.PauseMarketingBroadcasts },
            true
        );
        _rules[defaultRule.RuleId] = defaultRule;
    }

    public Task<SocialMentionRecord> IngestMentionAsync(IngestSocialMentionRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentText)) throw new ArgumentException("ContentText is required.", nameof(request));

        var textLower = request.ContentText.ToLowerInvariant();
        var detectedKeywords = new List<string>();

        // 1. Analyze Sentiment
        double score = 0.0;
        var sentiment = MentionSentiment.Neutral;

        var foundSevere = SevereNegativeKeywords.Where(k => textLower.Contains(k)).ToList();
        detectedKeywords.AddRange(foundSevere);
        bool hasSevere = foundSevere.Count > 0;

        var foundModerate = ModerateNegativeKeywords.Where(k => textLower.Contains(k)).ToList();
        detectedKeywords.AddRange(foundModerate);
        bool hasModerate = foundModerate.Count > 0;

        int posCount = PositiveKeywords.Count(k => textLower.Contains(k));

        if (hasSevere)
        {
            sentiment = MentionSentiment.SevereHostile;
            score = -0.90;
        }
        else if (hasModerate)
        {
            sentiment = MentionSentiment.Negative;
            score = -0.55;
        }
        else if (posCount > 0)
        {
            sentiment = MentionSentiment.Positive;
            score = Math.Min(1.0, 0.40 * posCount);
        }

        // 2. Assess Threat Level
        var threat = CrisisThreatLevel.NormalGreen;
        if (sentiment == MentionSentiment.SevereHostile || (sentiment == MentionSentiment.Negative && request.AuthorFollowersCount >= 10_000))
        {
            threat = request.AuthorFollowersCount >= 50_000 ? CrisisThreatLevel.CrisisRedEmergency : CrisisThreatLevel.HighAlertOrange;
        }
        else if (sentiment == MentionSentiment.Negative)
        {
            threat = CrisisThreatLevel.ElevatedYellow;
        }

        var now = DateTime.UtcNow;
        var mention = new SocialMentionRecord
        {
            TenantId = request.TenantId,
            Platform = request.Platform,
            AuthorHandle = request.AuthorHandle?.Trim() ?? "anonymous",
            AuthorFollowersCount = request.AuthorFollowersCount,
            ContentText = request.ContentText.Trim(),
            Sentiment = sentiment,
            SentimentScore = score,
            DetectedCrisisKeywords = detectedKeywords.Distinct().ToList(),
            ThreatLevel = threat,
            PublishedAtUtc = request.PublishedAtUtc ?? now,
            IngestedAtUtc = now,
            IsAcknowledged = false
        };

        _mentions[mention.MentionId] = mention;

        // 3. Evaluate Crisis Velocity & Rules
        EvaluateCrisisTrigger(request.TenantId, mention);

        return Task.FromResult(mention);
    }

    private void EvaluateCrisisTrigger(Guid tenantId, SocialMentionRecord latestMention)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentNegatives = _mentions.Values
            .Where(m => m.TenantId == tenantId &&
                        m.IngestedAtUtc >= oneHourAgo &&
                        (m.Sentiment == MentionSentiment.Negative || m.Sentiment == MentionSentiment.SevereHostile))
            .ToList();

        var rules = _rules.Values.Where(r => (r.TenantId == tenantId || r.TenantId == Guid.Empty) && r.IsActive).ToList();

        foreach (var rule in rules)
        {
            bool keywordTriggered = rule.TriggerKeywords.Any(k => latestMention.DetectedCrisisKeywords.Contains(k));
            bool velocityTriggered = recentNegatives.Count >= rule.NegativeVelocityThresholdPerHour;

            if (keywordTriggered || velocityTriggered)
            {
                bool pauseBroadcasts = rule.AutoActions.Contains(SentinelActionType.PauseMarketingBroadcasts);
                if (pauseBroadcasts)
                {
                    _broadcastHaltedState[tenantId] = true;
                }

                var actionsExecuted = rule.AutoActions.Select(a => $"Executed {a} at {DateTime.UtcNow:O}").ToList();

                var incident = new CrisisIncident(
                    Guid.NewGuid(),
                    tenantId,
                    DateTime.UtcNow,
                    latestMention.ThreatLevel == CrisisThreatLevel.CrisisRedEmergency ? CrisisThreatLevel.CrisisRedEmergency : CrisisThreatLevel.HighAlertOrange,
                    keywordTriggered ? $"Triggered by critical keyword: '{string.Join(", ", latestMention.DetectedCrisisKeywords)}'" : $"Negative mention velocity ({recentNegatives.Count}/hr) exceeded threshold ({rule.NegativeVelocityThresholdPerHour}/hr)",
                    recentNegatives.Select(m => m.MentionId).ToList(),
                    pauseBroadcasts,
                    actionsExecuted
                );

                _incidents[incident.IncidentId] = incident;
                break;
            }
        }
    }

    public Task<List<SocialMentionRecord>> ListMentionsAsync(Guid tenantId, SocialPlatform? platform = null, MentionSentiment? sentiment = null, CancellationToken ct = default)
    {
        var query = _mentions.Values.Where(m => m.TenantId == tenantId);
        if (platform.HasValue) query = query.Where(m => m.Platform == platform.Value);
        if (sentiment.HasValue) query = query.Where(m => m.Sentiment == sentiment.Value);

        var list = query.OrderByDescending(m => m.IngestedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<CrisisRule> CreateCrisisRuleAsync(CreateCrisisRuleRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Rule Name is required.", nameof(request));

        var rule = new CrisisRule(
            Guid.NewGuid(),
            request.TenantId,
            request.Name.Trim(),
            request.TriggerKeywords ?? new List<string>(),
            request.NegativeVelocityThresholdPerHour > 0 ? request.NegativeVelocityThresholdPerHour : 5,
            request.AutoActions ?? new List<SentinelActionType> { SentinelActionType.AlertPrLeadership },
            true
        );

        _rules[rule.RuleId] = rule;
        return Task.FromResult(rule);
    }

    public Task<List<CrisisRule>> GetCrisisRulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _rules.Values.Where(r => r.TenantId == tenantId || r.TenantId == Guid.Empty).ToList();
        return Task.FromResult(list);
    }

    public Task<CrisisIncident?> ResolveCrisisIncidentAsync(ResolveCrisisIncidentRequest request, CancellationToken ct = default)
    {
        if (_incidents.TryGetValue(request.IncidentId, out var incident) && incident.TenantId == request.TenantId)
        {
            var resolved = incident with
            {
                ResolvedAtUtc = DateTime.UtcNow,
                MitigationNotes = request.MitigationNotes?.Trim() ?? "Resolved by PR team"
            };

            _incidents[incident.IncidentId] = resolved;

            // If no other active incidents, lift broadcast halt
            var otherActive = _incidents.Values.Any(i => i.TenantId == request.TenantId && i.ResolvedAtUtc == null);
            if (!otherActive)
            {
                _broadcastHaltedState[request.TenantId] = false;
            }

            return Task.FromResult<CrisisIncident?>(resolved);
        }
        return Task.FromResult<CrisisIncident?>(null);
    }

    public Task<List<CrisisIncident>> GetActiveCrisisIncidentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _incidents.Values
            .Where(i => i.TenantId == tenantId && i.ResolvedAtUtc == null)
            .OrderByDescending(i => i.TriggeredAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<List<SentimentTrendPoint>> GetSentimentTrendsAsync(Guid tenantId, int hours = 24, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var start = now.AddHours(-hours);

        var tenantMentions = _mentions.Values
            .Where(m => m.TenantId == tenantId && m.IngestedAtUtc >= start)
            .ToList();

        var trends = new List<SentimentTrendPoint>();

        for (int h = 0; h < hours; h++)
        {
            var hourStart = start.AddHours(h);
            var hourEnd = hourStart.AddHours(1);

            var bucket = tenantMentions.Where(m => m.IngestedAtUtc >= hourStart && m.IngestedAtUtc < hourEnd).ToList();
            int pos = bucket.Count(m => m.Sentiment == MentionSentiment.Positive);
            int neu = bucket.Count(m => m.Sentiment == MentionSentiment.Neutral);
            int neg = bucket.Count(m => m.Sentiment == MentionSentiment.Negative || m.Sentiment == MentionSentiment.SevereHostile);
            double avg = bucket.Count > 0 ? bucket.Average(m => m.SentimentScore) : 0.0;

            trends.Add(new SentimentTrendPoint(hourStart, pos, neu, neg, Math.Round(avg, 2)));
        }

        return Task.FromResult(trends);
    }

    public Task<SocialListeningSummary> GetSocialListeningSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var recent = _mentions.Values
            .Where(m => m.TenantId == tenantId && m.IngestedAtUtc >= oneDayAgo)
            .ToList();

        int total = recent.Count;
        int negativeCount = recent.Count(m => m.Sentiment == MentionSentiment.Negative || m.Sentiment == MentionSentiment.SevereHostile);
        double negRatio = total > 0 ? Math.Round((double)negativeCount / total * 100.0, 1) : 0.0;

        _broadcastHaltedState.TryGetValue(tenantId, out bool isHalted);

        var activeIncidents = _incidents.Values.Where(i => i.TenantId == tenantId && i.ResolvedAtUtc == null).ToList();

        var highestThreat = activeIncidents.Count > 0
            ? activeIncidents.Max(i => i.ThreatLevel)
            : CrisisThreatLevel.NormalGreen;

        var recentSpikes = recent
            .Where(m => m.ThreatLevel >= CrisisThreatLevel.HighAlertOrange)
            .OrderByDescending(m => m.IngestedAtUtc)
            .Take(5)
            .ToList();

        return Task.FromResult(new SocialListeningSummary(
            tenantId,
            highestThreat,
            total,
            negRatio,
            isHalted,
            activeIncidents.Count,
            recentSpikes
        ));
    }
}
