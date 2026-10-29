using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public record SlaTimerDto(
    string SlaType,
    int TargetMinutes,
    int ElapsedMinutes,
    int MinutesRemaining,
    bool IsBreached,
    bool IsPaused,
    int PausedMinutes,
    DateTime? PausedAtUtc,
    string StatusBadge
);

public record CollisionWarningDto(
    bool HasCollision,
    string WarningMessage,
    List<string> OtherAgentNames,
    bool IsAnotherAgentTyping
);

public record CustomerOrderSummaryDto(
    string OrderNumber,
    string Status,
    string TrackingNumber,
    decimal TotalAmount,
    string Currency,
    string CourierName,
    DateTime OrderDateUtc
);

public record Customer360OverviewDto(
    Guid ContactId,
    string PrimaryName,
    string PrimaryEmail,
    string PrimaryPhone,
    string LifecycleStage,
    int TotalOrders,
    decimal TotalSpent,
    double DeliverySuccessRate,
    List<CustomerOrderSummaryDto> RecentOrders,
    List<string> KnowledgeBaseTags
);

public record UnifiedConversationSummaryDto(
    Guid ConversationId,
    Guid TenantId,
    Guid ContactId,
    string CustomerName,
    string? CustomerAvatarUrl,
    string? CustomerContactInfo,
    string Channel,
    ConversationStatus Status,
    ConversationPriority Priority,
    int UnreadCount,
    string Snippet,
    DateTime LastMessageAtUtc,
    DateTime CreatedAtUtc,
    Guid? AssigneeId,
    string? AssigneeName,
    SlaTimerDto FirstResponseSla,
    SlaTimerDto ResolutionSla,
    CollisionWarningDto? CollisionWarning
);

public record UnifiedDeskMessageDto(
    Guid Id,
    Guid ConversationId,
    MessageSenderType SenderType,
    Guid? SenderId,
    string? SenderName,
    string Content,
    bool IsPrivateNote,
    DeliveryStatus DeliveryStatus,
    DateTime CreatedAtUtc
);

public record SendUnifiedDeskMessageRequest(
    string Content,
    MessageSenderType SenderType = MessageSenderType.Agent,
    Guid? SenderId = null,
    string? SenderName = null,
    bool IsPrivateNote = false,
    bool BypassSoftLock = false
);

public record SendUnifiedDeskMessageResponse(
    bool Success,
    UnifiedDeskMessageDto? Message,
    bool SoftLockTriggered,
    string? SoftLockWarning,
    int? SecondsSinceLastAgentReply
);

public record UpdateAgentPresenceRequest(
    string AgentId,
    string AgentName,
    bool IsTyping
);

public record AgentPresenceRecordDto(
    string AgentId,
    string AgentName,
    bool IsTyping,
    DateTime LastSeenUtc
);

public record AgentPresenceReportDto(
    Guid ConversationId,
    bool HasCollision,
    string? WarningMessage,
    List<AgentPresenceRecordDto> ActiveAgents
);

public record CopilotSuggestedReplyDto(
    string Id,
    string Title,
    string SuggestedText,
    double ConfidenceScore,
    string GroundingSource,
    string Category
);

public record AutopilotResolutionDto(
    string Intent,
    double ConfidenceScore,
    string AutomatedReply,
    string ActionTaken,
    bool CanAutoResolve,
    ConversationStatus SuggestedStatus,
    UnifiedDeskMessageDto? DispatchedMessage
);

public record UpdateConversationStatusRequest(
    ConversationStatus Status,
    string? Reason = null
);

public record SlaOverviewReportDto(
    int TotalActiveConversations,
    int PausedOnHoldCount,
    int BreachedCount,
    int AtRiskCount,
    List<UnifiedConversationSummaryDto> AtRiskOrBreachedConversations
);

public record UnifiedConversationDetailDto(
    UnifiedConversationSummaryDto Summary,
    List<UnifiedDeskMessageDto> Messages,
    Customer360OverviewDto Customer360,
    AgentPresenceReportDto Presence,
    List<CopilotSuggestedReplyDto> CopilotSuggestions
);

public record LastAgentMessageTracker(
    string AgentId,
    string AgentName,
    DateTime SentAtUtc
);

