namespace SGrowing.Core.Entities;

public class ConversationPresenceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid AgentUserId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public bool IsTyping { get; set; } = false;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(2);
    public bool IsActive { get; set; } = true;

    public static ConversationPresenceSession Create(
        Guid tenantId,
        Guid conversationId,
        Guid agentUserId,
        string agentName,
        bool isTyping = false)
    {
        return new ConversationPresenceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            AgentUserId = agentUserId,
            AgentName = agentName.Trim(),
            IsTyping = isTyping,
            LastHeartbeatUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
            IsActive = true
        };
    }

    public void Heartbeat(bool isTyping)
    {
        IsTyping = isTyping;
        LastHeartbeatUtc = DateTime.UtcNow;
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2);
        IsActive = true;
    }

    public void Leave()
    {
        IsActive = false;
        IsTyping = false;
        ExpiresAtUtc = DateTime.UtcNow;
    }
}

public class CsatSurveyResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid CustomerContactId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Score { get; set; } = 5; // 1 to 5
    public string Channel { get; set; } = "WhatsApp";
    public string FeedbackText { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public static CsatSurveyResult Create(
        Guid tenantId,
        Guid conversationId,
        Guid customerContactId,
        string customerName,
        int score,
        string channel = "WhatsApp",
        string feedbackText = "")
    {
        return new CsatSurveyResult
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            CustomerContactId = customerContactId,
            CustomerName = customerName.Trim(),
            Score = Math.Clamp(score, 1, 5),
            Channel = channel.Trim(),
            FeedbackText = feedbackText.Trim(),
            RecordedAtUtc = DateTime.UtcNow
        };
    }
}
