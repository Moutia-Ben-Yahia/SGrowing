using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Conversations.Unified;

public interface IUnifiedInboxEngine
{
    UnifiedThread IngestMessage(IngestMessageRequest request);
    List<UnifiedThread> GetThreads(string tenantId, TicketStatus? status = null);
    UnifiedThread? GetThread(string tenantId, Guid threadId);
    AiDeflectionResult EvaluateAiDeflection(string tenantId, Guid threadId);
    UnifiedThread EscalateTicket(EscalateTicketRequest request);
    UnifiedThread AppendAgentReply(string tenantId, Guid threadId, string agentId, string agentName, string content, ConversationChannel channel);
}

public class UnifiedInboxEngine : IUnifiedInboxEngine
{
    private readonly ConcurrentDictionary<Guid, UnifiedThread> _threads = new();
    private readonly Func<DateTime> _clock;

    public UnifiedInboxEngine() : this(null)
    {
    }

    public UnifiedInboxEngine(Func<DateTime>? clock)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public static CustomerSentiment AnalyzeSentiment(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return CustomerSentiment.Neutral;
        }

        var text = content.ToLowerInvariant();

        string[] frustratedKeywords =
        [
            "furious", "angry", "unacceptable", "terrible", "worst", "lawyer",
            "manager", "disaster", "horrible", "scam", "rip off", "ridiculous",
            "escalate", "fraud", "sue", "complaint", "outraged", "appalled"
        ];

        string[] negativeKeywords =
        [
            "bad", "issue", "problem", "error", "fail", "broken", "slow",
            "bug", "delayed", "not working", "cannot", "can't", "disappointed",
            "refund", "crash", "wrong", "missing", "trouble", "frustrated"
        ];

        string[] positiveKeywords =
        [
            "great", "thank", "thanks", "awesome", "good", "love", "helpful",
            "excellent", "perfect", "appreciate", "fantastic", "wonderful",
            "amazing", "resolved", "kudos"
        ];

        if (frustratedKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return CustomerSentiment.Frustrated;
        }

        var posCount = positiveKeywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        var negCount = negativeKeywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (posCount > negCount)
        {
            return CustomerSentiment.Positive;
        }

        if (negCount > posCount)
        {
            return CustomerSentiment.Negative;
        }