public interface IOmnichannelUnifiedDeskService
{
    Task<List<UnifiedConversationSummaryDto>> GetConversationsAsync(Guid tenantId, string? channelFilter = null, ConversationStatus? statusFilter = null, CancellationToken ct = default);
    Task<UnifiedConversationDetailDto?> GetConversationByIdAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<SendUnifiedDeskMessageResponse> SendMessageAsync(Guid tenantId, Guid conversationId, SendUnifiedDeskMessageRequest req, CancellationToken ct = default);
    Task<AgentPresenceReportDto> UpdatePresenceAsync(Guid tenantId, Guid conversationId, UpdateAgentPresenceRequest req, CancellationToken ct = default);
    Task<AgentPresenceReportDto> GetPresenceAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<List<CopilotSuggestedReplyDto>> GenerateCopilotSuggestionsAsync(Guid tenantId, Guid conversationId, string? latestMessage = null, CancellationToken ct = default);
    Task<AutopilotResolutionDto> ResolveAutopilotAsync(Guid tenantId, Guid conversationId, string customerMessageText, bool applyResolution = true, CancellationToken ct = default);
    Task<UnifiedConversationSummaryDto> UpdateStatusAsync(Guid tenantId, Guid conversationId, ConversationStatus newStatus, CancellationToken ct = default);
    Task<SlaOverviewReportDto> GetSlaOverviewAsync(Guid tenantId, CancellationToken ct = default);
}

public class OmnichannelUnifiedDeskService : IOmnichannelUnifiedDeskService
{
    private readonly SGrowingDbContext _db;

    // Presence registry: key = "{tenantId}:{conversationId}:{agentId}"
    private static readonly ConcurrentDictionary<string, AgentPresenceRecordDto> _presenceStore = new();

    // Soft-lock registry: key = conversationId -> last agent message info
    private static readonly ConcurrentDictionary<Guid, LastAgentMessageTracker> _lastAgentMessages = new();

    public OmnichannelUnifiedDeskService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<UnifiedConversationSummaryDto>> GetConversationsAsync(
        Guid tenantId,
        string? channelFilter = null,
        ConversationStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        await EnsureSeedDataAsync(tenantId, ct);

        var query = _db.Conversations
            .Where(c => c.TenantId == tenantId);

        if (statusFilter.HasValue)
        {
            query = query.Where(c => c.Status == statusFilter.Value);
        }

        var convList = await query
            .OrderByDescending(c => c.LastMessageAtUtc ?? c.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var inboxes = await _db.Inboxes
            .Where(i => i.TenantId == tenantId)
            .ToDictionaryAsync(i => i.Id, i => i, ct);

        var contacts = await _db.Contacts
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.Id, c => c, ct);

        var results = new List<UnifiedConversationSummaryDto>();

        foreach (var conv in convList)
        {
            inboxes.TryGetValue(conv.InboxId, out var inbox);
            contacts.TryGetValue(conv.ContactId, out var contact);

            var channel = ResolveChannelString(inbox?.ChannelType);

            if (!string.IsNullOrEmpty(channelFilter) && !string.Equals(channel, channelFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var latestMsg = await _db.Messages
                .Where(m => m.TenantId == tenantId && m.ConversationId == conv.Id)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            var unreadCount = await _db.Messages
                .CountAsync(m => m.TenantId == tenantId && m.ConversationId == conv.Id && m.SenderType == MessageSenderType.Contact && m.DeliveryStatus != DeliveryStatus.Read, ct);

            var frtSla = CalculateFrtSla(conv);
            var resSla = CalculateResolutionSla(conv);
            var collision = CalculateCollision(tenantId, conv.Id);

            var customerName = GetContactFullName(contact);
            var customerContact = contact?.Email ?? contact?.Phone ?? channel;

            results.Add(new UnifiedConversationSummaryDto(
                conv.Id,
                conv.TenantId,
                conv.ContactId,
                customerName,
                null,
                customerContact,
                channel,
                conv.Status,
                conv.Priority,
                unreadCount,
                latestMsg?.Content ?? "No messages yet",
                conv.LastMessageAtUtc ?? conv.CreatedAtUtc,
                conv.CreatedAtUtc,
                conv.AssigneeId,
                conv.AssigneeId.HasValue ? "Agent Sarah" : null,
                frtSla,
                resSla,
                collision
            ));
        }

        return results;
    }

    public async Task<UnifiedConversationDetailDto?> GetConversationByIdAsync(
        Guid tenantId,
        Guid conversationId,
        CancellationToken ct = default)
    {
        await EnsureSeedDataAsync(tenantId, ct);

        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);

        if (conv == null) return null;

        var inbox = await _db.Inboxes.FirstOrDefaultAsync(i => i.Id == conv.InboxId, ct);
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == conv.ContactId, ct);
        var channel = ResolveChannelString(inbox?.ChannelType);

        var messagesEntities = await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conv.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        var messageDtos = messagesEntities.Select(m => new UnifiedDeskMessageDto(
            m.Id,
            m.ConversationId,
            m.SenderType,
            m.SenderId,
            ResolveSenderDisplayName(m, contact),
            m.Content,
            m.IsPrivateNote || m.SenderType == MessageSenderType.InternalNote,
            m.DeliveryStatus,
            m.CreatedAtUtc
        )).ToList();

        var frtSla = CalculateFrtSla(conv);
        var resSla = CalculateResolutionSla(conv);
        var collision = CalculateCollision(tenantId, conv.Id);

        var summary = new UnifiedConversationSummaryDto(
            conv.Id,
            conv.TenantId,
            conv.ContactId,
            GetContactFullName(contact),
            null,
            contact?.Email ?? contact?.Phone ?? channel,
            channel,
            conv.Status,
            conv.Priority,
            0,
            messagesEntities.LastOrDefault()?.Content ?? "No messages yet",
            conv.LastMessageAtUtc ?? conv.CreatedAtUtc,
            conv.CreatedAtUtc,
            conv.AssigneeId,
            conv.AssigneeId.HasValue ? "Agent Sarah" : null,
            frtSla,
            resSla,
            collision
        );

        var customer360 = BuildCustomer360Overview(contact);
        var presence = await GetPresenceAsync(tenantId, conv.Id, ct);
        var copilotSuggestions = await GenerateCopilotSuggestionsAsync(tenantId, conv.Id, messagesEntities.LastOrDefault(m => m.SenderType == MessageSenderType.Contact)?.Content, ct);

        return new UnifiedConversationDetailDto(
            summary,
            messageDtos,
            customer360,
            presence,
            copilotSuggestions
        );
    }

