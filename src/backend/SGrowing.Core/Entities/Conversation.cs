namespace SGrowing.Core.Entities;

public enum ConversationStatus
{
    Open = 1,
    Snoozed = 2,
    Resolved = 3,
    WaitingOnCustomer = 4
}

public enum ConversationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum AiMode
{
    Disabled = 0,
    Copilot = 1,
    Autonomous = 2
}

public class Conversation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InboxId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid? AssigneeId { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;
    public ConversationPriority Priority { get; set; } = ConversationPriority.Normal;
    public AiMode AiMode { get; set; } = AiMode.Copilot;
    public bool IsSlaBreached { get; set; } = false;
    public DateTime? FirstResponseAtUtc { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public int AccumulatedPausedMinutes { get; set; } = 0;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; set; }

    private Conversation() { }

    public static Conversation Create(Guid tenantId, Guid inboxId, Guid contactId, AiMode aiMode = AiMode.Copilot)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (inboxId == Guid.Empty) throw new ArgumentException("InboxId is required.", nameof(inboxId));
        if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));

        return new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InboxId = inboxId,
            ContactId = contactId,
            Status = ConversationStatus.Open,
            Priority = ConversationPriority.Normal,
            AiMode = aiMode,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void ChangeStatus(ConversationStatus status)
    {
        if (Status == ConversationStatus.WaitingOnCustomer && status != ConversationStatus.WaitingOnCustomer)
        {
            if (PausedAtUtc.HasValue)
            {
                var pausedDuration = (int)Math.Max(0, (DateTime.UtcNow - PausedAtUtc.Value).TotalMinutes);
                AccumulatedPausedMinutes += pausedDuration;
                PausedAtUtc = null;
            }
        }
        else if (status == ConversationStatus.WaitingOnCustomer && Status != ConversationStatus.WaitingOnCustomer)
        {
            PausedAtUtc = DateTime.UtcNow;
        }

        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AssignTo(Guid? userId)
    {
        AssigneeId = userId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
