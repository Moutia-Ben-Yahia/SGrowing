using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Commerce.LiveStreaming;

/// <summary>
/// Real-time chat moderation, sentiment analysis, viewer intent classification,
/// auto-reply generation, and host earpiece teleprompter cue synthesis.
/// </summary>
public interface ILiveVideoAiCoHostService
{
    Task<AiCoHostProcessingResult> ProcessIncomingMessageAsync(
        string sessionId,
        string userId,
        string username,
        string text,
        CancellationToken ct = default);

    Task<IReadOnlyList<AiCoHostCue>> GetPendingHostCuesAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<bool> AcknowledgeCueAsync(
        string sessionId,
        string cueId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessageRecord>> GetChatHistoryAsync(
        string sessionId,
        int limit = 50,
        CancellationToken ct = default);
}

/// <summary>
/// Thread-safe in-memory AI Co-Host engine with heuristic NLP classifiers.
/// </summary>
public partial class LiveVideoAiCoHostService : ILiveVideoAiCoHostService
{
    private readonly ConcurrentDictionary<string, List<ChatMessageRecord>> _chatHistoryBySession = new();
    private readonly ConcurrentDictionary<string, List<AiCoHostCue>> _cuesBySession = new();
    private readonly ConcurrentDictionary<string, (string LastText, DateTime Timestamp)> _recentUserMessages = new();
    private readonly Lock _dataLock = new();

    #region Moderation Wordlists & Regex

    private static readonly HashSet<string> ToxicWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "scam", "cheat", "cheater", "ugly", "fake", "idiot", "hate", "trash", "fraud",
        "stolen", "bastard", "stupid", "garbage", "liar", "thief", "harass", "die", "moron"
    };

