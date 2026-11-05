using System;
using System.Collections.Generic;

namespace SGrowing.Core.Entities;

public class UnifiedInboxThreadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ActiveChannelsJson { get; set; } = "[]";
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Medium";
    public DateTime SlaDueAtUtc { get; set; } = DateTime.UtcNow.AddHours(12);
    public bool SlaBreached { get; set; } = false;
    public string? AssignedAgentId { get; set; }
    public bool AiDeflected { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<UnifiedInboxMessageEntity> Messages { get; set; } = new();
}

public class UnifiedInboxMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Sentiment { get; set; } = "Neutral";
    public bool IsFromCustomer { get; set; } = true;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public UnifiedInboxThreadEntity? Thread { get; set; }
}
