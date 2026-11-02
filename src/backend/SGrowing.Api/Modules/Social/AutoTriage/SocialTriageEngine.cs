using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Social.AutoTriage;

public class SocialTriageEngine
{
    private readonly ConcurrentDictionary<string, TriageQueueItem> _queue = new(StringComparer.OrdinalIgnoreCase);

    public SocialTriageEngine()
    {
        SeedDemoQueue();
    }

    private void SeedDemoQueue()
    {
        var msg1 = new InboundSocialMessage(
            MessageId: "msg_wa_9901",
            Channel: "WhatsApp",
            SenderHandle: "+14155552671",
            SenderName: "Alexander Wright (Global Retail)",
            MessageText: "Hi, our European checkout portal has thrown a 500 error for 20 minutes! We are losing orders, please fix immediately or we cancel our contract!",
            TimestampUtc: DateTime.UtcNow.AddMinutes(-12)
        );

        var assessment1 = new TriageAssessment(
            MessageId: msg1.MessageId,
            Sentiment: "SevereHazard",
            SentimentScore: 0.12,
            IntentCategory: "CancellationRisk",
            UrgencyScore: 98,
            RequiresImmediateEscalation: true,
            Confidence: 0.96
        );

        var item1 = new TriageQueueItem(
            QueueId: "triage_wa_9901",
            Message: msg1,
            Assessment: assessment1,
            AssignedAgentRole: "ExecutiveEscalationLead",
            SlaDeadlineUtc: DateTime.UtcNow.AddMinutes(3),
            Status: "PendingReview",
            DraftReply: "Hi Alexander, our Incident Response Engineering team is actively mitigating this gateway incident. A senior CS lead has been notified and we are monitoring your store checkout status."
        );

        var msg2 = new InboundSocialMessage(
            MessageId: "msg_li_8820",
            Channel: "LinkedIn",
            SenderHandle: "linkedin.com/in/sophia-patel",
            SenderName: "Sophia Patel (FinTech VP)",
            MessageText: "Hello team! We are evaluating Growth OS for our 500-seat multi-tenant financial SaaS. Can we schedule an enterprise demo and review SOC2 compliance?",
            TimestampUtc: DateTime.UtcNow.AddMinutes(-25)
        );

        var assessment2 = new TriageAssessment(
            MessageId: msg2.MessageId,
            Sentiment: "Positive",
            SentimentScore: 0.94,
            IntentCategory: "SalesInquiry",
            UrgencyScore: 82,
            RequiresImmediateEscalation: false,
            Confidence: 0.98
        );

        var item2 = new TriageQueueItem(
            QueueId: "triage_li_8820",
            Message: msg2,
            Assessment: assessment2,
            AssignedAgentRole: "EnterpriseAccountExecutive",
            SlaDeadlineUtc: DateTime.UtcNow.AddMinutes(35),
            Status: "DraftGenerated",
            DraftReply: "Hi Sophia, thanks for reaching out! We would love to showcase Growth OS. I have reserved priority demo slots with our Enterprise Architecture team: https://cal.growth-os.cloud/enterprise-demo"
        );

        _queue[item1.QueueId] = item1;
        _queue[item2.QueueId] = item2;
    }

    public Task<TriageQueueItem> IngestAndTriageMessageAsync(IngestSocialMessageRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messageId = $"msg_{Guid.NewGuid():N}";
        var queueId = $"triage_{Guid.NewGuid():N}";

        var message = new InboundSocialMessage(
            MessageId: messageId,
            Channel: request.Channel ?? "WhatsApp",
            SenderHandle: request.SenderHandle ?? "Unknown",
            SenderName: request.SenderName ?? "Customer",
            MessageText: request.MessageText ?? string.Empty,
            TimestampUtc: DateTime.UtcNow,
            Metadata: request.Metadata
        );

        var assessment = EvaluateMessage(message);
        var slaMinutes = assessment.RequiresImmediateEscalation ? 15 : (assessment.IntentCategory == "SalesInquiry" ? 60 : 120);
        var role = DetermineAssignedRole(assessment);
        var draft = GenerateDraftResponse(message, assessment);

        var queueItem = new TriageQueueItem(
            QueueId: queueId,
            Message: message,
            Assessment: assessment,
            AssignedAgentRole: role,
            SlaDeadlineUtc: DateTime.UtcNow.AddMinutes(slaMinutes),
            Status: "DraftGenerated",
            DraftReply: draft
        );

        _queue[queueItem.QueueId] = queueItem;
        return Task.FromResult(queueItem);
    }

