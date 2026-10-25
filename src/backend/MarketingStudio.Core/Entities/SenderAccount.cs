namespace MarketingStudio.Core.Entities;

public enum MailboxProvider
{
    GoogleWorkspace = 1,
    Microsoft365 = 2,
    CustomSmtp = 3
}

public class SenderAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MailboxProvider Provider { get; set; } = MailboxProvider.GoogleWorkspace;
    public int DailyQuota { get; set; } = 40;
    public int SentTodayCount { get; private set; }
    public bool WarmupEnabled { get; set; } = true;
    public int WarmupRampDailyIncrement { get; set; } = 3;
    public int HealthScore { get; set; } = 95;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private SenderAccount() { }

    public static SenderAccount Create(
        Guid tenantId,
        string email,
        string displayName,
        MailboxProvider provider = MailboxProvider.GoogleWorkspace,
        int targetDailyQuota = 40,
        int rampDailyIncrement = 3)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));

        return new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName,
            Provider = provider,
            DailyQuota = targetDailyQuota,
            SentTodayCount = 0,
            WarmupEnabled = true,
            WarmupRampDailyIncrement = rampDailyIncrement,
            HealthScore = 95,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public bool CanSendToday() => IsActive && SentTodayCount < DailyQuota;

    public void RecordSend()
    {
        SentTodayCount++;
    }

    public void ResetDailyQuota()
    {
        SentTodayCount = 0;
    }

    public int CalculateMaxSafeQuota(int daysSinceCreated)
    {
        if (!WarmupEnabled) return DailyQuota;
        var ramped = 5 + (daysSinceCreated * WarmupRampDailyIncrement);
        return Math.Min(ramped, DailyQuota);
    }
}
