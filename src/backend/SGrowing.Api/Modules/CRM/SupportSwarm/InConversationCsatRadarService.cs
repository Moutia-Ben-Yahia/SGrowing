using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.CRM.SupportSwarm;

/// <summary>
/// Defines the real-time in-conversation customer satisfaction degradation and escalation radar interface.
/// </summary>
public interface IInConversationCsatRadarService
{
    CsatAnalysisResult AnalyzeConversation(string ticketId, string customerId, IEnumerable<SupportConversationMessage> messageThread);
    IReadOnlyList<CsatDropAlert> GetActiveAlerts();
    CsatDropAlert AcknowledgeAlert(string alertId, string? acknowledgedBy = null);
    CsatDropAlert? GetAlert(string alertId);
    IReadOnlyList<CsatDropAlert> GetAllAlerts();
}

/// <summary>
/// Real-time satisfaction degradation detector.
/// Scans ongoing omnichannel conversations for sentiment velocity drops, frustration repetition loops,
/// and legal/fraud trigger terms, synthesizing predictive CSAT scores and proactive de-escalation coupons.
/// </summary>
public sealed class InConversationCsatRadarService : IInConversationCsatRadarService
{
    private static readonly string[] EscalationKeywords =
    [
        "arnaque",
        "police",
        "avocat",
        "escroc",
        "directeur",
        "remboursement immédiatement",
        "remboursement immediatement",
        "scam",
        "fraud"
    ];

    private readonly Lock _lock = new();
    private readonly Dictionary<string, CsatDropAlert> _alerts = [];

    /// <summary>
    /// Analyzes a thread of conversation messages, predicts CSAT, detects degradation anomalies, and emits alerts.
    /// </summary>
    public CsatAnalysisResult AnalyzeConversation(string ticketId, string customerId, IEnumerable<SupportConversationMessage> messageThread)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(messageThread);