        return CustomerSentiment.Neutral;
    }

    public static TicketPriority DeterminePriorityFromSentiment(CustomerSentiment sentiment) => sentiment switch
    {
        CustomerSentiment.Frustrated => TicketPriority.Urgent,
        CustomerSentiment.Negative => TicketPriority.High,
        CustomerSentiment.Positive => TicketPriority.Low,
        _ => TicketPriority.Medium
    };

    public static TimeSpan GetSlaDuration(TicketPriority priority) => priority switch
    {
        TicketPriority.Urgent => TimeSpan.FromHours(1),
        TicketPriority.High => TimeSpan.FromHours(4),
        TicketPriority.Medium => TimeSpan.FromHours(12),
        TicketPriority.Low => TimeSpan.FromHours(24),
        _ => TimeSpan.FromHours(12)
    };

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private UnifiedThread RefreshSla(UnifiedThread thread)
    {
        var isBreached = thread.Status != TicketStatus.Resolved && _clock() > thread.SlaDueAtUtc;
        return thread with { SlaBreached = isBreached };
    }

    public UnifiedThread IngestMessage(IngestMessageRequest request)
    {
        var now = _clock();
        var sentiment = request.IsFromCustomer
            ? AnalyzeSentiment(request.Content)
            : CustomerSentiment.Neutral;

        var normPhone = NormalizePhone(request.CustomerPhone);
        var normEmail = request.CustomerEmail?.Trim() ?? string.Empty;

        // Omnichannel stitching: Match existing unresolved thread by email or normalized phone
        UnifiedThread? existing = _threads.Values
            .Where(t => t.TenantId == request.TenantId && t.Status != TicketStatus.Resolved)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefault(t =>
                (!string.IsNullOrEmpty(normEmail) && !string.IsNullOrEmpty(t.CustomerEmail) &&
                 string.Equals(t.CustomerEmail.Trim(), normEmail, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(normPhone) && !string.IsNullOrEmpty(t.CustomerPhone) &&
                 string.Equals(NormalizePhone(t.CustomerPhone), normPhone, StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            var msg = new CustomerMessage(
                Id: Guid.NewGuid(),
                ThreadId: existing.Id,
                TenantId: request.TenantId,
                Channel: request.Channel,
                SenderId: request.IsFromCustomer ? (normEmail.Length > 0 ? normEmail : normPhone) : "AGENT",
                SenderName: request.SenderName,
                Content: request.Content,
                Sentiment: sentiment,
                IsFromCustomer: request.IsFromCustomer,
                TimestampUtc: now
            );

            var updatedActiveChannels = new List<ConversationChannel>(existing.ActiveChannels);
            if (!updatedActiveChannels.Contains(request.Channel))
            {
                updatedActiveChannels.Add(request.Channel);
            }

            var updatedMessages = new List<CustomerMessage>(existing.Messages) { msg };

            var updatedEmail = string.IsNullOrEmpty(existing.CustomerEmail) ? normEmail : existing.CustomerEmail;
            var updatedPhone = string.IsNullOrEmpty(existing.CustomerPhone) ? request.CustomerPhone : existing.CustomerPhone;

            var newPriority = existing.Priority;
            if (sentiment == CustomerSentiment.Frustrated && existing.Priority != TicketPriority.Urgent)
            {
                newPriority = TicketPriority.Urgent;
            }

            var slaDue = newPriority != existing.Priority ? now.Add(GetSlaDuration(newPriority)) : existing.SlaDueAtUtc;

            var updatedThread = existing with
            {
                CustomerEmail = updatedEmail,
                CustomerPhone = updatedPhone,
                ActiveChannels = updatedActiveChannels,
                Messages = updatedMessages,
                Priority = newPriority,
                SlaDueAtUtc = slaDue,
                UpdatedAtUtc = now
            };

            updatedThread = RefreshSla(updatedThread);
            _threads[existing.Id] = updatedThread;
            return updatedThread;
        }
        else
        {
            var threadId = Guid.NewGuid();
            var priority = DeterminePriorityFromSentiment(sentiment);
            var slaDue = now.Add(GetSlaDuration(priority));

            var msg = new CustomerMessage(
                Id: Guid.NewGuid(),
                ThreadId: threadId,
                TenantId: request.TenantId,
                Channel: request.Channel,
                SenderId: request.IsFromCustomer ? (normEmail.Length > 0 ? normEmail : normPhone) : "AGENT",
                SenderName: request.SenderName,
                Content: request.Content,
                Sentiment: sentiment,
                IsFromCustomer: request.IsFromCustomer,
                TimestampUtc: now
            );

            var newThread = new UnifiedThread(
                Id: threadId,
                TenantId: request.TenantId,
                CustomerEmail: normEmail,
                CustomerPhone: request.CustomerPhone ?? string.Empty,
                ActiveChannels: [request.Channel],
                Messages: [msg],
                Status: TicketStatus.Open,
                Priority: priority,
                SlaDueAtUtc: slaDue,
                SlaBreached: false,
                AssignedAgentId: null,
                AiDeflected: false,
                UpdatedAtUtc: now
            );

            newThread = RefreshSla(newThread);
            _threads[threadId] = newThread;
            return newThread;
        }
    }

    public AiDeflectionResult EvaluateAiDeflection(string tenantId, Guid threadId)
    {
        if (!_threads.TryGetValue(threadId, out var thread) || thread.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Thread '{threadId}' not found for tenant '{tenantId}'.");
        }

        var lastCustomerMsg = thread.Messages.LastOrDefault(m => m.IsFromCustomer)
                              ?? thread.Messages.LastOrDefault();
        var content = lastCustomerMsg?.Content?.ToLowerInvariant() ?? string.Empty;

        string suggestedResponse;
        double confidence;
        bool canAutoSend;
        string sourceArticle;

        if (content.Contains("price") || content.Contains("pricing") || content.Contains("cost") || content.Contains("subscription") || content.Contains("plan") || content.Contains("how much"))
        {
            suggestedResponse = "You can view our available subscription tiers and volume pricing at /pricing. All annual plans include a 20% discount and a 14-day risk-free trial.";
            confidence = 0.95;
            canAutoSend = true;
            sourceArticle = "KB-101: Pricing and Subscription Tiers";
        }
        else if (content.Contains("password") || content.Contains("reset") || content.Contains("login") || content.Contains("can't sign in") || content.Contains("forgot password") || content.Contains("2fa"))
        {
            suggestedResponse = "To reset your password, please go to the login portal at /auth/login and click 'Forgot Password'. A secure reset link will be dispatched to your registered email immediately.";
            confidence = 0.94;
            canAutoSend = true;
            sourceArticle = "KB-102: Account Security and Password Recovery";
        }
        else if (content.Contains("refund") || content.Contains("cancel subscription") || content.Contains("cancellation") || content.Contains("money back") || content.Contains("return"))
        {
            suggestedResponse = "Our policy allows full refunds within 30 days of purchase. You can cancel your subscription or submit a refund request under Settings > Invoices & Billing.";
            confidence = 0.91;
            canAutoSend = true;
            sourceArticle = "KB-103: Cancellation & Refund Policy";
        }
        else if (content.Contains("shipping") || content.Contains("delivery") || content.Contains("track order") || content.Contains("courier") || content.Contains("package") || content.Contains("tracking number"))
        {
            suggestedResponse = "Standard orders are fulfilled within 24 hours. You can inspect live courier milestones and tracking telemetry in your dashboard under Orders > Tracking.";
            confidence = 0.92;
            canAutoSend = true;
            sourceArticle = "KB-104: Logistics & Order Tracking";
        }
        else if (content.Contains("api") || content.Contains("webhook") || content.Contains("sdk") || content.Contains("developer") || content.Contains("endpoint"))
        {
            suggestedResponse = "Comprehensive developer documentation, OpenAPI specifications, and SDK installation guides are available at /docs/api. API tokens can be minted in Developer Settings.";
            confidence = 0.96;
            canAutoSend = true;
            sourceArticle = "KB-105: Developer API & Webhook Integration";
        }
        else
        {
            suggestedResponse = "Thank you for reaching out to SGrowing customer support. Could you please provide additional details so our team can assist you?";
            confidence = 0.45;
            canAutoSend = false;
            sourceArticle = "KB-001: General Support FAQ";
        }

        if (canAutoSend)
        {
            var updated = thread with { AiDeflected = true, UpdatedAtUtc = _clock() };
            _threads[threadId] = RefreshSla(updated);
        }

        return new AiDeflectionResult(
            ThreadId: threadId,
            SuggestedResponse: suggestedResponse,
            ConfidenceScore: confidence,
            CanAutoSend: canAutoSend,
            SourceArticle: sourceArticle
        );
    }

    public UnifiedThread EscalateTicket(EscalateTicketRequest request)
    {
        if (!_threads.TryGetValue(request.ThreadId, out var thread) || thread.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Thread '{request.ThreadId}' not found for tenant '{request.TenantId}'.");
        }

        var now = _clock();
        var newSlaDue = now.Add(GetSlaDuration(request.NewPriority));

        var escalationNote = new CustomerMessage(
            Id: Guid.NewGuid(),
            ThreadId: thread.Id,
            TenantId: request.TenantId,
            Channel: thread.ActiveChannels.FirstOrDefault(),
            SenderId: "SYSTEM",
            SenderName: "Escalation Engine",
            Content: $"Ticket escalated to {request.NewPriority}. Reason: {request.Reason}",
            Sentiment: CustomerSentiment.Neutral,
            IsFromCustomer: false,
            TimestampUtc: now
        );

        var updatedMessages = new List<CustomerMessage>(thread.Messages) { escalationNote };

        var updatedThread = thread with
        {
            Status = TicketStatus.Escalated,
            Priority = request.NewPriority,
            SlaDueAtUtc = newSlaDue,
            Messages = updatedMessages,
            UpdatedAtUtc = now
        };

        updatedThread = RefreshSla(updatedThread);
        _threads[thread.Id] = updatedThread;
        return updatedThread;
    }

    public UnifiedThread AppendAgentReply(string tenantId, Guid threadId, string agentId, string agentName, string content, ConversationChannel channel)
    {
        if (!_threads.TryGetValue(threadId, out var thread) || thread.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Thread '{threadId}' not found for tenant '{tenantId}'.");
        }

        var now = _clock();
        var reply = new CustomerMessage(
            Id: Guid.NewGuid(),
            ThreadId: threadId,
            TenantId: tenantId,
            Channel: channel,
            SenderId: agentId,
            SenderName: agentName,
            Content: content,
            Sentiment: CustomerSentiment.Positive,
            IsFromCustomer: false,
            TimestampUtc: now
        );

        var updatedChannels = new List<ConversationChannel>(thread.ActiveChannels);
        if (!updatedChannels.Contains(channel))
        {
            updatedChannels.Add(channel);
        }

        var updatedMessages = new List<CustomerMessage>(thread.Messages) { reply };

        var updatedThread = thread with
        {
            AssignedAgentId = agentId,
            Status = TicketStatus.PendingCustomer,
            ActiveChannels = updatedChannels,
            Messages = updatedMessages,
            UpdatedAtUtc = now
        };

        updatedThread = RefreshSla(updatedThread);
        _threads[threadId] = updatedThread;
        return updatedThread;
    }

    public UnifiedThread? GetThread(string tenantId, Guid threadId)
    {
        if (_threads.TryGetValue(threadId, out var thread) && thread.TenantId == tenantId)
        {
            return RefreshSla(thread);
        }

        return null;
    }

    public List<UnifiedThread> GetThreads(string tenantId, TicketStatus? status = null)
    {
        var query = _threads.Values
            .Where(t => t.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return query
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Select(RefreshSla)
            .ToList();
    }
}