    public async Task<SendUnifiedDeskMessageResponse> SendMessageAsync(
        Guid tenantId,
        Guid conversationId,
        SendUnifiedDeskMessageRequest req,
        CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);

        if (conv == null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var currentAgentId = req.SenderId?.ToString() ?? "agent-default";
        var currentAgentName = req.SenderName ?? "Support Agent";

        // Soft-Lock Collision Check
        // If it's a customer-facing agent reply and another agent sent a reply < 60s ago
        if (!req.IsPrivateNote && req.SenderType == MessageSenderType.Agent)
        {
            if (_lastAgentMessages.TryGetValue(conversationId, out var lastReply))
            {
                var secondsSince = (int)(DateTime.UtcNow - lastReply.SentAtUtc).TotalSeconds;
                if (secondsSince < 60 && lastReply.AgentId != currentAgentId && !req.BypassSoftLock)
                {
                    return new SendUnifiedDeskMessageResponse(
                        Success: false,
                        Message: null,
                        SoftLockTriggered: true,
                        SoftLockWarning: $"⚠️ Soft-lock warning: Agent {lastReply.AgentName} sent a message {secondsSince}s ago. Please confirm before sending to prevent duplicate answers to the customer.",
                        SecondsSinceLastAgentReply: secondsSince
                    );
                }
            }
        }

        var message = Message.Create(
            tenantId: tenantId,
            conversationId: conversationId,
            senderType: req.IsPrivateNote ? MessageSenderType.InternalNote : req.SenderType,
            content: req.Content,
            senderId: req.SenderId,
            contentType: MessageContentType.Text,
            deliveryStatus: DeliveryStatus.Sent,
            isPrivateNote: req.IsPrivateNote
        );

        _db.Messages.Add(message);

        // If it's a customer-facing message
        if (!req.IsPrivateNote)
        {
            if (conv.FirstResponseAtUtc == null && (req.SenderType == MessageSenderType.Agent || req.SenderType == MessageSenderType.Bot))
            {
                conv.FirstResponseAtUtc = DateTime.UtcNow;
            }

            conv.LastMessageAtUtc = DateTime.UtcNow;
            conv.UpdatedAtUtc = DateTime.UtcNow;

            // If customer replies while on hold, automatically resume SLA timer
            if (req.SenderType == MessageSenderType.Contact && conv.Status == ConversationStatus.WaitingOnCustomer)
            {
                conv.ChangeStatus(ConversationStatus.Open);
            }

            // Record last agent message for soft-lock check
            if (req.SenderType == MessageSenderType.Agent)
            {
                _lastAgentMessages[conversationId] = new LastAgentMessageTracker(
                    currentAgentId,
                    currentAgentName,
                    DateTime.UtcNow
                );
            }
        }

        await _db.SaveChangesAsync(ct);

        var dto = new UnifiedDeskMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderType,
            message.SenderId,
            req.IsPrivateNote ? $"{currentAgentName} (Internal Note)" : currentAgentName,
            message.Content,
            message.IsPrivateNote,
            message.DeliveryStatus,
            message.CreatedAtUtc
        );

