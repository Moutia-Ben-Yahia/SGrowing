namespace SGrowing.Core.Entities;

public enum MailboxProvider
{
    GoogleWorkspace = 1,
    Microsoft365 = 2,
    CustomSmtp = 3
}

public enum InboxRotationStatus
{
    Active = 1,
    Warmup = 2,
    Quarantined = 3,
    Cooling = 4
}

public class SenderAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MailboxProvider Provider { get; set; } = MailboxProvider.GoogleWorkspace;
    public int DailyQuota { get; set; } = 40;
    public int SentTodayCount { get; set; }
    public bool WarmupEnabled { get; set; } = true;
    public int WarmupRampDailyIncrement { get; set; } = 3;
    public int HealthScore { get; set; } = 95;
    public bool IsActive { get; set; } = true;
    public InboxRotationStatus Status { get; set; } = InboxRotationStatus.Active;
    public string? QuarantineReason { get; set; }
    public DateTime? QuarantinedAtUtc { get; set; }
    public DateTime? ThawedAtUtc { get; set; }
    public DateTime? LastDispatchedAtUtc { get; set; }
    public int TotalDispatched { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalBounced { get; set; }
    public int TotalReplied { get; set; }
    public int TotalOpened { get; set; }
    public int TotalSpamComplaints { get; set; }
    public bool HasSpf { get; set; } = true;
    public bool HasDkim { get; set; } = true;
    public bool HasDmarc { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string Domain => Email.Contains('@') ? Email.Split('@')[1].Trim().ToLowerInvariant() : string.Empty;
    public int RemainingDailyQuota => Math.Max(0, DailyQuota - SentTodayCount);
    public double BounceRate => TotalDispatched > 0 ? Math.Round(((double)TotalBounced / TotalDispatched) * 100.0, 2) : 0.0;
    public double ReplyRate => TotalDispatched > 0 ? Math.Round(((double)TotalReplied / TotalDispatched) * 100.0, 2) : 0.0;
    public double OpenRate => TotalDispatched > 0 ? Math.Round(((double)TotalOpened / TotalDispatched) * 100.0, 2) : 0.0;

    public SenderAccount() { }

    public static SenderAccount Create(
        Guid tenantId,
        string email,
        string displayName,
        MailboxProvider provider = MailboxProvider.GoogleWorkspace,
        int targetDailyQuota = 40,
        int rampDailyIncrement = 3,
        InboxRotationStatus initialStatus = InboxRotationStatus.Active,
        bool warmupEnabled = true)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));

        var account = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Provider = provider,
            DailyQuota = targetDailyQuota,
            SentTodayCount = 0,
            WarmupEnabled = warmupEnabled,
            WarmupRampDailyIncrement = rampDailyIncrement,
            HealthScore = 95,
            IsActive = true,
            Status = initialStatus,
            CreatedAtUtc = DateTime.UtcNow,
            HasSpf = true,
            HasDkim = true,
            HasDmarc = true
        };

        account.RecalculateHealthScore();
        return account;
    }

    public bool CanSendToday() => IsActive && Status != InboxRotationStatus.Quarantined && SentTodayCount < DailyQuota;

    public void RecordSend()
    {
        SentTodayCount++;
        TotalDispatched++;
        LastDispatchedAtUtc = DateTime.UtcNow;
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

    public void Quarantine(string reason = "Manual quarantine triggered")
    {
        Status = InboxRotationStatus.Quarantined;
        QuarantineReason = reason;
        QuarantinedAtUtc = DateTime.UtcNow;
        HealthScore = Math.Min(HealthScore, 40);
    }

    public void Thaw()
    {
        Status = WarmupEnabled ? InboxRotationStatus.Warmup : InboxRotationStatus.Active;
        QuarantineReason = null;
        ThawedAtUtc = DateTime.UtcNow;
        RecalculateHealthScore();
    }

    public void RecordDispatchEvent(bool wasDelivered, bool wasBounced, bool wasReplied, bool wasOpened = false, bool isSpamComplaint = false)
    {
        if (wasDelivered) TotalDelivered++;
        if (wasBounced) TotalBounced++;
        if (wasReplied) TotalReplied++;
        if (wasOpened) TotalOpened++;
        if (isSpamComplaint) TotalSpamComplaints++;

        RecalculateHealthScore();

        // Automatic Quarantine check: if bounce rate > 2.5% (with significant dispatch or multiple bounces) or spam complaint
        if ((BounceRate > 2.5 && (TotalDispatched >= 10 || TotalBounced >= 2)) || TotalSpamComplaints > 0)
        {
            var triggerReason = TotalSpamComplaints > 0
                ? $"Spam complaint reported ({TotalSpamComplaints} total complaints)"
                : $"Bounce rate ({BounceRate:F1}%) exceeded safety threshold (2.5%)";
            Quarantine(triggerReason);
        }
    }

    public int RecalculateHealthScore()
    {
        int score = 0;

        // 1. DNS Authentication baseline (max 40 pts)
        if (HasSpf) score += 15;
        if (HasDkim) score += 15;
        if (HasDmarc) score += 10;

        // 2. Engagement & Delivery metrics (max 60 pts)
        if (TotalDispatched == 0)
        {
            score += 55; // Baseline high score for newly connected inbox with valid DNS
        }
        else
        {
            // Bounce rate score (max 30 pts)
            if (BounceRate <= 1.0)
            {
                score += 30;
            }
            else if (BounceRate <= 2.5)
            {
                var penalty = (int)Math.Round(((BounceRate - 1.0) / 1.5) * 20.0);
                score += Math.Max(10, 30 - penalty);
            }
            else
            {
                score += 0; // Exceeded threshold!
            }

            // Reply rate score (max 20 pts)
            if (ReplyRate >= 15.0)
            {
                score += 20;
            }
            else
            {
                score += (int)Math.Min(20, Math.Round(ReplyRate * 1.33));
            }

            // Open rate score (max 10 pts)
            if (OpenRate >= 50.0)
            {
                score += 10;
            }
            else
            {
                score += (int)Math.Min(10, Math.Round(OpenRate * 0.2));
            }

            // Spam complaints penalty
            score -= (TotalSpamComplaints * 20);
        }

        if (Status == InboxRotationStatus.Quarantined)
        {
            score = Math.Min(score, 35);
        }

        HealthScore = Math.Clamp(score, 0, 100);
        return HealthScore;
    }
}
