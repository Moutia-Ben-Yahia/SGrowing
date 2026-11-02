namespace SGrowing.Api.Modules.Deliverability.WarmupSwarm;

public enum WarmupStatus
{
    Inactive,
    WarmingUp,
    Healthy,
    Throttled,
    Degraded
}

public record DnsCheckResult(
    string RecordType,
    string Hostname,
    string Value,
    bool IsValid,
    string StatusBadge,
    string Recommendations
);

public record MailboxWarmupAccount(
    string AccountId,
    string TenantId,
    string Email,
    string SmtpHost,
    int TargetDailyVolume,
    int CurrentDailySent,
    int RampDay,
    WarmupStatus Status,
    double ReputationScore,
    double OpenRatePercent,
    double SpamRescuePercent,
    List<DnsCheckResult> DnsHealth,
    DateTime EnrolledAtUtc,
    DateTime LastAuditedUtc
);

public record EnrollWarmupAccountRequest(
    string TenantId,
    string Email,
    string SmtpHost,
    int TargetDailyVolume = 50
);

public record SimulateP2PExchangeRequest(
    string TenantId,
    string AccountId,
    int ExchangeBatchSize = 10
);

public record P2PExchangeSummary(
    string AccountId,
    int MessagesDispatched,
    int SimulatedOpens,
    int RescuedFromSpam,
    int ContextualRepliesGenerated,
    double NewReputationScore,
    WarmupStatus Status
);

public record AuditDnsRequest(
    string TenantId,
    string AccountId,
    string Domain
);