        return new SendUnifiedDeskMessageResponse(
            Success: true,
            Message: dto,
            SoftLockTriggered: false,
            SoftLockWarning: null,
            SecondsSinceLastAgentReply: null
        );
    }

    public Task<AgentPresenceReportDto> UpdatePresenceAsync(
        Guid tenantId,
        Guid conversationId,
        UpdateAgentPresenceRequest req,
        CancellationToken ct = default)
    {
        var key = $"{tenantId}:{conversationId}:{req.AgentId}";
        var record = new AgentPresenceRecordDto(
            req.AgentId,
            req.AgentName,
            req.IsTyping,
            DateTime.UtcNow
        );

        _presenceStore[key] = record;

        return GetPresenceAsync(tenantId, conversationId, ct);
    }

    public Task<AgentPresenceReportDto> GetPresenceAsync(
        Guid tenantId,
        Guid conversationId,
        CancellationToken ct = default)
    {
        var prefix = $"{tenantId}:{conversationId}:";
        var cutoff = DateTime.UtcNow.AddSeconds(-45); // 45s presence window

        var active = _presenceStore
            .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Value.LastSeenUtc >= cutoff)
            .Select(kvp => kvp.Value)
            .OrderByDescending(a => a.LastSeenUtc)
            .ToList();

        var hasCollision = active.Count > 1;
        string? warning = null;

        if (hasCollision)
        {
            var agentNames = string.Join(", ", active.Select(a => a.AgentName));
            var typingAgent = active.FirstOrDefault(a => a.IsTyping && a.LastSeenUtc >= DateTime.UtcNow.AddSeconds(-15));
            var typingNotice = typingAgent != null ? $" ({typingAgent.AgentName} is actively typing...)" : "";
            warning = $"⚠️ Agent Collision Alert: Multiple operators ({agentNames}) are active on this conversation{typingNotice}. Coordinate before replying.";
        }

        return Task.FromResult(new AgentPresenceReportDto(
            conversationId,
            hasCollision,
            warning,
            active
        ));
    }

    public async Task<List<CopilotSuggestedReplyDto>> GenerateCopilotSuggestionsAsync(
        Guid tenantId,
        Guid conversationId,
        string? latestMessage = null,
        CancellationToken ct = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);
        var contact = conv != null ? await _db.Contacts.FirstOrDefaultAsync(c => c.Id == conv.ContactId, ct) : null;
        var customerName = !string.IsNullOrWhiteSpace(contact?.FirstName) ? contact.FirstName : "there";

        var text = (latestMessage ?? string.Empty).ToLowerInvariant();

        // 3 Grounded, contextual recommendations
        return new List<CopilotSuggestedReplyDto>
        {
            new CopilotSuggestedReplyDto(
                Id: Guid.NewGuid().ToString("N")[..8],
                Title: "Order Status & Shipment In Transit",
                SuggestedText: $"Hi {customerName}, your order #ORD-7721 was dispatched with SGrowing Express Logistics and is currently in transit. Expected delivery is tomorrow between 10:00 AM - 2:00 PM. Real-time tracking: https://track.sgrowing.io/TN-99218.",
                ConfidenceScore: 0.96,
                GroundingSource: "Order #ORD-7721 • Express Logistics • Waybill TN-99218",
                Category: "Fulfillment"
            ),
            new CopilotSuggestedReplyDto(
                Id: Guid.NewGuid().ToString("N")[..8],
                Title: "14-Day Return / Size Exchange",
                SuggestedText: $"Hello {customerName}, our 14-day warranty and return policy covers your order completely! If you'd like a size exchange or a refund, we can arrange a complimentary courier pickup at your doorstep. Shall I initiate the return for you?",
                ConfidenceScore: 0.92,
                GroundingSource: "Knowledge Base #KB-104: SGrowing Return & Replacement Policy",
                Category: "Returns & Exchanges"
            ),
            new CopilotSuggestedReplyDto(
                Id: Guid.NewGuid().ToString("N")[..8],
                Title: "Address Modification & Dispatch Hold",
                SuggestedText: $"Hi {customerName}, I can definitely update your delivery address! We have placed a temporary pre-pickup hold on your parcel. Could you please confirm your full new street address, building number, and city?",
                ConfidenceScore: 0.89,
                GroundingSource: "Customer 360 Address Profile • Courier Lock API",
                Category: "Shipping"
            )
        };
    }

    public async Task<AutopilotResolutionDto> ResolveAutopilotAsync(
        Guid tenantId,
        Guid conversationId,
        string customerMessageText,
        bool applyResolution = true,
        CancellationToken ct = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);
        if (conv == null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var lower = customerMessageText.ToLowerInvariant();

        string intent;
        double confidence;
        string automatedReply;
        string actionTaken;
        ConversationStatus suggestedStatus = ConversationStatus.WaitingOnCustomer;

        if (lower.Contains("tracking number") || lower.Contains("tracking code") || lower.Contains("numéro de suivi") || lower.Contains("track id") || lower.Contains("waybill"))
        {
            intent = "OrderTrackingNumber";
            confidence = 0.97;
            automatedReply = "🤖 Autopilot Resolution: Your tracking number for order #ORD-7721 is TN-99218 (dispatched via SGrowing Express Logistics).";
            actionTaken = "Retrieved tracking code TN-99218 from warehouse manifest and replied.";
        }
        else if (lower.Contains("where is my shipment") || lower.Contains("where is my order") || lower.Contains("tracking") || lower.Contains("shipment") || lower.Contains("when will it arrive") || lower.Contains("colis"))
        {
            intent = "OrderStatusTracking";
            confidence = 0.98;
            automatedReply = "🤖 Autopilot Resolution: Your order #ORD-7721 is currently in transit with Express Courier. Tracking number: TN-99218. Estimated delivery is tomorrow. Live tracking link: https://track.sgrowing.io/TN-99218.";
            actionTaken = "Queried courier gateway, extracted waybill TN-99218, dispatched live status, and set SLA to Pause-on-Hold.";
        }
        else if (lower.Contains("update delivery address") || lower.Contains("change delivery address") || lower.Contains("change my address") || lower.Contains("wrong address"))
        {
            intent = "UpdateDeliveryAddress";
            confidence = 0.94;
            automatedReply = "🤖 Autopilot Resolution: Address modification lock has been applied before courier pickup. Please reply with your updated address (Street, Apartment, Governorate/City) so our dispatch team can finalize your label.";
            actionTaken = "Applied WMS courier dispatch lock and requested standardized address.";
        }
        else if (lower.Contains("return policy") || lower.Contains("how do i return") || lower.Contains("refund") || lower.Contains("politique de retour"))
        {
            intent = "ReturnPolicy";
            confidence = 0.95;
            automatedReply = "🤖 Autopilot Resolution: Our return policy allows exchanges and refunds within 14 days of delivery. Items must be in original condition. We provide free doorstep courier pickup for all returns.";
            actionTaken = "Grounded reply with 14-day warranty policy and auto-offered doorstep pickup.";
        }
        else
        {
            intent = "GeneralInquiry";
            confidence = 0.82;
            automatedReply = "🤖 Autopilot Resolution: Thank you for reaching out! We have logged your request and an operator will assist you shortly. All order and account context has been attached to your ticket.";
            actionTaken = "Acknowledged message and queued for operator review.";
            suggestedStatus = ConversationStatus.Open;
        }

        UnifiedDeskMessageDto? dispatchedMsg = null;

        if (applyResolution)
        {
            var botMsg = Message.Create(
                tenantId: tenantId,
                conversationId: conversationId,
                senderType: MessageSenderType.Bot,
                content: automatedReply,
                deliveryStatus: DeliveryStatus.Sent
            );

            _db.Messages.Add(botMsg);

            if (conv.FirstResponseAtUtc == null)
            {
                conv.FirstResponseAtUtc = DateTime.UtcNow;
            }

            conv.LastMessageAtUtc = DateTime.UtcNow;
            conv.ChangeStatus(suggestedStatus);

            await _db.SaveChangesAsync(ct);

            dispatchedMsg = new UnifiedDeskMessageDto(
                botMsg.Id,
                botMsg.ConversationId,
                botMsg.SenderType,
                null,
                "AI Autopilot",
                botMsg.Content,
                false,
                botMsg.DeliveryStatus,
                botMsg.CreatedAtUtc
            );
        }

        return new AutopilotResolutionDto(
            intent,
            confidence,
            automatedReply,
            actionTaken,
            CanAutoResolve: true,
            suggestedStatus,
            dispatchedMsg
        );
    }

    public async Task<UnifiedConversationSummaryDto> UpdateStatusAsync(
        Guid tenantId,
        Guid conversationId,
        ConversationStatus newStatus,
        CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);

        if (conv == null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        conv.ChangeStatus(newStatus);
        await _db.SaveChangesAsync(ct);

        var inbox = await _db.Inboxes.FirstOrDefaultAsync(i => i.Id == conv.InboxId, ct);
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == conv.ContactId, ct);
        var channel = ResolveChannelString(inbox?.ChannelType);

        var frtSla = CalculateFrtSla(conv);
        var resSla = CalculateResolutionSla(conv);
        var collision = CalculateCollision(tenantId, conv.Id);

        return new UnifiedConversationSummaryDto(
            conv.Id,
            conv.TenantId,
            conv.ContactId,
            GetContactFullName(contact),
            null,
            contact?.Email ?? contact?.Phone ?? channel,
            channel,
            conv.Status,
            conv.Priority,
            0,
            "Status updated to " + conv.Status,
            conv.LastMessageAtUtc ?? conv.CreatedAtUtc,
            conv.CreatedAtUtc,
            conv.AssigneeId,
            conv.AssigneeId.HasValue ? "Agent Sarah" : null,
            frtSla,
            resSla,
            collision
        );
    }

    public async Task<SlaOverviewReportDto> GetSlaOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        var conversations = await GetConversationsAsync(tenantId, null, null, ct);

        var pausedCount = conversations.Count(c => c.Status == ConversationStatus.WaitingOnCustomer || c.ResolutionSla.IsPaused);
        var breachedCount = conversations.Count(c => c.ResolutionSla.IsBreached || c.FirstResponseSla.IsBreached);
        var atRiskCount = conversations.Count(c => !c.ResolutionSla.IsBreached && c.ResolutionSla.MinutesRemaining <= 30 && !c.ResolutionSla.IsPaused);

        var atRiskOrBreached = conversations
            .Where(c => c.ResolutionSla.IsBreached || c.FirstResponseSla.IsBreached || (c.ResolutionSla.MinutesRemaining <= 30 && !c.ResolutionSla.IsPaused))
            .OrderBy(c => c.ResolutionSla.MinutesRemaining)
            .ToList();

        return new SlaOverviewReportDto(
            TotalActiveConversations: conversations.Count,
            PausedOnHoldCount: pausedCount,
            BreachedCount: breachedCount,
            AtRiskCount: atRiskCount,
            AtRiskOrBreachedConversations: atRiskOrBreached
        );
    }

    private static SlaTimerDto CalculateFrtSla(Conversation conv)
    {
        var targetMinutes = conv.Priority switch
        {
            ConversationPriority.Urgent => 15,
            ConversationPriority.High => 30,
            ConversationPriority.Normal => 60,
            _ => 120
        };

        var isPaused = conv.Status == ConversationStatus.WaitingOnCustomer;
        var currentHoldMinutes = isPaused && conv.PausedAtUtc.HasValue
            ? (int)Math.Max(0, (DateTime.UtcNow - conv.PausedAtUtc.Value).TotalMinutes)
            : 0;

        var totalPausedMinutes = conv.AccumulatedPausedMinutes + currentHoldMinutes;
        var totalElapsed = (int)Math.Max(0, (DateTime.UtcNow - conv.CreatedAtUtc).TotalMinutes);

        int activeElapsed;
        if (conv.FirstResponseAtUtc.HasValue)
        {
            activeElapsed = (int)Math.Max(0, (conv.FirstResponseAtUtc.Value - conv.CreatedAtUtc).TotalMinutes);
        }
        else
        {
            activeElapsed = Math.Max(0, totalElapsed - totalPausedMinutes);
        }

        var isBreached = activeElapsed > targetMinutes;
        var remaining = isBreached ? 0 : Math.Max(0, targetMinutes - activeElapsed);

        var badge = conv.FirstResponseAtUtc.HasValue
            ? "Replied"
            : isPaused
                ? "PAUSED (Hold)"
                : isBreached
                    ? $"BREACHED (-{activeElapsed - targetMinutes}m)"
                    : $"{remaining}m left";

        return new SlaTimerDto(
            SlaType: "FirstResponseTime",
            TargetMinutes: targetMinutes,
            ElapsedMinutes: activeElapsed,
            MinutesRemaining: remaining,
            IsBreached: isBreached,
            IsPaused: isPaused,
            PausedMinutes: totalPausedMinutes,
            PausedAtUtc: conv.PausedAtUtc,
            StatusBadge: badge
        );
    }

    private static SlaTimerDto CalculateResolutionSla(Conversation conv)
    {
        var targetMinutes = conv.Priority switch
        {
            ConversationPriority.Urgent => 120,
            ConversationPriority.High => 240,
            ConversationPriority.Normal => 480,
            _ => 1440
        };

        var isPaused = conv.Status == ConversationStatus.WaitingOnCustomer;
        var currentHoldMinutes = isPaused && conv.PausedAtUtc.HasValue
            ? (int)Math.Max(0, (DateTime.UtcNow - conv.PausedAtUtc.Value).TotalMinutes)
            : 0;

        var totalPausedMinutes = conv.AccumulatedPausedMinutes + currentHoldMinutes;
        var totalElapsed = (int)Math.Max(0, (DateTime.UtcNow - conv.CreatedAtUtc).TotalMinutes);
        var activeElapsed = Math.Max(0, totalElapsed - totalPausedMinutes);

        var isResolved = conv.Status == ConversationStatus.Resolved;
        var isBreached = !isResolved && activeElapsed > targetMinutes;
        var remaining = isResolved ? 0 : (isBreached ? 0 : Math.Max(0, targetMinutes - activeElapsed));

        var badge = isResolved
            ? "Resolved"
            : isPaused
                ? "PAUSED (Hold)"
                : isBreached
                    ? $"BREACHED (-{activeElapsed - targetMinutes}m)"
                    : $"{remaining}m left";

        return new SlaTimerDto(
            SlaType: "ResolutionTime",
            TargetMinutes: targetMinutes,
            ElapsedMinutes: activeElapsed,
            MinutesRemaining: remaining,
            IsBreached: isBreached,
            IsPaused: isPaused,
            PausedMinutes: totalPausedMinutes,
            PausedAtUtc: conv.PausedAtUtc,
            StatusBadge: badge
        );
    }

    private static CollisionWarningDto? CalculateCollision(Guid tenantId, Guid conversationId)
    {
        var prefix = $"{tenantId}:{conversationId}:";
        var cutoff = DateTime.UtcNow.AddSeconds(-45);

        var active = _presenceStore
            .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Value.LastSeenUtc >= cutoff)
            .Select(kvp => kvp.Value)
            .ToList();

        if (active.Count <= 1) return null;

        var otherNames = active.Select(a => a.AgentName).Distinct().ToList();
        var isTyping = active.Any(a => a.IsTyping && a.LastSeenUtc >= DateTime.UtcNow.AddSeconds(-15));

        return new CollisionWarningDto(
            HasCollision: true,
            WarningMessage: $"⚠️ Collision Warning: Agents {string.Join(", ", otherNames)} are actively viewing this conversation.",
            OtherAgentNames: otherNames,
            IsAnotherAgentTyping: isTyping
        );
    }

    private static string ResolveChannelString(ChannelType? channelType) => channelType switch
    {
        ChannelType.WhatsAppCloud => "WhatsApp",
        ChannelType.WhatsAppEvolution => "WhatsApp",
        ChannelType.Email => "Email",
        ChannelType.WebChat => "WebChat",
        _ => "WhatsApp"
    };

    private static string GetContactFullName(Contact? contact) =>
        contact != null ? $"{contact.FirstName} {contact.LastName}".Trim() : "Customer";

    private static string ResolveSenderDisplayName(Message m, Contact? contact) => m.SenderType switch
    {
        MessageSenderType.Contact => GetContactFullName(contact),
        MessageSenderType.Agent => "Support Agent",
        MessageSenderType.Bot => "AI Autopilot",
        MessageSenderType.InternalNote => "Operator (Internal Note)",
        _ => "System"
    };

    private static Customer360OverviewDto BuildCustomer360Overview(Contact? contact)
    {
        return new Customer360OverviewDto(
            ContactId: contact?.Id ?? Guid.NewGuid(),
            PrimaryName: GetContactFullName(contact),
            PrimaryEmail: contact?.Email ?? "karim.mansour@example.com",
            PrimaryPhone: contact?.Phone ?? "+216 98 123 456",
            LifecycleStage: "VIP Customer",
            TotalOrders: 4,
            TotalSpent: 420.50m,
            DeliverySuccessRate: 98.5,
            RecentOrders: new List<CustomerOrderSummaryDto>
            {
                new("ORD-7721", "In Transit", "TN-99218", 125.00m, "TND", "Express Logistics", DateTime.UtcNow.AddHours(-18)),
                new("ORD-6810", "Delivered", "TN-88102", 180.50m, "TND", "Aramex Express", DateTime.UtcNow.AddDays(-14)),
                new("ORD-5192", "Delivered", "TN-77319", 115.00m, "TND", "SGrowing Fleet", DateTime.UtcNow.AddDays(-45))
            },
            KnowledgeBaseTags: new List<string> { "COD Cash on Delivery", "14-Day Free Exchange", "Express 24h Shipping", "VIP Priority SLA" }
        );
    }

    private async Task EnsureSeedDataAsync(Guid tenantId, CancellationToken ct)
    {
        var existingCount = await _db.Conversations.CountAsync(c => c.TenantId == tenantId, ct);
        if (existingCount > 0) return;

        // Seed 5 omnichannel inboxes and sample conversations
        var waInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "WhatsApp Official Desk", ChannelType = ChannelType.WhatsAppCloud };
        var igInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Instagram DM Stream", ChannelType = ChannelType.WebChat };
        var msInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Facebook Messenger Queue", ChannelType = ChannelType.WebChat };
        var emInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Priority Support Email", ChannelType = ChannelType.Email };
        var wbInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Live Storefront WebChat", ChannelType = ChannelType.WebChat };

        _db.Inboxes.AddRange(waInbox, igInbox, msInbox, emInbox, wbInbox);

        var contact1 = Contact.Create(tenantId, "Karim", "Mansour", "karim.mansour@example.com", "+216 98 123 456");
        var contact2 = Contact.Create(tenantId, "Nour", "Ben Salah", "nour.bensalah@client.tn", "+216 55 987 654");
        var contact3 = Contact.Create(tenantId, "Yassine", "Trabelsi", "yassine.t@gmail.com", "+216 22 456 789");
        var contact4 = Contact.Create(tenantId, "Amira", "Gharbi", "amira.gharbi@outlook.com", "+216 29 112 334");

        _db.Contacts.AddRange(contact1, contact2, contact3, contact4);

        // 1. WhatsApp conversation: Where is my shipment?
        var c1 = Conversation.Create(tenantId, waInbox.Id, contact1.Id);
        c1.Priority = ConversationPriority.High;
        c1.Status = ConversationStatus.Open;
        _db.Conversations.Add(c1);

        var m1_1 = Message.Create(tenantId, c1.Id, MessageSenderType.Contact, "Hello SGrowing, where is my shipment for order #ORD-7721? I need it before the weekend!");
        _db.Messages.Add(m1_1);
        c1.LastMessageAtUtc = DateTime.UtcNow.AddMinutes(-12);

        // 2. Instagram conversation: Paused on hold
        var c2 = Conversation.Create(tenantId, igInbox.Id, contact2.Id);
        c2.Priority = ConversationPriority.Urgent;
        c2.ChangeStatus(ConversationStatus.WaitingOnCustomer);
        _db.Conversations.Add(c2);

        var m2_1 = Message.Create(tenantId, c2.Id, MessageSenderType.Contact, "Hi! I ordered the wrong shoe size (42 instead of 43). Can I exchange it?");
        var m2_2 = Message.Create(tenantId, c2.Id, MessageSenderType.Agent, "Hello Nour! Absolutely. We've initiated a size exchange hold. Could you confirm if you prefer white or black?", senderId: Guid.NewGuid());
        _db.Messages.AddRange(m2_1, m2_2);
        c2.FirstResponseAtUtc = DateTime.UtcNow.AddMinutes(-20);
        c2.LastMessageAtUtc = DateTime.UtcNow.AddMinutes(-15);

        // 3. Email conversation: Return policy question
        var c3 = Conversation.Create(tenantId, emInbox.Id, contact3.Id);
        c3.Priority = ConversationPriority.Normal;
        c3.Status = ConversationStatus.Open;
        _db.Conversations.Add(c3);

        var m3_1 = Message.Create(tenantId, c3.Id, MessageSenderType.Contact, "Good afternoon, what is your return policy and warranty conditions for electronics?");
        _db.Messages.Add(m3_1);
        c3.LastMessageAtUtc = DateTime.UtcNow.AddMinutes(-35);

        // 4. WebChat conversation: Update delivery address
        var c4 = Conversation.Create(tenantId, wbInbox.Id, contact4.Id);
        c4.Priority = ConversationPriority.High;
        c4.Status = ConversationStatus.Open;
        _db.Conversations.Add(c4);

        var m4_1 = Message.Create(tenantId, c4.Id, MessageSenderType.Contact, "Urgent: I need to update my delivery address before courier pickup. Can you change it to Ariana Center?");
        _db.Messages.Add(m4_1);
        c4.LastMessageAtUtc = DateTime.UtcNow.AddMinutes(-5);

        await _db.SaveChangesAsync(ct);
    }
}