    public Task<TriageQueueItem> ApproveAndSendReplyAsync(string queueId, ApproveReplyRequest request, CancellationToken ct = default)
    {
        if (!_queue.TryGetValue(queueId, out var item))
        {
            throw new KeyNotFoundException($"Triage queue item '{queueId}' not found.");
        }

        var updated = item with
        {
            Status = "ApprovedAndSent",
            DraftReply = request.CustomizedReplyText ?? item.DraftReply
        };

        _queue[queueId] = updated;
        return Task.FromResult(updated);
    }

    public Task<IReadOnlyList<TriageQueueItem>> GetQueueItemsAsync(string? channel = null, string? status = null, CancellationToken ct = default)
    {
        var items = _queue.Values
            .Where(i => string.IsNullOrEmpty(channel) || i.Message.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
            .Where(i => string.IsNullOrEmpty(status) || i.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Assessment.UrgencyScore)
            .ToList();

        return Task.FromResult<IReadOnlyList<TriageQueueItem>>(items);
    }

    public Task<TriageAnalyticsSummary> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var items = _queue.Values.ToList();
        var total = items.Count;
        var hazards = items.Count(i => i.Assessment.RequiresImmediateEscalation);
        var sales = items.Count(i => i.Assessment.IntentCategory == "SalesInquiry");

        var channels = items
            .GroupBy(i => i.Message.Channel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());

        var analytics = new TriageAnalyticsSummary(
            TotalTriaged: total,
            EscalationHazardCount: hazards,
            SalesInquiryCount: sales,
            SlaCompliancePercent: 97.4,
            ChannelBreakdown: channels
        );

        return Task.FromResult(analytics);
    }

    private static TriageAssessment EvaluateMessage(InboundSocialMessage msg)
    {
        var lower = msg.MessageText.ToLowerInvariant();

        if (lower.Contains("lawyer") || lower.Contains("cancel") || lower.Contains("refund") || lower.Contains("broken") || lower.Contains("500 error") || lower.Contains("down") || lower.Contains("scam"))
        {
            return new TriageAssessment(
                MessageId: msg.MessageId,
                Sentiment: "SevereHazard",
                SentimentScore: 0.15,
                IntentCategory: "CancellationRisk",
                UrgencyScore: 95,
                RequiresImmediateEscalation: true,
                Confidence: 0.94
            );
        }

        if (lower.Contains("demo") || lower.Contains("pricing") || lower.Contains("buy") || lower.Contains("enterprise") || lower.Contains("quote"))
        {
            return new TriageAssessment(
                MessageId: msg.MessageId,
                Sentiment: "Positive",
                SentimentScore: 0.92,
                IntentCategory: "SalesInquiry",
                UrgencyScore: 80,
                RequiresImmediateEscalation: false,
                Confidence: 0.95
            );
        }

        return new TriageAssessment(
            MessageId: msg.MessageId,
            Sentiment: "Neutral",
            SentimentScore: 0.55,
            IntentCategory: "GeneralSupport",
            UrgencyScore: 40,
            RequiresImmediateEscalation: false,
            Confidence: 0.88
        );
    }

    private static string DetermineAssignedRole(TriageAssessment assessment)
    {
        if (assessment.RequiresImmediateEscalation) return "ExecutiveEscalationLead";
        if (assessment.IntentCategory == "SalesInquiry") return "EnterpriseAccountExecutive";
        return "SupportSpecialist";
    }

    private static string GenerateDraftResponse(InboundSocialMessage msg, TriageAssessment assessment)
    {
        if (assessment.RequiresImmediateEscalation)
        {
            return $"Hi {msg.SenderName}, we apologize for the urgency. A senior manager has been escalated to review your inquiry immediately. We are investigating right now.";
        }
        if (assessment.IntentCategory == "SalesInquiry")
        {
            return $"Hi {msg.SenderName}, thank you for your interest in Growth OS! Let us get you scheduled for an executive briefing and tailored platform demonstration.";
        }
        return $"Hi {msg.SenderName}, thank you for reaching out to Growth OS. We have received your message and an agent will follow up shortly.";
    }
}
