namespace SGrowing.Core.Entities;

public class DomainDnsHealthRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public bool HasSpf { get; set; } = false;
    public string SpfRecord { get; set; } = string.Empty;
    public bool HasDkim { get; set; } = false;
    public string DkimSelector { get; set; } = "default";
    public bool HasDmarc { get; set; } = false;
    public string DmarcPolicy { get; set; } = "none"; // none, quarantine, reject
    public bool HasMx { get; set; } = false;
    public string MxHost { get; set; } = string.Empty;
    public int OverallHealthScore { get; set; } = 100; // 0 to 100
    public int OverallScore { get => OverallHealthScore; set => OverallHealthScore = value; }
    public string DmarcRecord { get => $"v=DMARC1; p={DmarcPolicy}"; set { } }
    public string IssuesJson { get; set; } = "[]";
    public List<string> AuditIssues => System.Text.Json.JsonSerializer.Deserialize<List<string>>(IssuesJson) ?? new();
    public DateTime LastCheckedAtUtc { get; set; } = DateTime.UtcNow;

    public static DomainDnsHealthRecord Create(
        Guid tenantId,
        string domainName,
        bool hasSpf,
        string spfRecord,
        bool hasDkim,
        string dkimSelector,
        bool hasDmarc,
        string dmarcPolicy,
        bool hasMx,
        string mxHost,
        List<string>? issues = null)
    {
        var issuesList = issues ?? new List<string>();
        int score = 100;
        if (!hasSpf) score -= 25;
        if (!hasDkim) score -= 25;
        if (!hasDmarc) score -= 25;
        if (!hasMx) score -= 25;
        if (score < 0) score = 0;

        return new DomainDnsHealthRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainName = domainName.Trim().ToLowerInvariant(),
            HasSpf = hasSpf,
            SpfRecord = spfRecord.Trim(),
            HasDkim = hasDkim,
            DkimSelector = dkimSelector.Trim(),
            HasDmarc = hasDmarc,
            DmarcPolicy = dmarcPolicy.Trim(),
            HasMx = hasMx,
            MxHost = mxHost.Trim(),
            OverallHealthScore = score,
            IssuesJson = System.Text.Json.JsonSerializer.Serialize(issuesList),
            LastCheckedAtUtc = DateTime.UtcNow
        };
    }
}

public class EmailSpamAnalysisReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtmlPreview { get; set; } = string.Empty;
    public int SpamScore { get; set; } = 0; // 0 to 100 (0 = clean, 100 = definite spam)
    public string TriggerWordsDetectedJson { get; set; } = "[]";
    public bool HasUnsubscribeHeader { get; set; } = true;
    public double ImageToTextRatio { get; set; } = 0.15;
    public string Verdict { get; set; } = "Safe"; // Safe, ModerateRisk, HighRisk
    public string RiskLevel { get => Verdict == "Safe" ? "Low" : Verdict; set => Verdict = value; }
    public List<string> DetectedTriggers => System.Text.Json.JsonSerializer.Deserialize<List<string>>(TriggerWordsDetectedJson) ?? new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static EmailSpamAnalysisReport Create(
        Guid tenantId,
        string subject,
        string bodyHtml,
        int spamScore,
        List<string> triggerWords,
        bool hasUnsubscribeHeader,
        double imageRatio,
        string verdict)
    {
        return new EmailSpamAnalysisReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Subject = subject.Trim(),
            BodyHtmlPreview = bodyHtml.Length > 500 ? bodyHtml[..500] : bodyHtml,
            SpamScore = spamScore,
            TriggerWordsDetectedJson = System.Text.Json.JsonSerializer.Serialize(triggerWords),
            HasUnsubscribeHeader = hasUnsubscribeHeader,
            ImageToTextRatio = imageRatio,
            Verdict = verdict,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class MailboxWarmupCadence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public int DailyTargetLimit { get; set; } = 40;
    public int DailyTarget { get => DailyTargetLimit; set => DailyTargetLimit = value; }
    public int CurrentDayNumber { get; set; } = 1;
    public string WarmupRampCurve { get; set; } = "Linear"; // Linear, Conservative, Aggressive
    public int TotalSimulatedSent { get; set; } = 0;
    public int TotalSentLifetime { get => TotalSimulatedSent; set => TotalSimulatedSent = value; }
    public int CurrentDailyVolume { get; set; } = 5;
    public int SentToday { get; set; } = 5;
    public int TotalSimulatedReceived { get; set; } = 0;
    public int SpamRescueCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public static MailboxWarmupCadence Create(
        Guid tenantId,
        string senderEmail,
        int dailyTarget = 40,
        string rampCurve = "Linear")
    {
        return new MailboxWarmupCadence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SenderEmail = senderEmail.Trim().ToLowerInvariant(),
            DailyTargetLimit = dailyTarget,
            CurrentDayNumber = 1,
            WarmupRampCurve = rampCurve,
            TotalSimulatedSent = 0,
            TotalSimulatedReceived = 0,
            SpamRescueCount = 0,
            IsActive = true,
            StartedAtUtc = DateTime.UtcNow
        };
    }
}

public class WarmupExchangeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarmupCadenceId { get; set; }
    public string PeerInboxEmail { get; set; } = string.Empty;
    public string Direction { get; set; } = "Sent"; // Sent, Received, RescuedFromSpam
    public bool OpenedByPeer { get => Direction == "Received" || Direction == "RescuedFromSpam"; set { } }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static WarmupExchangeLog Create(Guid tenantId, Guid cadenceId, string peerEmail, string direction)
    {
        return new WarmupExchangeLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarmupCadenceId = cadenceId,
            PeerInboxEmail = peerEmail.Trim().ToLowerInvariant(),
            Direction = direction,
            TimestampUtc = DateTime.UtcNow
        };
    }
}