    private static readonly HashSet<string> PositiveSentimentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "love", "great", "awesome", "beautiful", "perfect", "best", "nice", "excited",
        "amazing", "recommend", "buy", "bought", "good", "super", "like", "excellent",
        "gorgeous", "fire", "cozy", "happy", "stellar", "quality"
    };

    private static readonly HashSet<string> NegativeSentimentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bad", "slow", "terrible", "expensive", "broke", "broken", "hate", "worst",
        "poor", "return", "delay", "refund", "damaged", "defect", "awful", "horrible",
        "fail", "disappointed", "overpriced"
    };

    private static readonly string[] ProductInquiryKeywords =
    [
        "size", "sizing", "fit", "material", "leather", "cotton", "wool", "dimensions",
        "fabric", "color", "specs", "warranty", "genuine", "height", "weight", "model",
        "quality", "measure", "ingredients", "origin", "compatible", "small", "medium", "large", "xl"
    ];

    private static readonly string[] PriceInquiryKeywords =
    [
        "price", "how much", "cost", "discount", "flash", "sale", "coupon", "voucher",
        "promo", "shipping", "delivery", "expensive", "cheap", "dzd", "da", "fee", "offer"
    ];

    private static readonly string[] ComplimentKeywords =
    [
        "love this", "looks great", "awesome host", "beautiful", "gorgeous", "congrats",
        "you look great", "great stream", "best product", "favorite", "amazing host",
        "love it", "well done", "bravo", "fantastic"
    ];

    [GeneratedRegex(@"(.)\1{5,}", RegexOptions.IgnoreCase)]
    private static partial Regex CharacterRepetitionRegex();

    [GeneratedRegex(@"https?://|www\.", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPatternRegex();

    #endregion

    public Task<AiCoHostProcessingResult> ProcessIncomingMessageAsync(
        string sessionId,
        string userId,
        string username,
        string text,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var trimmedText = text?.Trim() ?? string.Empty;
        var now = DateTime.UtcNow;

        // 1. Moderation Evaluation
        var (moderationStatus, moderationReason) = EvaluateModeration(userId, trimmedText, now);

        // 2. Sentiment Analysis
        var sentimentScore = CalculateSentimentScore(trimmedText, moderationStatus);

        // 3. Intent Classification
        var detectedIntent = ClassifyIntent(trimmedText, moderationStatus);

        // 4. Create Chat Message Record
        var messageRecord = new ChatMessageRecord(
            MessageId: $"msg_{Guid.NewGuid():N}",
            SessionId: sessionId,
            UserId: userId,
            Username: username,
            Text: trimmedText,
            Timestamp: now,
            ModerationStatus: moderationStatus,
            SentimentScore: sentimentScore,
            DetectedIntent: detectedIntent,
            ModerationReason: moderationReason);

        // Store chat in session history
        lock (_dataLock)
        {
            var history = _chatHistoryBySession.GetOrAdd(sessionId, _ => []);
            history.Add(messageRecord);
        }

        // 5. Auto-Reply Generation
        var autoReply = GenerateAutoReply(username, trimmedText, detectedIntent, moderationStatus);

        // 6. Host Earpiece Cue Generation
        var hostCue = GenerateHostEarpieceCue(sessionId, username, trimmedText, detectedIntent, moderationStatus, sentimentScore, now);

        if (hostCue is not null)
        {
            lock (_dataLock)
            {
                var cues = _cuesBySession.GetOrAdd(sessionId, _ => []);
                cues.Add(hostCue);
            }
        }

        return Task.FromResult(new AiCoHostProcessingResult(
            Message: messageRecord,
            AutoReplyText: autoReply,
            GeneratedCue: hostCue));
    }

    public Task<IReadOnlyList<AiCoHostCue>> GetPendingHostCuesAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        lock (_dataLock)
        {
            if (!_cuesBySession.TryGetValue(sessionId, out var cues))
            {
                IReadOnlyList<AiCoHostCue> empty = [];
                return Task.FromResult(empty);
            }

            IReadOnlyList<AiCoHostCue> pending = [.. cues
                .Where(c => !c.IsAcknowledged)
                .OrderByDescending(c => c.Urgency)
                .ThenBy(c => c.GeneratedAt)];

            return Task.FromResult(pending);
        }
    }

    public Task<bool> AcknowledgeCueAsync(
        string sessionId,
        string cueId,
        CancellationToken ct = default)
    {
        lock (_dataLock)
        {
            if (!_cuesBySession.TryGetValue(sessionId, out var cues))
            {
                return Task.FromResult(false);
            }

            var index = cues.FindIndex(c => string.Equals(c.CueId, cueId, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                return Task.FromResult(false);
            }

            cues[index] = cues[index] with { IsAcknowledged = true };
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<ChatMessageRecord>> GetChatHistoryAsync(
        string sessionId,
        int limit = 50,
        CancellationToken ct = default)
    {
        lock (_dataLock)
        {
            if (!_chatHistoryBySession.TryGetValue(sessionId, out var history))
            {
                IReadOnlyList<ChatMessageRecord> empty = [];
                return Task.FromResult(empty);
            }

            IReadOnlyList<ChatMessageRecord> result = [.. history.TakeLast(Math.Max(1, limit))];
            return Task.FromResult(result);
        }
    }

    #region Internal AI Heuristic Methods

    private (ModerationStatus Status, string? Reason) EvaluateModeration(string userId, string text, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (ModerationStatus.Approved, null);
        }

        // Rapid duplicate message flood filter per user (within 5 seconds)
        if (_recentUserMessages.TryGetValue(userId, out var recent))
        {
            if (string.Equals(recent.LastText, text, StringComparison.OrdinalIgnoreCase) &&
                (now - recent.Timestamp).TotalSeconds < 5.0)
            {
                return (ModerationStatus.SpamBlocked, "Duplicate message flood detected.");
            }
        }

        _recentUserMessages[userId] = (text, now);

        // Toxic / Abusive word filter
        var tokens = text.Split([' ', ',', '.', '!', '?', ';', ':', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (ToxicWords.Contains(token))
            {
                return (ModerationStatus.FlaggedToxic, "Abusive or toxic content detected.");
            }
        }

        // URL / External Link Spam
        if (UrlPatternRegex().IsMatch(text))
        {
            return (ModerationStatus.SpamBlocked, "External promotional link spam blocked.");
        }

        // Repetitive Character Spam (e.g. aaaaaa, !!!!!!!)
        if (CharacterRepetitionRegex().IsMatch(text))
        {
            return (ModerationStatus.SpamBlocked, "Repetitive character spam pattern detected.");
        }

        // Excessive Capslock (shouting spam)
        var letterChars = text.Count(char.IsLetter);
        if (letterChars >= 10)
        {
            var upperChars = text.Count(char.IsUpper);
            if ((double)upperChars / letterChars >= 0.85)
            {
                return (ModerationStatus.SpamBlocked, "Excessive uppercase shouting spam detected.");
            }
        }

        return (ModerationStatus.Approved, null);
    }

    private static double CalculateSentimentScore(string text, ModerationStatus status)
    {
        if (status == ModerationStatus.FlaggedToxic)
        {
            return -0.90;
        }

        if (status == ModerationStatus.SpamBlocked)
        {
            return -0.30;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        var tokens = text.Split([' ', ',', '.', '!', '?', ';', ':', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var positiveCount = 0;
        var negativeCount = 0;

        foreach (var token in tokens)
        {
            if (PositiveSentimentWords.Contains(token))
            {
                positiveCount++;
            }
            else if (NegativeSentimentWords.Contains(token))
            {
                negativeCount++;
            }
        }

        if (positiveCount == 0 && negativeCount == 0)
        {
            return 0.0;
        }

        var rawScore = (positiveCount * 0.35) - (negativeCount * 0.45);
        return Math.Round(Math.Clamp(rawScore, -1.0, 1.0), 2);
    }

    private static DetectedIntent ClassifyIntent(string text, ModerationStatus status)
    {
        if (status == ModerationStatus.SpamBlocked)
        {
            return DetectedIntent.Spam;
        }

        var lower = text.ToLowerInvariant();

        foreach (var kw in ComplimentKeywords)
        {
            if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return DetectedIntent.Compliment;
            }
        }

        foreach (var kw in PriceInquiryKeywords)
        {
            if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return DetectedIntent.PriceInquiry;
            }
        }

        foreach (var kw in ProductInquiryKeywords)
        {
            if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return DetectedIntent.ProductInquiry;
            }
        }

        return DetectedIntent.General;
    }

    private static string? GenerateAutoReply(
        string username,
        string text,
        DetectedIntent intent,
        ModerationStatus status)
    {
        if (status != ModerationStatus.Approved)
        {
            return null;
        }

        var lower = text.ToLowerInvariant();

        return intent switch
        {
            DetectedIntent.ProductInquiry => lower switch
            {
                _ when lower.Contains("size") || lower.Contains("sizing") || lower.Contains("fit") =>
                    "We offer free returns within 30 days and sizes run true to size! Check our pinned size guide.",
                _ when lower.Contains("material") || lower.Contains("leather") || lower.Contains("fabric") || lower.Contains("cotton") =>
                    "All our featured products use 100% premium certified materials! Feel free to ask the host for a close-up demo.",
                _ => "Great question! Our AI Co-Host has forwarded your product inquiry directly to the host's earpiece."
            },

            DetectedIntent.PriceInquiry => lower switch
            {
                _ when lower.Contains("shipping") || lower.Contains("delivery") || lower.Contains("fee") =>
                    "Standard shipping takes 24-48 hours with cash-on-delivery options available across all wilayas!",
                _ => "This flash sale price is exclusively available during this live stream! Grab it before the timer expires."
            },

            DetectedIntent.Compliment =>
                "Thank you for the love! Don't forget to tap the heart and share the stream with friends!",

            DetectedIntent.General =>
                $"Welcome to the stream, @{username}! Tap the pinned product to see today's exclusive live deal.",

            _ => null
        };
    }

    private static AiCoHostCue? GenerateHostEarpieceCue(
        string sessionId,
        string username,
        string text,
        DetectedIntent intent,
        ModerationStatus status,
        double sentimentScore,
        DateTime now)
    {
        // Never send toxic remarks or spam bots into host's earpiece
        if (status != ModerationStatus.Approved)
        {
            return null;
        }

        var lower = text.ToLowerInvariant();

        switch (intent)
        {
            case DetectedIntent.ProductInquiry:
                if (lower.Contains("leather") || lower.Contains("genuine"))
                {
                    return new AiCoHostCue(
                        CueId: $"cue_{Guid.NewGuid():N}",
                        SessionId: sessionId,
                        HostPromptText: $"[Earpiece Cue] {username} asks: Is this genuine leather? (Answer: Yes, 100% full-grain leather)",
                        Urgency: CueUrgency.High,
                        RecommendedAction: "Show material texture close-up to the camera",
                        GeneratedAt: now);
                }

                if (lower.Contains("size") || lower.Contains("sizing") || lower.Contains("fit"))
                {
                    return new AiCoHostCue(
                        CueId: $"cue_{Guid.NewGuid():N}",
                        SessionId: sessionId,
                        HostPromptText: $"[Earpiece Cue] {username} asks about sizing/fit (Answer: Runs true to size, recommend standard size)",
                        Urgency: CueUrgency.High,
                        RecommendedAction: "Demonstrate sizing and fit on camera",
                        GeneratedAt: now);
                }

                return new AiCoHostCue(
                    CueId: $"cue_{Guid.NewGuid():N}",
                    SessionId: sessionId,
                    HostPromptText: $"[Earpiece Cue] {username} asks: \"{text}\"",
                    Urgency: CueUrgency.Medium,
                    RecommendedAction: "Address viewer product question directly",
                    GeneratedAt: now);

            case DetectedIntent.PriceInquiry:
                return new AiCoHostCue(
                    CueId: $"cue_{Guid.NewGuid():N}",
                    SessionId: sessionId,
                    HostPromptText: $"[Earpiece Cue] Viewers asking about price/discount from {username} (Answer: Highlight pinned flash deal)",
                    Urgency: CueUrgency.Urgent,
                    RecommendedAction: "Call to action: remind viewers about limited flash inventory and countdown",
                    GeneratedAt: now);

            case DetectedIntent.Compliment when sentimentScore >= 0.5:
                return new AiCoHostCue(
                    CueId: $"cue_{Guid.NewGuid():N}",
                    SessionId: sessionId,
                    HostPromptText: $"[Earpiece Cue] Viewer {username} gave a shoutout: \"{text}\"",
                    Urgency: CueUrgency.Low,
                    RecommendedAction: "Give quick shoutout to viewer",
                    GeneratedAt: now);

            default:
                return null;
        }
    }

    #endregion
}