        var messages = messageThread.OrderBy(m => m.TimestampUtc).ToList();
        var customerMessages = messages
            .Where(m => string.Equals(m.SenderType, "Customer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (customerMessages.Count == 0)
        {
            return new CsatAnalysisResult(
                TicketId: ticketId,
                CustomerId: customerId,
                PredictedCsatScore: 4.50,
                RiskLevel: CsatRiskLevel.LowRisk,
                SentimentDelta: 0.0,
                TriggerKeywordsDetected: false,
                MatchedKeywords: [],
                FrustrationLoopDetected: false,
                SuggestedDeEscalationDiscountPct: 0.0m,
                Alert: null
            );
        }

        var initialCustomerMessage = customerMessages[0];
        var latestCustomerMessage = customerMessages[^1];

        var initialSentiment = initialCustomerMessage.SentimentScore;
        var latestSentiment = latestCustomerMessage.SentimentScore;
        var sentimentDrop = initialSentiment - latestSentiment;
        var sentimentDegradationDetected = sentimentDrop > 0.40;

        // Keyword scan across all customer messages
        List<string> matchedKeywords = [];
        foreach (var keyword in EscalationKeywords)
        {
            if (customerMessages.Any(m => m.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                matchedKeywords.Add(keyword);
            }
        }
        var triggerKeywordsDetected = matchedKeywords.Count > 0;

        // Frustration repetition loop detection: 3+ customer messages with negative sentiment (< 0)
        var negativeMessagesCount = customerMessages.Count(m => m.SentimentScore < 0.0);
        var frustrationLoopDetected = negativeMessagesCount >= 3;

        // Predict CSAT score (1.0 to 5.0)
        // Baseline: neutral (0.0) -> 3.5; positive (+1.0) -> 5.0; negative (-1.0) -> 2.0
        var baseScore = 3.50 + (latestSentiment * 1.50);

        if (sentimentDegradationDetected)
        {
            baseScore -= (0.50 + (sentimentDrop * 0.40));
        }

        if (frustrationLoopDetected)
        {
            baseScore -= (0.50 + ((negativeMessagesCount - 3) * 0.20));
        }

        if (triggerKeywordsDetected)
        {
            baseScore -= (1.60 + (matchedKeywords.Count * 0.30));
        }

        var predictedCsat = Math.Clamp(Math.Round(baseScore, 2), 1.0, 5.0);

        // If trigger keywords detected, ensure it is in the critical band (< 2.0)
        if (triggerKeywordsDetected && predictedCsat > 1.80)
        {
            predictedCsat = 1.80;
        }

        // Determine Risk Level
        CsatRiskLevel riskLevel;
        if (triggerKeywordsDetected || predictedCsat < 1.80)
        {
            riskLevel = CsatRiskLevel.CriticalInterventionNeeded;
        }
        else if (sentimentDegradationDetected || predictedCsat < 2.80)
        {
            riskLevel = CsatRiskLevel.HighRiskChurnCandidate;
        }
        else if (predictedCsat < 3.80 || frustrationLoopDetected || negativeMessagesCount >= 2)
        {
            riskLevel = CsatRiskLevel.ModerateAttention;
        }
        else
        {
            riskLevel = CsatRiskLevel.LowRisk;
        }

        // Automated de-escalation discount recommendation (5% to 15% coupon)
        var discountPct = riskLevel switch
        {
            CsatRiskLevel.CriticalInterventionNeeded => 15.0m,
            CsatRiskLevel.HighRiskChurnCandidate => 10.0m,
            CsatRiskLevel.ModerateAttention => 5.0m,
            _ => 0.0m
        };

        CsatDropAlert? alert = null;
        if (riskLevel != CsatRiskLevel.LowRisk)
        {
            List<string> triggerReasons = [];
            if (triggerKeywordsDetected)
            {
                triggerReasons.Add($"Legal/fraud escalation keywords detected: [{string.Join(", ", matchedKeywords)}]");
            }
            if (sentimentDegradationDetected)
            {
                triggerReasons.Add($"Severe sentiment degradation: drop of {sentimentDrop:F2} (> 0.40 threshold)");
            }
            if (frustrationLoopDetected)
            {
                triggerReasons.Add($"Repetition frustration loop: {negativeMessagesCount} negative customer messages");
            }
            if (triggerReasons.Count == 0)
            {
                triggerReasons.Add($"Sub-optimal predicted CSAT ({predictedCsat:F2}/5.00)");
            }

            var triggerReasonText = string.Join(" | ", triggerReasons);

            lock (_lock)
            {
                alert = new CsatDropAlert(
                    AlertId: $"ALT-{Guid.NewGuid():N}",
                    TicketId: ticketId,
                    CustomerId: customerId,
                    PredictedCsatScore: predictedCsat,
                    RiskLevel: riskLevel,
                    TriggerReason: triggerReasonText,
                    SuggestedDeEscalationDiscountPct: discountPct,
                    EmittedAtUtc: DateTime.UtcNow,
                    Acknowledged: false
                );

                _alerts[alert.AlertId] = alert;
            }
        }

        return new CsatAnalysisResult(
            TicketId: ticketId,
            CustomerId: customerId,
            PredictedCsatScore: predictedCsat,
            RiskLevel: riskLevel,
            SentimentDelta: Math.Round(sentimentDrop, 4),
            TriggerKeywordsDetected: triggerKeywordsDetected,
            MatchedKeywords: matchedKeywords.AsReadOnly(),
            FrustrationLoopDetected: frustrationLoopDetected,
            SuggestedDeEscalationDiscountPct: discountPct,
            Alert: alert
        );
    }

    /// <summary>
    /// Retrieves all active, unacknowledged CSAT degradation alerts.
    /// </summary>
    public IReadOnlyList<CsatDropAlert> GetActiveAlerts()
    {
        lock (_lock)
        {
            return [.. _alerts.Values.Where(a => !a.Acknowledged).OrderByDescending(a => a.EmittedAtUtc)];
        }
    }

    /// <summary>
    /// Acknowledges an active alert, recording the supervisor ID and timestamp.
    /// </summary>
    public CsatDropAlert AcknowledgeAlert(string alertId, string? acknowledgedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);

        lock (_lock)
        {
            if (!_alerts.TryGetValue(alertId, out var existingAlert))
            {
                throw new KeyNotFoundException($"CSAT drop alert '{alertId}' was not found.");
            }

            var updatedAlert = existingAlert with
            {
                Acknowledged = true,
                AcknowledgedAtUtc = DateTime.UtcNow,
                AcknowledgedBy = acknowledgedBy ?? "SeniorHumanSupervisor"
            };

            _alerts[alertId] = updatedAlert;
            return updatedAlert;
        }
    }

    /// <summary>
    /// Retrieves a specific alert by its ID.
    /// </summary>
    public CsatDropAlert? GetAlert(string alertId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);

        lock (_lock)
        {
            return _alerts.GetValueOrDefault(alertId);
        }
    }

    /// <summary>
    /// Retrieves all historical and active alerts.
    /// </summary>
    public IReadOnlyList<CsatDropAlert> GetAllAlerts()
    {
        lock (_lock)
        {
            return [.. _alerts.Values.OrderByDescending(a => a.EmittedAtUtc)];
        }
    }
}
