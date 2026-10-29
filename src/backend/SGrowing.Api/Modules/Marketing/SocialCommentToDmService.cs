using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public enum SocialRuleMatchType
{
    Exact = 1,
    Contains = 2,
    Regex = 3,
    IntentSentiment = 4
}

public enum CommentProcessingStatus
{
    NoMatch = 0,
    RepliedPublicly = 1,
    DmDelivered = 2,
    DuplicateSkipped = 3,
    RateLimitThrottled = 4,
    Failed = 5
}

public class SocialCommentRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SocialPlatform? Platform { get; set; } // null = applies to all platforms
    public SocialRuleMatchType MatchType { get; set; } = SocialRuleMatchType.Contains;
    public List<string> Keywords { get; set; } = new();
    public string? RegexPattern { get; set; }
    public List<string> PublicReplyTemplates { get; set; } = new();
    public string PrivateDmTemplate { get; set; } = string.Empty;
    public string DiscountPrefix { get; set; } = "VIP";
    public int DiscountPercent { get; set; } = 20;
    public string BaseCheckoutUrl { get; set; } = "https://sgrowing.com/checkout";
    public string UtmSource { get; set; } = "social";
    public string UtmMedium { get; set; } = "comment_to_dm";
    public string UtmCampaign { get; set; } = "inbound_lead_h14";
    public int MinJitterSeconds { get; set; } = 5;
    public int MaxJitterSeconds { get; set; } = 30;
    public int MaxDmsPerHour { get; set; } = 60;
    public int MaxDmsPerMinute { get; set; } = 10;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record IncomingSocialCommentDto(
    Guid TenantId,
    string Platform,
    string PostId,
    string CommentId,
    string CommenterId,
    string CommenterUsername,
    string CommentText,
    string? PostTitle = null,
    string? PostUrl = null,
    DateTime? TimestampUtc = null
);

public record NormalizedSocialComment(
    Guid TenantId,
    SocialPlatform Platform,
    string PostId,
    string CommentId,
    string CommenterId,
    string CommenterUsername,
    string CleanText,
    string OriginalText,
    string? PostTitle,
    DateTime TimestampUtc
);

public record TwoPhaseReactionResult(
    Guid LogId,
    Guid TenantId,
    SocialPlatform Platform,
    string PostId,
    string CommentId,
    string CommenterUsername,
    bool Matched,
    Guid? MatchedRuleId,
    string? MatchedRuleName,
    string? MatchedKeyword,
    CommentProcessingStatus Status,
    string? PublicReply,
    string? PrivateDm,
    string? CouponCode,
    string? CheckoutUrl,
    int JitterDelaySeconds,
    DateTime ProcessedAtUtc,
    string? Message = null
);

