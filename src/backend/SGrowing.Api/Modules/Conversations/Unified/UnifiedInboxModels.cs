using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.Unified;

public enum ConversationChannel
{
    Email,
    WhatsApp,
    Sms,
    WebChat
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TicketStatus
{
    Open,
    PendingCustomer,
    Resolved,
    Escalated
}

public enum CustomerSentiment
{
    Positive,
    Neutral,
    Negative,
    Frustrated
}

public record CustomerMessage(
    Guid Id,
    Guid ThreadId,
    string TenantId,
    ConversationChannel Channel,
    string SenderId,
    string SenderName,
    string Content,
    CustomerSentiment Sentiment,
    bool IsFromCustomer,
    DateTime TimestampUtc
);

public record UnifiedThread(
    Guid Id,
    string TenantId,
    string CustomerEmail,
    string CustomerPhone,
    List<ConversationChannel> ActiveChannels,
    List<CustomerMessage> Messages,
    TicketStatus Status,
    TicketPriority Priority,
    DateTime SlaDueAtUtc,
    bool SlaBreached,
    string? AssignedAgentId,
    bool AiDeflected,
    DateTime UpdatedAtUtc
);

public record IngestMessageRequest(
    string TenantId,
    ConversationChannel Channel,
    string CustomerEmail,
    string CustomerPhone,
    string SenderName,
    string Content,
    bool IsFromCustomer = true
);

public record AiDeflectionResult(
    Guid ThreadId,
    string SuggestedResponse,
    double ConfidenceScore,
    bool CanAutoSend,
    string SourceArticle
);

public record EscalateTicketRequest(
    Guid ThreadId,
    string TenantId,
    string Reason,
    TicketPriority NewPriority
);

public record AppendAgentReplyRequest(
    string TenantId,
    string AgentId,
    string AgentName,
    string Content,
    ConversationChannel Channel
);

public static class UnifiedInboxEntityExtensions
{
    public static UnifiedThread ToModel(this UnifiedInboxThreadEntity entity)
    {
        List<ConversationChannel>? channels = null;
        if (!string.IsNullOrWhiteSpace(entity.ActiveChannelsJson))
        {
            try
            {
                channels = JsonSerializer.Deserialize<List<ConversationChannel>>(entity.ActiveChannelsJson);
            }
            catch
            {
                // Fallback to empty
            }
        }

        Enum.TryParse<TicketStatus>(entity.Status, true, out var status);
        Enum.TryParse<TicketPriority>(entity.Priority, true, out var priority);

        var messages = entity.Messages != null
            ? entity.Messages
                .OrderBy(m => m.TimestampUtc)
                .Select(m => m.ToModel())
                .ToList()
            : new List<CustomerMessage>();

        return new UnifiedThread(
            Id: entity.Id,
            TenantId: entity.TenantId,
            CustomerEmail: entity.CustomerEmail,
            CustomerPhone: entity.CustomerPhone,
            ActiveChannels: channels ?? new List<ConversationChannel>(),
            Messages: messages,
            Status: status,
            Priority: priority,
            SlaDueAtUtc: entity.SlaDueAtUtc,
            SlaBreached: entity.SlaBreached,
            AssignedAgentId: entity.AssignedAgentId,
            AiDeflected: entity.AiDeflected,
            UpdatedAtUtc: entity.UpdatedAtUtc
        );
    }

    public static CustomerMessage ToModel(this UnifiedInboxMessageEntity entity)
    {
        Enum.TryParse<ConversationChannel>(entity.Channel, true, out var channel);
        Enum.TryParse<CustomerSentiment>(entity.Sentiment, true, out var sentiment);

        return new CustomerMessage(
            Id: entity.Id,
            ThreadId: entity.ThreadId,
            TenantId: entity.TenantId,
            Channel: channel,
            SenderId: entity.SenderId,
            SenderName: entity.SenderName,
            Content: entity.Content,
            Sentiment: sentiment,
            IsFromCustomer: entity.IsFromCustomer,
            TimestampUtc: entity.TimestampUtc
        );
    }

    public static UnifiedInboxThreadEntity ToEntity(this UnifiedThread thread)
    {
        var entity = new UnifiedInboxThreadEntity
        {
            Id = thread.Id,
            TenantId = thread.TenantId,
            CustomerEmail = thread.CustomerEmail,
            CustomerPhone = thread.CustomerPhone,
            ActiveChannelsJson = JsonSerializer.Serialize(thread.ActiveChannels ?? new List<ConversationChannel>()),
            Status = thread.Status.ToString(),
            Priority = thread.Priority.ToString(),
            SlaDueAtUtc = thread.SlaDueAtUtc,
            SlaBreached = thread.SlaBreached,
            AssignedAgentId = thread.AssignedAgentId,
            AiDeflected = thread.AiDeflected,
            CreatedAtUtc = thread.Messages.FirstOrDefault()?.TimestampUtc ?? thread.UpdatedAtUtc,
            UpdatedAtUtc = thread.UpdatedAtUtc,
            Messages = new List<UnifiedInboxMessageEntity>()
        };

        if (thread.Messages != null)
        {
            foreach (var msg in thread.Messages)
            {
                entity.Messages.Add(msg.ToEntity());
            }
        }

        return entity;
    }

    public static UnifiedInboxMessageEntity ToEntity(this CustomerMessage message)
    {
        return new UnifiedInboxMessageEntity
        {
            Id = message.Id,
            ThreadId = message.ThreadId,
            TenantId = message.TenantId,
            Channel = message.Channel.ToString(),
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            Content = message.Content,
            Sentiment = message.Sentiment.ToString(),
            IsFromCustomer = message.IsFromCustomer,
            TimestampUtc = message.TimestampUtc
        };
    }
}
