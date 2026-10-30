using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Voice;

public enum SpeakerType
{
    Agent,
    Caller
}

public enum SentimentTrajectory
{
    Improving,
    Stable,
    Deteriorating
}

public enum ChurnAlertSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public record WordConfidence(
    string Word,
    double Confidence,
    int StartMs,
    int EndMs
);

public class TranscribedUtterance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CallId { get; set; } = string.Empty;
    public SpeakerType Speaker { get; set; }
    public string Text { get; set; } = string.Empty;
    public int StartOffsetMs { get; set; }
    public int EndOffsetMs { get; set; }
    public int DurationMs => Math.Max(0, EndOffsetMs - StartOffsetMs);
    public double ConfidenceScore { get; set; } = 0.95;
    public double SentimentScore { get; set; }
    public double SentimentVelocity { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<WordConfidence> Words { get; set; } = new();
    public bool TriggeredFrustrationAlert { get; set; }
}

public class FrustrationAlertInfo
{
    public bool IsActive { get; set; }
    public ChurnAlertSeverity Severity { get; set; } = ChurnAlertSeverity.None;
    public string TriggerReason { get; set; } = string.Empty;
    public List<string> TriggerKeywords { get; set; } = new();
    public List<string> EmpathyGuidanceTalkingPoints { get; set; } = new();
    public DateTimeOffset? TriggeredAt { get; set; }
}

public class PostCallStructuredSummary
{
    public string CallId { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> KeyDiscussionPoints { get; set; } = new();
    public List<string> ObjectionsRaised { get; set; } = new();
    public SentimentTrajectory CustomerSentimentTrajectory { get; set; }
    public double OverallSentimentScore { get; set; }
    public List<string> AgreedActionItems { get; set; } = new();
    public string RecommendedNextStep { get; set; } = string.Empty;
    public string CrmDealStage { get; set; } = "Negotiation";
    public string? AssociatedDealId { get; set; }
    public string ExecutiveBrief { get; set; } = string.Empty;
}

public class DiarizedVoiceCall
{
    public string CallId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string CallerName { get; set; } = string.Empty;
    public string CallerPhoneNumber { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string CallTopic { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string Status { get; set; } = "Completed"; // "Live" | "Completed"
    public double OverallSentimentScore { get; set; }
    public SentimentTrajectory SentimentTrajectory { get; set; } = SentimentTrajectory.Stable;
    public FrustrationAlertInfo FrustrationAlert { get; set; } = new();
    public List<TranscribedUtterance> Utterances { get; set; } = new();
    public PostCallStructuredSummary? Summary { get; set; }
    public string? AssociatedDealId { get; set; }
}

public record StreamUtteranceRequest(
    string CallId,
    SpeakerType Speaker,
    string Text,
    int? StartOffsetMs = null,
    int? EndOffsetMs = null,
    double? ConfidenceScore = null,
    List<WordConfidence>? Words = null,
    Guid? TenantId = null
);

public record GenerateSummaryRequest(
    string CallId
);

public interface IVoiceTranscriptionDiarizationService
{
    Task<IReadOnlyList<DiarizedVoiceCall>> ListCallsAsync(Guid? tenantId = null, string? search = null);
    Task<DiarizedVoiceCall?> GetCallByIdAsync(string callId);
    Task<TranscribedUtterance> StreamUtteranceAsync(StreamUtteranceRequest request);
    Task<PostCallStructuredSummary> GeneratePostCallSummaryAsync(string callId);
    double CalculateLexicalSentiment(string text);
    double CalculateSentimentVelocity(double currentSentiment, double? previousSentiment, double deltaSeconds);
    SentimentTrajectory DetermineTrajectory(IReadOnlyList<TranscribedUtterance> utterances);
    FrustrationAlertInfo EvaluateFrustrationAndChurn(IReadOnlyList<TranscribedUtterance> utterances, string latestText);
}

public class VoiceTranscriptionDiarizationService : IVoiceTranscriptionDiarizationService
{
    private readonly ConcurrentDictionary<string, DiarizedVoiceCall> _calls = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private static readonly Dictionary<string, double> PositiveLexicon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["excellent"] = 0.85,
        ["delighted"] = 0.85,
        ["fantastic"] = 0.85,
        ["wonderful"] = 0.80,
        ["perfect"] = 0.80,
        ["awesome"] = 0.80,
        ["love"] = 0.75,
        ["great"] = 0.70,
        ["seamless"] = 0.70,
        ["appreciate"] = 0.65,
        ["grateful"] = 0.65,
        ["pleased"] = 0.60,
        ["resolved"] = 0.60,
        ["satisfied"] = 0.60,
        ["helpful"] = 0.55,
        ["good"] = 0.40,
        ["happy"] = 0.50,
        ["easy"] = 0.45,
        ["thanks"] = 0.40,
        ["thank"] = 0.40,
        ["agree"] = 0.40,
        ["clear"] = 0.35,
        ["improved"] = 0.50,
        ["valuable"] = 0.55,
        ["smooth"] = 0.50,
        ["impressive"] = 0.65,
        ["solved"] = 0.60
    };

    private static readonly Dictionary<string, double> NegativeLexicon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unacceptable"] = -0.85,
        ["fraud"] = -0.90,
        ["cancel"] = -0.80,
        ["refund"] = -0.75,
        ["supervisor"] = -0.70,
        ["furious"] = -0.85,
        ["terrible"] = -0.85,
        ["horrible"] = -0.85,
        ["worst"] = -0.90,
        ["hate"] = -0.80,
        ["scam"] = -0.90,
        ["disaster"] = -0.85,
        ["lawyer"] = -0.85,
        ["broken"] = -0.65,
        ["angry"] = -0.70,
        ["frustrated"] = -0.70,
        ["useless"] = -0.75,
        ["fail"] = -0.60,
        ["failed"] = -0.60,
        ["failure"] = -0.65,
        ["disappointed"] = -0.65,
        ["outage"] = -0.60,
        ["delay"] = -0.40,
        ["delayed"] = -0.40,
        ["expensive"] = -0.40,
        ["complaint"] = -0.50,
        ["ridiculous"] = -0.70,
        ["buggy"] = -0.50,
        ["painful"] = -0.60,
        ["defect"] = -0.50,
        ["escalate"] = -0.65,
        ["dispute"] = -0.75,
        ["chargeback"] = -0.85,
        ["terminate"] = -0.80
    };