public class CommentProcessingLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public SocialPlatform Platform { get; set; }
    public string PostId { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
    public string CommenterId { get; set; } = string.Empty;
    public string CommenterUsername { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public Guid? MatchedRuleId { get; set; }
    public string? MatchedRuleName { get; set; }
    public string? MatchedKeyword { get; set; }
    public CommentProcessingStatus Status { get; set; }
    public string? PublicReply { get; set; }
    public string? PrivateDm { get; set; }
    public string? CouponCode { get; set; }
    public string? CheckoutUrl { get; set; }
    public int JitterSeconds { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public bool HasClicked { get; set; }
    public bool HasConverted { get; set; }
    public decimal? OrderValue { get; set; }
}

public record PlatformFunnelBreakdown(
    string Platform,
    int Comments,
    int Matched,
    int DmsSent,
    int Clicks,
    int Conversions,
    decimal Revenue
);

public record SocialCommentFunnelMetrics(
    int TotalComments,
    int MatchedIntent,
    int PublicRepliesSent,
    int DMsDelivered,
    int DuplicateSkipped,
    int RateLimited,
    int LinkClicks,
    int Conversions,
    decimal TotalRevenue,
    double MatchRatePercentage,
    double ClickThroughRatePercentage,
    double ConversionRatePercentage,
    Dictionary<string, PlatformFunnelBreakdown> ByPlatform,
    List<CommentProcessingLogEntry> RecentLogs
);

public record CreateOrUpdateRuleDto(
    Guid? Id,
    Guid TenantId,
    string Name,
    string? Platform,
    SocialRuleMatchType MatchType,
    List<string> Keywords,
    string? RegexPattern,
    List<string> PublicReplyTemplates,
    string PrivateDmTemplate,
    string? DiscountPrefix = "VIP",
    int? DiscountPercent = 20,
    string? BaseCheckoutUrl = "https://sgrowing.com/checkout",
    string? UtmSource = "social",
    string? UtmMedium = "comment_to_dm",
    string? UtmCampaign = "inbound_lead_h14",
    int? MinJitterSeconds = 5,
    int? MaxJitterSeconds = 30,
    int? MaxDmsPerHour = 60,
    int? MaxDmsPerMinute = 10,
    bool? IsEnabled = true,
    int? Priority = 1
);

public interface ISocialCommentToDmService
{
    Task<SocialCommentRule> CreateOrUpdateRuleAsync(SocialCommentRule rule);
    Task<List<SocialCommentRule>> GetRulesAsync(Guid tenantId);
    Task<SocialCommentRule?> GetRuleByIdAsync(Guid tenantId, Guid ruleId);
    Task<bool> DeleteRuleAsync(Guid tenantId, Guid ruleId);
    NormalizedSocialComment NormalizeComment(IncomingSocialCommentDto incoming);
    Task<TwoPhaseReactionResult> ProcessCommentAsync(IncomingSocialCommentDto incoming, CancellationToken cancellationToken = default);
    Task<List<CommentProcessingLogEntry>> GetLogsAsync(Guid tenantId, int limit = 50);
    Task<SocialCommentFunnelMetrics> GetFunnelMetricsAsync(Guid tenantId);
    Task<bool> RecordClickAsync(Guid tenantId, string couponCode);
    Task<bool> RecordConversionAsync(Guid tenantId, string couponCode, decimal orderValue);
}

public class SocialCommentToDmService : ISocialCommentToDmService
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, SocialCommentRule>> RulesByTenant = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentBag<CommentProcessingLogEntry>> LogsByTenant = new();
    private static readonly ConcurrentDictionary<string, DateTime> DedupDispatches = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> RateLimitWindows = new();

    private static readonly string[] NegativeSentimentKeywords =
    {
        "scam", "fake", "bad", "terrible", "awful", "trash", "hate", "fraud",
        "stolen", "ripoff", "rip off", "waste of money", "don't buy", "dont buy",
        "do not buy", "refund", "broken", "worst", "unhappy"
    };

    private static readonly string[] CommercialIntentKeywords =
    {
        "price", "pricing", "how much", "cost", "buy", "purchase", "order",
        "discount", "coupon", "code", "voucher", "deal", "link", "info",
        "interested", "available", "where to buy", "get this", "need this",
        "want this", "checkout", "shipping"
    };

    public Task<List<SocialCommentRule>> GetRulesAsync(Guid tenantId)
    {
        EnsureDefaultRulesSeeded(tenantId);
        var tenantRules = RulesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, SocialCommentRule>());
        var list = tenantRules.Values.OrderBy(r => r.Priority).ThenByDescending(r => r.CreatedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<SocialCommentRule?> GetRuleByIdAsync(Guid tenantId, Guid ruleId)
    {
        EnsureDefaultRulesSeeded(tenantId);
        if (RulesByTenant.TryGetValue(tenantId, out var dict) && dict.TryGetValue(ruleId, out var rule))
        {
            return Task.FromResult<SocialCommentRule?>(rule);
        }
        return Task.FromResult<SocialCommentRule?>(null);
    }

    public Task<SocialCommentRule> CreateOrUpdateRuleAsync(SocialCommentRule rule)
    {
        if (rule.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(rule));
        if (rule.Id == Guid.Empty) rule.Id = Guid.NewGuid();

        if (rule.PublicReplyTemplates == null || rule.PublicReplyTemplates.Count == 0)
        {
            rule.PublicReplyTemplates = GetDefaultPublicReplies();
        }

        if (string.IsNullOrWhiteSpace(rule.PrivateDmTemplate))
        {
            rule.PrivateDmTemplate = "Hey @{{username}}! 👋 Thanks for reaching out! Here is your exclusive {{discount_percent}}% discount code: {{discount_code}}\n\nDirect checkout: {{checkout_url}} 🚀";
        }

        rule.UpdatedAtUtc = DateTime.UtcNow;
        var tenantRules = RulesByTenant.GetOrAdd(rule.TenantId, _ => new ConcurrentDictionary<Guid, SocialCommentRule>());
        tenantRules[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<bool> DeleteRuleAsync(Guid tenantId, Guid ruleId)
    {
        if (RulesByTenant.TryGetValue(tenantId, out var dict))
        {
            return Task.FromResult(dict.TryRemove(ruleId, out _));
        }
        return Task.FromResult(false);
    }

    public NormalizedSocialComment NormalizeComment(IncomingSocialCommentDto incoming)
    {
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));

        var platform = ResolvePlatform(incoming.Platform);
        var cleanText = CleanCommentText(incoming.CommentText);

        return new NormalizedSocialComment(
            TenantId: incoming.TenantId,
            Platform: platform,
            PostId: incoming.PostId?.Trim() ?? string.Empty,
            CommentId: string.IsNullOrWhiteSpace(incoming.CommentId) ? Guid.NewGuid().ToString() : incoming.CommentId.Trim(),
            CommenterId: string.IsNullOrWhiteSpace(incoming.CommenterId) ? incoming.CommenterUsername?.Trim() ?? "anonymous" : incoming.CommenterId.Trim(),
            CommenterUsername: (incoming.CommenterUsername ?? "user").Trim().TrimStart('@'),
            CleanText: cleanText,
            OriginalText: incoming.CommentText ?? string.Empty,
            PostTitle: incoming.PostTitle,
            TimestampUtc: incoming.TimestampUtc ?? DateTime.UtcNow
        );
    }

    public async Task<TwoPhaseReactionResult> ProcessCommentAsync(IncomingSocialCommentDto incoming, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeComment(incoming);
        var tenantId = normalized.TenantId;

        EnsureDefaultRulesSeeded(tenantId);
        var rules = await GetRulesAsync(tenantId);
        var activeRules = rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority).ToList();

        SocialCommentRule? matchedRule = null;
        string? matchedKeyword = null;

        foreach (var rule in activeRules)
        {
            if (rule.Platform.HasValue && rule.Platform.Value != normalized.Platform)
            {
                continue;
            }

            var (isMatch, keyword) = EvaluateRuleMatch(rule, normalized.CleanText);
            if (isMatch)
            {
                matchedRule = rule;
                matchedKeyword = keyword;
                break;
            }
        }

        var logEntry = new CommentProcessingLogEntry
        {
            TenantId = tenantId,
            Platform = normalized.Platform,
            PostId = normalized.PostId,
            CommentId = normalized.CommentId,
            CommenterId = normalized.CommenterId,
            CommenterUsername = normalized.CommenterUsername,
            CommentText = normalized.OriginalText,
            ProcessedAtUtc = DateTime.UtcNow
        };

        if (matchedRule == null)
        {
            logEntry.Status = CommentProcessingStatus.NoMatch;
            AddLog(tenantId, logEntry);

            return new TwoPhaseReactionResult(
                LogId: logEntry.Id,
                TenantId: tenantId,
                Platform: normalized.Platform,
                PostId: normalized.PostId,
                CommentId: normalized.CommentId,
                CommenterUsername: normalized.CommenterUsername,
                Matched: false,
                MatchedRuleId: null,
                MatchedRuleName: null,
                MatchedKeyword: null,
                Status: CommentProcessingStatus.NoMatch,
                PublicReply: null,
                PrivateDm: null,
                CouponCode: null,
                CheckoutUrl: null,
                JitterDelaySeconds: 0,
                ProcessedAtUtc: logEntry.ProcessedAtUtc,
                Message: "Comment did not match any active inbound keyword or intent rules."
            );
        }

        logEntry.MatchedRuleId = matchedRule.Id;
        logEntry.MatchedRuleName = matchedRule.Name;
        logEntry.MatchedKeyword = matchedKeyword;

        // Guardrail 1: Jitter Delay Calculation (simulated between min & max)
        var minJitter = Math.Max(1, matchedRule.MinJitterSeconds);
        var maxJitter = Math.Max(minJitter, matchedRule.MaxJitterSeconds);
        var jitterSeconds = Random.Shared.Next(minJitter, maxJitter + 1);
        logEntry.JitterSeconds = jitterSeconds;

        // Phase 1: Randomized Public Comment Reply from Template Pool (5+ rotating templates)
        var publicReplyPool = matchedRule.PublicReplyTemplates != null && matchedRule.PublicReplyTemplates.Count > 0
            ? matchedRule.PublicReplyTemplates
            : GetDefaultPublicReplies();
        var selectedPublicTemplate = publicReplyPool[Random.Shared.Next(publicReplyPool.Count)];
        var publicReply = selectedPublicTemplate.Replace("{{username}}", normalized.CommenterUsername);
        logEntry.PublicReply = publicReply;

        // Guardrail 2: De-duplication check: ensures a user receives at most 1 DM per post
        // Idempotency Key: TenantId:PostId:CommenterId
        var dedupKey = $"{tenantId}:{normalized.PostId}:{normalized.CommenterId}";
        if (DedupDispatches.TryGetValue(dedupKey, out _))
        {
            logEntry.Status = CommentProcessingStatus.DuplicateSkipped;
            AddLog(tenantId, logEntry);

            return new TwoPhaseReactionResult(
                LogId: logEntry.Id,
                TenantId: tenantId,
                Platform: normalized.Platform,
                PostId: normalized.PostId,
                CommentId: normalized.CommentId,
                CommenterUsername: normalized.CommenterUsername,
                Matched: true,
                MatchedRuleId: matchedRule.Id,
                MatchedRuleName: matchedRule.Name,
                MatchedKeyword: matchedKeyword,
                Status: CommentProcessingStatus.DuplicateSkipped,
                PublicReply: publicReply,
                PrivateDm: null,
                CouponCode: null,
                CheckoutUrl: null,
                JitterDelaySeconds: jitterSeconds,
                ProcessedAtUtc: logEntry.ProcessedAtUtc,
                Message: "Duplicate comment detected for this user on this post. Public reply sent, but private DM skipped to avoid spam."
            );
        }

        // Guardrail 3: Anti-Shadowban Rate Limiter (tracks dispatches per minute/hour per account)
        var rateLimitKey = $"{tenantId}:{normalized.Platform}";
        if (IsRateLimited(rateLimitKey, matchedRule.MaxDmsPerMinute, matchedRule.MaxDmsPerHour))
        {
            logEntry.Status = CommentProcessingStatus.RateLimitThrottled;
            AddLog(tenantId, logEntry);

            return new TwoPhaseReactionResult(
                LogId: logEntry.Id,
                TenantId: tenantId,
                Platform: normalized.Platform,
                PostId: normalized.PostId,
                CommentId: normalized.CommentId,
                CommenterUsername: normalized.CommenterUsername,
                Matched: true,
                MatchedRuleId: matchedRule.Id,
                MatchedRuleName: matchedRule.Name,
                MatchedKeyword: matchedKeyword,
                Status: CommentProcessingStatus.RateLimitThrottled,
                PublicReply: publicReply,
                PrivateDm: null,
                CouponCode: null,
                CheckoutUrl: null,
                JitterDelaySeconds: jitterSeconds,
                ProcessedAtUtc: logEntry.ProcessedAtUtc,
                Message: "Anti-shadowban rate limit exceeded for this platform account. DM throttled."
            );
        }

        // Phase 2: Dispatch 1-on-1 Direct Message with personalized coupon code & tracked checkout link
        var prefix = string.IsNullOrWhiteSpace(matchedRule.DiscountPrefix) ? "VIP" : matchedRule.DiscountPrefix.Trim().ToUpperInvariant();
        var couponCode = $"{prefix}-{GenerateRandomAlphaNumeric(4)}";
        var checkoutUrl = BuildTrackedCheckoutUrl(matchedRule, normalized.Platform, normalized.PostId, couponCode);

        var privateDm = matchedRule.PrivateDmTemplate
            .Replace("{{discount_code}}", couponCode)
            .Replace("{{checkout_url}}", checkoutUrl)
            .Replace("{{username}}", normalized.CommenterUsername)
            .Replace("{{platform}}", normalized.Platform.ToString())
            .Replace("{{discount_percent}}", matchedRule.DiscountPercent.ToString())
            .Replace("{{post_id}}", normalized.PostId);

        // Record successful DM dispatch in dedup and rate limiter
        DedupDispatches[dedupKey] = DateTime.UtcNow;
        RecordRateLimitDispatch(rateLimitKey);

        logEntry.Status = CommentProcessingStatus.DmDelivered;
        logEntry.PrivateDm = privateDm;
        logEntry.CouponCode = couponCode;
        logEntry.CheckoutUrl = checkoutUrl;

        AddLog(tenantId, logEntry);

        return new TwoPhaseReactionResult(
            LogId: logEntry.Id,
            TenantId: tenantId,
            Platform: normalized.Platform,
            PostId: normalized.PostId,
            CommentId: normalized.CommentId,
            CommenterUsername: normalized.CommenterUsername,
            Matched: true,
            MatchedRuleId: matchedRule.Id,
            MatchedRuleName: matchedRule.Name,
            MatchedKeyword: matchedKeyword,
            Status: CommentProcessingStatus.DmDelivered,
            PublicReply: publicReply,
            PrivateDm: privateDm,
            CouponCode: couponCode,
            CheckoutUrl: checkoutUrl,
            JitterDelaySeconds: jitterSeconds,
            ProcessedAtUtc: logEntry.ProcessedAtUtc,
            Message: "Two-Phase Reaction dispatched: Public comment reply posted and private DM delivered."
        );
    }

    public Task<List<CommentProcessingLogEntry>> GetLogsAsync(Guid tenantId, int limit = 50)
    {
        if (LogsByTenant.TryGetValue(tenantId, out var bag))
        {
            var logs = bag.OrderByDescending(l => l.ProcessedAtUtc).Take(limit).ToList();
            return Task.FromResult(logs);
        }
        return Task.FromResult(new List<CommentProcessingLogEntry>());
    }

    public Task<SocialCommentFunnelMetrics> GetFunnelMetricsAsync(Guid tenantId)
    {
        EnsureDefaultRulesSeeded(tenantId);
        var logs = LogsByTenant.TryGetValue(tenantId, out var bag) ? bag.ToList() : new List<CommentProcessingLogEntry>();

        var totalComments = logs.Count;
        var matchedIntent = logs.Count(l => l.Status != CommentProcessingStatus.NoMatch);
        var publicRepliesSent = logs.Count(l => l.Status == CommentProcessingStatus.DmDelivered || l.Status == CommentProcessingStatus.RepliedPublicly || l.Status == CommentProcessingStatus.DuplicateSkipped);
        var dmsDelivered = logs.Count(l => l.Status == CommentProcessingStatus.DmDelivered);
        var duplicateSkipped = logs.Count(l => l.Status == CommentProcessingStatus.DuplicateSkipped);
        var rateLimited = logs.Count(l => l.Status == CommentProcessingStatus.RateLimitThrottled);
        var linkClicks = logs.Count(l => l.HasClicked);
        var conversions = logs.Count(l => l.HasConverted);
        var totalRevenue = logs.Where(l => l.HasConverted && l.OrderValue.HasValue).Sum(l => l.OrderValue!.Value);

        var matchRate = totalComments > 0 ? Math.Round(((double)matchedIntent / totalComments) * 100, 1) : 0;
        var ctr = dmsDelivered > 0 ? Math.Round(((double)linkClicks / dmsDelivered) * 100, 1) : 0;
        var convRate = dmsDelivered > 0 ? Math.Round(((double)conversions / dmsDelivered) * 100, 1) : 0;

        var platformNames = new[] { "Instagram", "Facebook", "TikTok", "YouTube" };
        var byPlatform = new Dictionary<string, PlatformFunnelBreakdown>();

        foreach (var pName in platformNames)
        {
            var pEnum = ResolvePlatform(pName);
            var pLogs = logs.Where(l => l.Platform == pEnum).ToList();
            var pComments = pLogs.Count;
            var pMatched = pLogs.Count(l => l.Status != CommentProcessingStatus.NoMatch);
            var pDms = pLogs.Count(l => l.Status == CommentProcessingStatus.DmDelivered);
            var pClicks = pLogs.Count(l => l.HasClicked);
            var pConv = pLogs.Count(l => l.HasConverted);
            var pRev = pLogs.Where(l => l.HasConverted && l.OrderValue.HasValue).Sum(l => l.OrderValue!.Value);

            byPlatform[pName] = new PlatformFunnelBreakdown(
                Platform: pName,
                Comments: pComments,
                Matched: pMatched,
                DmsSent: pDms,
                Clicks: pClicks,
                Conversions: pConv,
                Revenue: pRev
            );
        }

        var recentLogs = logs.OrderByDescending(l => l.ProcessedAtUtc).Take(20).ToList();

        var metrics = new SocialCommentFunnelMetrics(
            TotalComments: totalComments,
            MatchedIntent: matchedIntent,
            PublicRepliesSent: publicRepliesSent,
            DMsDelivered: dmsDelivered,
            DuplicateSkipped: duplicateSkipped,
            RateLimited: rateLimited,
            LinkClicks: linkClicks,
            Conversions: conversions,
            TotalRevenue: totalRevenue,
            MatchRatePercentage: matchRate,
            ClickThroughRatePercentage: ctr,
            ConversionRatePercentage: convRate,
            ByPlatform: byPlatform,
            RecentLogs: recentLogs
        );

        return Task.FromResult(metrics);
    }

    public Task<bool> RecordClickAsync(Guid tenantId, string couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode)) return Task.FromResult(false);
        if (LogsByTenant.TryGetValue(tenantId, out var bag))
        {
            var match = bag.FirstOrDefault(l => string.Equals(l.CouponCode, couponCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.HasClicked = true;
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task<bool> RecordConversionAsync(Guid tenantId, string couponCode, decimal orderValue)
    {
        if (string.IsNullOrWhiteSpace(couponCode)) return Task.FromResult(false);
        if (LogsByTenant.TryGetValue(tenantId, out var bag))
        {
            var match = bag.FirstOrDefault(l => string.Equals(l.CouponCode, couponCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.HasClicked = true;
                match.HasConverted = true;
                match.OrderValue = orderValue;
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public static (bool isMatch, string? keyword) EvaluateRuleMatch(SocialCommentRule rule, string cleanCommentText)
    {
        if (string.IsNullOrWhiteSpace(cleanCommentText)) return (false, null);

        switch (rule.MatchType)
        {
            case SocialRuleMatchType.Exact:
                var cleanTokens = cleanCommentText.Split(new[] { ' ', ',', '!', '?', '.', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var kw in rule.Keywords)
                {
                    var trimmedKw = kw.Trim();
                    if (string.Equals(cleanCommentText, trimmedKw, StringComparison.OrdinalIgnoreCase) ||
                        cleanTokens.Any(t => string.Equals(t, trimmedKw, StringComparison.OrdinalIgnoreCase)))
                    {
                        return (true, kw);
                    }
                }
                return (false, null);

            case SocialRuleMatchType.Contains:
                foreach (var kw in rule.Keywords)
                {
                    var trimmedKw = kw.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedKw) &&
                        cleanCommentText.Contains(trimmedKw, StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, kw);
                    }
                }
                return (false, null);

            case SocialRuleMatchType.Regex:
                if (!string.IsNullOrWhiteSpace(rule.RegexPattern))
                {
                    try
                    {
                        var regex = new Regex(rule.RegexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
                        var match = regex.Match(cleanCommentText);
                        if (match.Success)
                        {
                            return (true, match.Value);
                        }
                    }
                    catch
                    {
                        // Fallback
                    }
                }
                // Also check if any keyword matches as a word boundary regex
                foreach (var kw in rule.Keywords)
                {
                    var pattern = $@"\b{Regex.Escape(kw.Trim())}\b";
                    if (Regex.IsMatch(cleanCommentText, pattern, RegexOptions.IgnoreCase))
                    {
                        return (true, kw);
                    }
                }
                return (false, null);

            case SocialRuleMatchType.IntentSentiment:
                // First check negative sentiment filter
                var lower = cleanCommentText.ToLowerInvariant();
                foreach (var neg in NegativeSentimentKeywords)
                {
                    if (lower.Contains(neg))
                    {
                        return (false, null); // Reject negative sentiment
                    }
                }

                // Check rule keywords first
                foreach (var kw in rule.Keywords)
                {
                    if (lower.Contains(kw.Trim().ToLowerInvariant()))
                    {
                        return (true, kw);
                    }
                }

                // Check global commercial intent keywords
                foreach (var intent in CommercialIntentKeywords)
                {
                    if (lower.Contains(intent))
                    {
                        return (true, intent);
                    }
                }
                return (false, null);

            default:
                return (false, null);
        }
    }

    public static SocialPlatform ResolvePlatform(string? platformName)
    {
        if (string.IsNullOrWhiteSpace(platformName)) return SocialPlatform.Instagram;

        var clean = platformName.Trim().ToLowerInvariant();
        return clean switch
        {
            "instagram" or "ig" or "insta" => SocialPlatform.Instagram,
            "facebook" or "fb" or "meta" => SocialPlatform.Facebook,
            "tiktok" or "tt" => SocialPlatform.TikTok,
            "youtube" or "yt" or "shorts" => SocialPlatform.YouTube,
            "x" or "twitter" => SocialPlatform.X,
            "threads" => SocialPlatform.Threads,
            "linkedin" => SocialPlatform.LinkedIn,
            "telegram" => SocialPlatform.Telegram,
            _ => Enum.TryParse<SocialPlatform>(platformName, true, out var parsed) ? parsed : SocialPlatform.Instagram
        };
    }

    public static string CleanCommentText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        // Normalize multiple spaces into single space
        return Regex.Replace(trimmed, @"\s+", " ");
    }

    public static List<string> GetDefaultPublicReplies()
    {
        return new List<string>
        {
            "Check your DMs! Just sent you all the details ✨📩",
            "Sent you a private message with your exclusive access! 🚀🎁",
            "DM delivered! Check your inbox for the link & VIP code 🔥📬",
            "We just slid into your DMs with something special! 💫🛒",
            "All set! Check your DMs for the full info and promo code 💬🎉",
            "Sent you a direct message! Let us know if you have any questions 💌✨"
        };
    }

    private static string BuildTrackedCheckoutUrl(SocialCommentRule rule, SocialPlatform platform, string postId, string couponCode)
    {
        var baseUrl = string.IsNullOrWhiteSpace(rule.BaseCheckoutUrl) ? "https://sgrowing.com/checkout" : rule.BaseCheckoutUrl.TrimEnd('/');
        var utmSource = (string.IsNullOrWhiteSpace(rule.UtmSource) || rule.UtmSource.Equals("social", StringComparison.OrdinalIgnoreCase))
            ? platform.ToString().ToLowerInvariant()
            : rule.UtmSource;
        var utmMedium = string.IsNullOrWhiteSpace(rule.UtmMedium) ? "comment_to_dm" : rule.UtmMedium;
        var utmCampaign = string.IsNullOrWhiteSpace(rule.UtmCampaign) ? "inbound_lead_h14" : rule.UtmCampaign;

        return $"{baseUrl}?code={Uri.EscapeDataString(couponCode)}&utm_source={Uri.EscapeDataString(utmSource)}&utm_medium={Uri.EscapeDataString(utmMedium)}&utm_campaign={Uri.EscapeDataString(utmCampaign)}&utm_content={Uri.EscapeDataString(postId)}";
    }

    private static string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[Random.Shared.Next(chars.Length)];
        }
        return new string(result);
    }

    private static bool IsRateLimited(string accountKey, int maxPerMinute, int maxPerHour)
    {
        var queue = RateLimitWindows.GetOrAdd(accountKey, _ => new ConcurrentQueue<DateTime>());
        var now = DateTime.UtcNow;

        // Clean out timestamps older than 1 hour
        while (queue.TryPeek(out var oldest) && (now - oldest).TotalHours > 1)
        {
            queue.TryDequeue(out _);
        }

        var list = queue.ToArray();
        var inLastMinute = list.Count(t => (now - t).TotalMinutes <= 1);
        var inLastHour = list.Length;

        return inLastMinute >= maxPerMinute || inLastHour >= maxPerHour;
    }

    private static void RecordRateLimitDispatch(string accountKey)
    {
        var queue = RateLimitWindows.GetOrAdd(accountKey, _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(DateTime.UtcNow);
    }

    private static void AddLog(Guid tenantId, CommentProcessingLogEntry log)
    {
        var bag = LogsByTenant.GetOrAdd(tenantId, _ => new ConcurrentBag<CommentProcessingLogEntry>());
        bag.Add(log);
    }

    private static void EnsureDefaultRulesSeeded(Guid tenantId)
    {
        if (tenantId == Guid.Empty) return;
        var tenantRules = RulesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, SocialCommentRule>());
        if (!tenantRules.IsEmpty) return;

        // Seed default high-converting rules
        var rule1 = new SocialCommentRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "VIP Discount & Inbound Checkout",
            Platform = null, // Applies to all platforms
            MatchType = SocialRuleMatchType.Contains,
            Keywords = new List<string> { "PRICE", "BUY", "INFO", "COUPON", "LINK", "HOW MUCH", "DISCOUNT", "COST" },
            PublicReplyTemplates = GetDefaultPublicReplies(),
            PrivateDmTemplate = "Hey @{{username}}! 👋 Thanks for reaching out! Here is your exclusive {{discount_percent}}% discount code: {{discount_code}}\n\nTap here for direct VIP checkout: {{checkout_url}}\n\nHave questions? We're here to help!",
            DiscountPrefix = "VIP",
            DiscountPercent = 20,
            BaseCheckoutUrl = "https://sgrowing.com/checkout",
            UtmSource = "social",
            UtmMedium = "comment_to_dm",
            UtmCampaign = "inbound_lead_h14",
            MinJitterSeconds = 5,
            MaxJitterSeconds = 30,
            IsEnabled = true,
            Priority = 1
        };

        var rule2 = new SocialCommentRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Instant Promo Code Exact Match",
            Platform = null,
            MatchType = SocialRuleMatchType.Exact,
            Keywords = new List<string> { "CODE", "PROMO", "COUPON", "CLAIM", "VOUCHER", "DISCOUNT" },
            PublicReplyTemplates = GetDefaultPublicReplies(),
            PrivateDmTemplate = "Boom @{{username}}! 💥 You got it! Here is your exclusive {{discount_percent}}% code: {{discount_code}}\n\nInstant checkout link: {{checkout_url}}\n\nEnjoy shopping! 🛍️",
            DiscountPrefix = "SAVE",
            DiscountPercent = 25,
            BaseCheckoutUrl = "https://sgrowing.com/checkout",
            UtmSource = "social",
            UtmMedium = "comment_to_dm",
            UtmCampaign = "promo_claim_h14",
            MinJitterSeconds = 5,
            MaxJitterSeconds = 25,
            IsEnabled = true,
            Priority = 2
        };

        var rule3 = new SocialCommentRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "AI Intent & Buying Sentiment Deflector",
            Platform = null,
            MatchType = SocialRuleMatchType.IntentSentiment,
            Keywords = new List<string> { "interested", "how to buy", "where to get", "order now", "available" },
            PublicReplyTemplates = GetDefaultPublicReplies(),
            PrivateDmTemplate = "Hi @{{username}}! We love your interest! Here is a direct link with {{discount_percent}}% off: {{checkout_url}} (use code {{discount_code}} at checkout). Let us know if you need anything!",
            DiscountPrefix = "VIP",
            DiscountPercent = 15,
            BaseCheckoutUrl = "https://sgrowing.com/checkout",
            UtmSource = "social",
            UtmMedium = "comment_to_dm",
            UtmCampaign = "ai_intent_h14",
            MinJitterSeconds = 5,
            MaxJitterSeconds = 30,
            IsEnabled = true,
            Priority = 3
        };

        tenantRules[rule1.Id] = rule1;
        tenantRules[rule2.Id] = rule2;
        tenantRules[rule3.Id] = rule3;
    }
}