    private static readonly HashSet<string> NegationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "no", "never", "cannot", "can't", "cant", "don't", "dont", "won't", "wont", "hardly", "barely"
    };

    private static readonly HashSet<string> Intensifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "very", "extremely", "totally", "completely", "really", "absolutely", "severely", "deeply", "highly"
    };

    private static readonly string[] ChurnKeywords =
    {
        "cancel", "refund", "supervisor", "fraud", "unacceptable", "lawyer", "dispute", "chargeback", "terminate", "escalate"
    };

    public VoiceTranscriptionDiarizationService()
    {
        SeedEnterpriseCalls();
    }

    public Task<IReadOnlyList<DiarizedVoiceCall>> ListCallsAsync(Guid? tenantId = null, string? search = null)
    {
        var query = _calls.Values.AsEnumerable();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                c.CallId.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.CallerName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.AgentName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.CallTopic.Contains(s, StringComparison.OrdinalIgnoreCase)
            );
        }

        var result = query
            .OrderByDescending(c => c.StartedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<DiarizedVoiceCall>>(result);
    }

    public Task<DiarizedVoiceCall?> GetCallByIdAsync(string callId)
    {
        if (string.IsNullOrWhiteSpace(callId))
            return Task.FromResult<DiarizedVoiceCall?>(null);

        _calls.TryGetValue(callId, out var call);
        return Task.FromResult(call);
    }

    public Task<TranscribedUtterance> StreamUtteranceAsync(StreamUtteranceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CallId))
            throw new ArgumentException("CallId must not be empty.", nameof(request));

        var call = _calls.GetOrAdd(request.CallId, id => new DiarizedVoiceCall
        {
            CallId = id,
            TenantId = request.TenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CallerName = "Inbound Enterprise Lead",
            CallerPhoneNumber = "+1 (800) 555-0199",
            AgentName = "SGrowing AI Co-Pilot",
            CallTopic = "Real-Time Inbound Voice Session",
            StartedAtUtc = DateTimeOffset.UtcNow,
            Status = "Live",
            AssociatedDealId = "DEAL-LIVE-01"
        });

        lock (call)
        {
            var sentimentScore = CalculateLexicalSentiment(request.Text);

            // Compute delta seconds and previous sentiment
            TranscribedUtterance? lastSpeakerUtterance = call.Utterances
                .Where(u => u.Speaker == request.Speaker)
                .LastOrDefault();

            double deltaSeconds = 1.0;
            if (lastSpeakerUtterance != null)
            {
                var timeDiff = (DateTimeOffset.UtcNow - lastSpeakerUtterance.Timestamp).TotalSeconds;
                deltaSeconds = Math.Max(timeDiff, 1.0);
            }

            var velocity = CalculateSentimentVelocity(sentimentScore, lastSpeakerUtterance?.SentimentScore, deltaSeconds);

            int startMs = request.StartOffsetMs ?? (call.Utterances.LastOrDefault()?.EndOffsetMs ?? 0);
            int durationEstimate = Math.Max(1200, request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 350);
            int endMs = request.EndOffsetMs ?? (startMs + durationEstimate);

            var utterance = new TranscribedUtterance
            {
                CallId = call.CallId,
                Speaker = request.Speaker,
                Text = request.Text.Trim(),
                StartOffsetMs = startMs,
                EndOffsetMs = endMs,
                ConfidenceScore = Math.Clamp(request.ConfidenceScore ?? 0.96, 0.0, 1.0),
                SentimentScore = sentimentScore,
                SentimentVelocity = velocity,
                Timestamp = DateTimeOffset.UtcNow,
                Words = request.Words ?? GenerateWordConfidenceTokens(request.Text, startMs, endMs, request.ConfidenceScore ?? 0.96)
            };

            call.Utterances.Add(utterance);
            call.TotalDurationSeconds = (int)Math.Ceiling(endMs / 1000.0);

            // Re-evaluate Call Overall Sentiment & Trajectory
            call.OverallSentimentScore = Math.Round(call.Utterances.Average(u => u.SentimentScore), 3);
            call.SentimentTrajectory = DetermineTrajectory(call.Utterances);

            // Frustration & Churn Alert Detection
            var alert = EvaluateFrustrationAndChurn(call.Utterances, request.Text);
            if (alert.IsActive)
            {
                utterance.TriggeredFrustrationAlert = true;
                call.FrustrationAlert = alert;
            }
            else if (!call.FrustrationAlert.IsActive)
            {
                call.FrustrationAlert = alert;
            }

            return Task.FromResult(utterance);
        }
    }

    public Task<PostCallStructuredSummary> GeneratePostCallSummaryAsync(string callId)
    {
        if (string.IsNullOrWhiteSpace(callId))
            throw new ArgumentException("CallId must not be empty.", nameof(callId));

        if (!_calls.TryGetValue(callId, out var call))
            throw new KeyNotFoundException($"Call with ID '{callId}' was not found.");

        lock (call)
        {
            var summary = SynthesizeSummary(call);
            call.Summary = summary;
            call.Status = "Completed";
            call.EndedAtUtc ??= DateTimeOffset.UtcNow;
            return Task.FromResult(summary);
        }
    }

    public double CalculateLexicalSentiment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0.0;

        // Tokenize into clean words
        var words = Regex.Matches(text.ToLowerInvariant(), @"[a-z']+")
            .Select(m => m.Value)
            .ToList();

        if (words.Count == 0)
            return 0.0;

        double totalScore = 0.0;
        int sentimentWordsCount = 0;
        bool nextIsNegated = false;
        double currentMultiplier = 1.0;

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (NegationWords.Contains(word))
            {
                nextIsNegated = true;
                continue;
            }

            if (Intensifiers.Contains(word))
            {
                currentMultiplier = 1.4;
                continue;
            }

            double wordScore = 0.0;
            bool isSentimentWord = false;

            if (PositiveLexicon.TryGetValue(word, out var posScore))
            {
                wordScore = posScore;
                isSentimentWord = true;
            }
            else if (NegativeLexicon.TryGetValue(word, out var negScore))
            {
                wordScore = negScore;
                isSentimentWord = true;
            }

            if (isSentimentWord)
            {
                if (nextIsNegated)
                {
                    // Invert and slightly attenuate
                    wordScore = -(wordScore * 0.85);
                }

                wordScore *= currentMultiplier;
                totalScore += wordScore;
                sentimentWordsCount++;

                // Reset modifiers after applying
                nextIsNegated = false;
                currentMultiplier = 1.0;
            }
        }

        if (sentimentWordsCount == 0)
            return 0.0;

        // Bounded average + sum contribution clamped to [-1.0, 1.0]
        double normalized = totalScore / Math.Max(1.0, Math.Sqrt(sentimentWordsCount));
        return Math.Round(Math.Clamp(normalized, -1.0, 1.0), 3);
    }

    public double CalculateSentimentVelocity(double currentSentiment, double? previousSentiment, double deltaSeconds)
    {
        if (!previousSentiment.HasValue)
            return 0.0;

        double safeDelta = Math.Max(deltaSeconds, 1.0);
        double velocity = (currentSentiment - previousSentiment.Value) / safeDelta;
        return Math.Round(velocity, 4);
    }

    public SentimentTrajectory DetermineTrajectory(IReadOnlyList<TranscribedUtterance> utterances)
    {
        if (utterances == null || utterances.Count < 2)
            return SentimentTrajectory.Stable;

        var callerUtterances = utterances.Where(u => u.Speaker == SpeakerType.Caller).ToList();
        var evalList = callerUtterances.Count >= 2 ? callerUtterances : utterances.ToList();

        if (evalList.Count < 2)
            return SentimentTrajectory.Stable;

        // Take initial window vs final window
        int sampleSize = Math.Max(1, evalList.Count / 3);
        double initialAvg = evalList.Take(sampleSize).Average(u => u.SentimentScore);
        double finalAvg = evalList.TakeLast(sampleSize).Average(u => u.SentimentScore);

        double delta = finalAvg - initialAvg;

        if (delta >= 0.20)
            return SentimentTrajectory.Improving;
        if (delta <= -0.20)
            return SentimentTrajectory.Deteriorating;

        return SentimentTrajectory.Stable;
    }

    public FrustrationAlertInfo EvaluateFrustrationAndChurn(IReadOnlyList<TranscribedUtterance> utterances, string latestText)
    {
        var alert = new FrustrationAlertInfo();
        var detectedKeywords = new List<string>();

        // 1. Check keyword triggers
        string lowerText = latestText?.ToLowerInvariant() ?? string.Empty;
        foreach (var keyword in ChurnKeywords)
        {
            if (Regex.IsMatch(lowerText, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase))
            {
                detectedKeywords.Add(keyword);
            }
        }

        // 2. Check consecutive caller turns < -0.60
        var callerUtterances = utterances
            .Where(u => u.Speaker == SpeakerType.Caller)
            .ToList();

        bool hasTwoConsecutiveNegative = false;
        if (callerUtterances.Count >= 2)
        {
            var lastTwo = callerUtterances.TakeLast(2).ToList();
            if (lastTwo[0].SentimentScore < -0.60 && lastTwo[1].SentimentScore < -0.60)
            {
                hasTwoConsecutiveNegative = true;
            }
        }

        bool hasKeywords = detectedKeywords.Count > 0;

        if (hasKeywords || hasTwoConsecutiveNegative)
        {
            alert.IsActive = true;
            alert.TriggeredAt = DateTimeOffset.UtcNow;
            alert.TriggerKeywords = detectedKeywords;

            if (hasKeywords && hasTwoConsecutiveNegative)
            {
                alert.Severity = ChurnAlertSeverity.Critical;
                alert.TriggerReason = $"Critical Churn Threat: Caller sentiment dropped below -0.60 for consecutive turns and trigger words detected: [{string.Join(", ", detectedKeywords)}].";
            }
            else if (hasKeywords)
            {
                alert.Severity = ChurnAlertSeverity.High;
                alert.TriggerReason = $"High Frustration Alert: High-risk keywords detected: [{string.Join(", ", detectedKeywords)}].";
            }
            else
            {
                alert.Severity = ChurnAlertSeverity.High;
                alert.TriggerReason = "High Churn Threat: Caller sentiment sustained below -0.60 over two consecutive turns.";
            }

            // Empathy Guidance Generation
            alert.EmpathyGuidanceTalkingPoints = GenerateEmpathyTalkingPoints(detectedKeywords, hasTwoConsecutiveNegative);
        }
        else
        {
            // Check mild warning
            if (callerUtterances.LastOrDefault()?.SentimentScore < -0.50)
            {
                alert.IsActive = false;
                alert.Severity = ChurnAlertSeverity.Medium;
                alert.TriggerReason = "Caller sentiment dipping; monitor tone.";
            }
            else
            {
                alert.IsActive = false;
                alert.Severity = ChurnAlertSeverity.None;
            }
        }

        return alert;
    }

    private static List<string> GenerateEmpathyTalkingPoints(List<string> keywords, bool consecutiveNegative)
    {
        var points = new List<string>();

        if (keywords.Any(k => k is "cancel" or "refund" or "dispute" or "chargeback" or "terminate"))
        {
            points.Add("I completely understand how unacceptable this situation is. Let me take personal ownership of this account right now and pause any billing concerns while we resolve the issue.");
            points.Add("I am reviewing your subscription and can issue an immediate courtesy credit while our senior lead addresses the root cause.");
        }

        if (keywords.Any(k => k is "supervisor" or "escalate" or "manager" or "lawyer"))
        {
            points.Add("You have every right to expect executive clarity on this. I am personally escalating this ticket directly to our VP of Engineering and will share their direct bridge link.");
            points.Add("I will stay on this line with you and guarantee you will not be transferred to another queue.");
        }

        if (keywords.Any(k => k is "unacceptable" or "fraud" or "terrible" or "disaster"))
        {
            points.Add("I hear how disruptive this has been for your daily logistics operations, and I sincerely apologize for the frustration it has caused your team.");
            points.Add("Let's set aside procedural steps and focus 100% on immediate technical restoration for your active orders.");
        }

        if (consecutiveNegative && points.Count < 2)
        {
            points.Add("I hear how frustrating this experience has been for you. I am logging these details so you never have to repeat yourself, and I will see this through to completion today.");
            points.Add("Please allow me 60 seconds to pull up the system audit log so we can fix this immediately.");
        }

        if (points.Count == 0)
        {
            points.Add("I acknowledge your concern and I am actively working to make this right for you.");
            points.Add("Thank you for your patience while I ensure our engineering team resolves this.");
        }

        return points;
    }

    private PostCallStructuredSummary SynthesizeSummary(DiarizedVoiceCall call)
    {
        var keyPoints = new List<string>();
        var objections = new List<string>();
        var actionItems = new List<string>();

        foreach (var u in call.Utterances)
        {
            if (u.SentimentScore < -0.30 || u.TriggeredFrustrationAlert)
            {
                if (!objections.Contains(u.Text) && objections.Count < 4)
                {
                    objections.Add($"Caller objection ({u.SentimentScore:F2}): \"{u.Text}\"");
                }
            }

            if (u.Text.Contains("API", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("renewal", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("webhook", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("latency", StringComparison.OrdinalIgnoreCase) ||
                u.Text.Contains("order", StringComparison.OrdinalIgnoreCase))
            {
                if (keyPoints.Count < 5 && !keyPoints.Contains(u.Text))
                {
                    keyPoints.Add($"Discussion on {call.CallTopic}: {u.Speaker}: {u.Text}");
                }
            }
        }

        if (keyPoints.Count == 0)
        {
            keyPoints.Add($"Overview of {call.CallTopic} between {call.CallerName} and {call.AgentName}.");
            keyPoints.Add("Standard operational sync on real-time telephony stream and SLA metrics.");
        }

        // Action Items & Recommendations based on trajectory
        string nextStep;
        string dealStage;

        if (call.SentimentTrajectory == SentimentTrajectory.Improving)
        {
            actionItems.Add("Send updated expansion contract addendum with agreed volume discount.");
            actionItems.Add("Schedule 15-minute verification check-in with integration engineer.");
            nextStep = "Deliver contract amendment via DocuSign and confirm technical green light by end of day.";
            dealStage = "Closed Won";
        }
        else if (call.SentimentTrajectory == SentimentTrajectory.Deteriorating || call.FrustrationAlert.IsActive)
        {
            actionItems.Add("Trigger Tier-3 Executive De-escalation Protocol and alert Customer Success VP.");
            actionItems.Add("Issue billing courtesy credit memo and conduct full webhook latency post-mortem.");
            nextStep = "Account Executive follow-up call within 2 hours with written RCA (Root Cause Analysis).";
            dealStage = "At Risk";
        }
        else
        {
            actionItems.Add("Follow up with updated webhook payload documentation and schema samples.");
            actionItems.Add("Confirm upcoming quarterly volume projections with billing team.");
            nextStep = "Send summary email to client with recap notes and API documentation links.";
            dealStage = "Negotiation";
        }

        return new PostCallStructuredSummary
        {
            CallId = call.CallId,
            GeneratedAt = DateTimeOffset.UtcNow,
            KeyDiscussionPoints = keyPoints,
            ObjectionsRaised = objections,
            CustomerSentimentTrajectory = call.SentimentTrajectory,
            OverallSentimentScore = call.OverallSentimentScore,
            AgreedActionItems = actionItems,
            RecommendedNextStep = nextStep,
            CrmDealStage = dealStage,
            AssociatedDealId = call.AssociatedDealId ?? $"DEAL-{call.CallId}",
            ExecutiveBrief = $"Call '{call.CallTopic}' concluded with {call.SentimentTrajectory} sentiment (Score: {call.OverallSentimentScore:F2}). Next step: {nextStep}"
        };
    }

    private static List<WordConfidence> GenerateWordConfidenceTokens(string text, int startMs, int endMs, double baseConfidence)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new List<WordConfidence>();

        var list = new List<WordConfidence>();
        int span = Math.Max(10, (endMs - startMs) / words.Length);

        for (int i = 0; i < words.Length; i++)
        {
            int wStart = startMs + (i * span);
            int wEnd = (i == words.Length - 1) ? endMs : (wStart + span);
            // Slight jitter on word confidence around base
            double conf = Math.Clamp(baseConfidence - (0.02 * (i % 3)), 0.70, 0.99);
            list.Add(new WordConfidence(words[i], Math.Round(conf, 3), wStart, wEnd));
        }

        return list;
    }

    private void SeedEnterpriseCalls()
    {
        // -------------------------------------------------------------
        // Call 1: Sarah Chen - Apex Logistics (Improving Trajectory)
        // -------------------------------------------------------------
        var call1 = new DiarizedVoiceCall
        {
            CallId = "CALL-8921-EXP",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CallerName = "Sarah Chen",
            CallerPhoneNumber = "+1 (415) 892-1002",
            AgentName = "Marcus Vance",
            CallTopic = "Warehouse Automation API Renewal & Volume Expansion",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-42),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-27),
            TotalDurationSeconds = 885,
            Status = "Completed",
            OverallSentimentScore = 0.58,
            SentimentTrajectory = SentimentTrajectory.Improving,
            AssociatedDealId = "DEAL-APEX-EXP-2026",
            FrustrationAlert = new FrustrationAlertInfo { IsActive = false, Severity = ChurnAlertSeverity.None }
        };

        AddSeededUtterances(call1, new List<(SpeakerType, string, int, int, double, double)>
        {
            (SpeakerType.Agent, "Hello Sarah, thank you for joining today. How are the new automated warehouse fulfillment webhooks performing for Apex?", 0, 5200, 0.40, 0.0),
            (SpeakerType.Caller, "Hi Marcus. We had a few initial concerns regarding p99 latency during peak flash sales last week.", 6000, 12500, -0.15, -0.08),
            (SpeakerType.Agent, "Understood. Our team deployed the Redis connection pool patch on Wednesday, and global latency dropped to 14 milliseconds.", 13200, 21000, 0.55, 0.09),
            (SpeakerType.Caller, "That is fantastic news. We checked our Grafana metrics yesterday and the throughput was remarkably smooth.", 22000, 29000, 0.78, 0.03),
            (SpeakerType.Agent, "Glad to hear that. With your volume growing 40%, we can lock in the tier-4 enterprise pricing on your annual renewal.", 30200, 38000, 0.65, -0.01),
            (SpeakerType.Caller, "We agree completely. That is a wonderful offer and makes our procurement approval very easy. Let's finalize it.", 39000, 47500, 0.88, 0.02)
        });
        call1.Summary = SynthesizeSummary(call1);
        _calls[call1.CallId] = call1;

        // -------------------------------------------------------------
        // Call 2: David Miller - Global Freight (Dispute De-escalation)
        // -------------------------------------------------------------
        var call2 = new DiarizedVoiceCall
        {
            CallId = "CALL-7419-DIS",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CallerName = "David Miller",
            CallerPhoneNumber = "+1 (312) 440-9921",
            AgentName = "Jessica Taylor",
            CallTopic = "Billing Overage Dispute & Contract Escalation",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-25),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-8),
            TotalDurationSeconds = 1010,
            Status = "Completed",
            OverallSentimentScore = 0.02,
            SentimentTrajectory = SentimentTrajectory.Improving,
            AssociatedDealId = "DEAL-GFREIGHT-RET-2026",
            FrustrationAlert = new FrustrationAlertInfo
            {
                IsActive = false,
                Severity = ChurnAlertSeverity.High,
                TriggerReason = "High Frustration Alert: High-risk keywords detected: [unacceptable, cancel, refund]. Resolved via courtesy credit.",
                TriggerKeywords = new List<string> { "unacceptable", "cancel", "refund" },
                EmpathyGuidanceTalkingPoints = new List<string>
                {
                    "I completely understand how unacceptable this unexpected charge feels. Let me take ownership and apply a courtesy credit immediately."
                }
            }
        };

        AddSeededUtterances(call2, new List<(SpeakerType, string, int, int, double, double)>
        {
            (SpeakerType.Caller, "This billing statement is completely unacceptable. You charged us for 50,000 extra API calls and I will cancel our contract and demand an immediate refund!", 0, 8500, -0.85, 0.0),
            (SpeakerType.Agent, "David, I completely understand your frustration with this unexpected overage charge. Let me take personal ownership of this right now and review your ledger.", 9200, 18000, 0.35, 0.12),
            (SpeakerType.Caller, "Our accounting director was furious this morning. We were told sandbox calls were exempt from billable limits.", 19000, 26000, -0.65, 0.03),
            (SpeakerType.Agent, "You are 100% correct. A misconfigured test key routed sandbox traffic through production. I am issuing a direct $1,250 refund credit to your account right this second.", 27200, 37000, 0.60, 0.11),
            (SpeakerType.Caller, "Thank you, Jessica. I appreciate you fixing this without giving me the runaround. That is helpful.", 38500, 45000, 0.52, 0.15)
        });
        call2.Summary = SynthesizeSummary(call2);
        _calls[call2.CallId] = call2;

        // -------------------------------------------------------------
        // Call 3: Elena Rostova - EuroLogistics (Active Churn Alert)
        // -------------------------------------------------------------
        var call3 = new DiarizedVoiceCall
        {
            CallId = "CALL-5120-OUT",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CallerName = "Elena Rostova",
            CallerPhoneNumber = "+44 20 7946 0912",
            AgentName = "Liam O'Connor",
            CallTopic = "Critical Webhook Latency & Ingest Outage Incident",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-12),
            EndedAtUtc = null,
            TotalDurationSeconds = 620,
            Status = "Live",
            OverallSentimentScore = -0.74,
            SentimentTrajectory = SentimentTrajectory.Deteriorating,
            AssociatedDealId = "DEAL-EUROLOG-RISK-2026",
            FrustrationAlert = new FrustrationAlertInfo
            {
                IsActive = true,
                Severity = ChurnAlertSeverity.Critical,
                TriggerReason = "Critical Churn Threat: Caller sentiment dropped below -0.60 for consecutive turns and trigger words detected: [unacceptable, supervisor, fraud].",
                TriggerKeywords = new List<string> { "unacceptable", "supervisor", "fraud" },
                TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                EmpathyGuidanceTalkingPoints = new List<string>
                {
                    "I hear how disruptive this webhook outage is to your dispatch fleet. I am personally escalating this ticket directly to our VP of Engineering.",
                    "I will stay on this line with you and guarantee you will not be transferred to another queue."
                }
            }
        };

        AddSeededUtterances(call3, new List<(SpeakerType, string, int, int, double, double)>
        {
            (SpeakerType.Caller, "Liam, our warehouse dispatchers are dead in the water. Webhooks are dropping 30% of incoming deliveries.", 0, 7000, -0.45, 0.0),
            (SpeakerType.Agent, "Elena, I am checking our EU-West gateway telemetry right now to identify the ingress bottleneck.", 7500, 14000, 0.20, 0.08),
            (SpeakerType.Caller, "This has been going on for two hours. It is completely unacceptable and feels like fraud given your 99.99% SLA claim!", 14800, 23000, -0.88, -0.12),
            (SpeakerType.Caller, "Get me a supervisor or your VP of Infrastructure immediately. We are losing thousands of euros every minute!", 24000, 31500, -0.82, 0.01)
        });
        call3.Summary = SynthesizeSummary(call3);
        _calls[call3.CallId] = call3;

        // -------------------------------------------------------------
        // Call 4: Robert Sterling - Sterling Retail (Stable Trajectory)
        // -------------------------------------------------------------
        var call4 = new DiarizedVoiceCall
        {
            CallId = "CALL-3382-STB",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CallerName = "Robert Sterling",
            CallerPhoneNumber = "+1 (206) 555-8321",
            AgentName = "Sophia Martinez",
            CallTopic = "Quarterly Business Review & Multi-Storefront Webhook Setup",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-60),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-41),
            TotalDurationSeconds = 1140,
            Status = "Completed",
            OverallSentimentScore = 0.38,
            SentimentTrajectory = SentimentTrajectory.Stable,
            AssociatedDealId = "DEAL-STERLING-QBR-2026",
            FrustrationAlert = new FrustrationAlertInfo { IsActive = false, Severity = ChurnAlertSeverity.None }
        };

        AddSeededUtterances(call4, new List<(SpeakerType, string, int, int, double, double)>
        {
            (SpeakerType.Agent, "Good afternoon Robert. Welcome to our quarterly business review. Let's look over your 12 storefront integrations.", 0, 7500, 0.35, 0.0),
            (SpeakerType.Caller, "Good afternoon Sophia. Overall the order synchronization has been steady and reliable this past quarter.", 8200, 15000, 0.42, 0.01),
            (SpeakerType.Agent, "Excellent. Your average response time across Shopify and Magento was 94 milliseconds, well within your SLA targets.", 16000, 24000, 0.55, 0.01),
            (SpeakerType.Caller, "Good to hear. We are preparing to launch our Canadian storefront next month and wanted to confirm webhook endpoints.", 25000, 33000, 0.32, -0.01),
            (SpeakerType.Agent, "Everything is configured and tested. We will provide staging credentials by tomorrow morning.", 34000, 41000, 0.40, 0.01),
            (SpeakerType.Caller, "Thank you, Sophia. That sounds like a clear and sensible plan.", 42000, 48000, 0.45, 0.01)
        });
        call4.Summary = SynthesizeSummary(call4);
        _calls[call4.CallId] = call4;
    }

    private static void AddSeededUtterances(DiarizedVoiceCall call, List<(SpeakerType Speaker, string Text, int StartMs, int EndMs, double Sentiment, double Velocity)> items)
    {
        foreach (var item in items)
        {
            var u = new TranscribedUtterance
            {
                CallId = call.CallId,
                Speaker = item.Speaker,
                Text = item.Text,
                StartOffsetMs = item.StartMs,
                EndOffsetMs = item.EndMs,
                ConfidenceScore = 0.96,
                SentimentScore = item.Sentiment,
                SentimentVelocity = item.Velocity,
                Timestamp = call.StartedAtUtc.AddMilliseconds(item.StartMs),
                Words = GenerateWordConfidenceTokens(item.Text, item.StartMs, item.EndMs, 0.96)
            };

            if (item.Sentiment < -0.70 || ChurnKeywords.Any(k => item.Text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                u.TriggeredFrustrationAlert = true;
            }

            call.Utterances.Add(u);
        }
    }
}
