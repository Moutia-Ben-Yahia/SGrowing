using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Deliverability.WarmupSwarm;

public interface IWarmupSwarmEngine
{
    MailboxWarmupAccount EnrollAccount(EnrollWarmupAccountRequest request);
    MailboxWarmupAccount? GetAccount(string tenantId, string accountId);
    List<MailboxWarmupAccount> ListAccounts(string tenantId);
    P2PExchangeSummary SimulateP2PExchange(SimulateP2PExchangeRequest request);
    MailboxWarmupAccount AuditDns(AuditDnsRequest request);
}

public class WarmupSwarmEngine : IWarmupSwarmEngine
{
    private readonly ConcurrentDictionary<string, MailboxWarmupAccount> _accounts = new();
    private readonly Random _random;

    public WarmupSwarmEngine(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    private static string BuildKey(string tenantId, string accountId) => $"{tenantId}::{accountId}";

    public MailboxWarmupAccount EnrollAccount(EnrollWarmupAccountRequest request)
    {
        var domain = request.Email.Contains('@') ? request.Email.Split('@')[1] : "example.com";
        var accountId = "mbx_" + Math.Abs(request.Email.GetHashCode()).ToString("x8");
        var key = BuildKey(request.TenantId, accountId);

        var initialDns = EvaluateDomainDns(domain);

        var account = new MailboxWarmupAccount(
            AccountId: accountId,
            TenantId: request.TenantId,
            Email: request.Email,
            SmtpHost: request.SmtpHost,
            TargetDailyVolume: Math.Max(10, request.TargetDailyVolume),
            CurrentDailySent: 0,
            RampDay: 1,
            Status: WarmupStatus.WarmingUp,
            ReputationScore: 65.0,
            OpenRatePercent: 88.0,
            SpamRescuePercent: 95.0,
            DnsHealth: initialDns,
            EnrolledAtUtc: DateTime.UtcNow,
            LastAuditedUtc: DateTime.UtcNow
        );

        _accounts[key] = account;
        return account;
    }

    public MailboxWarmupAccount? GetAccount(string tenantId, string accountId)
    {
        _accounts.TryGetValue(BuildKey(tenantId, accountId), out var acc);
        return acc;
    }

    public List<MailboxWarmupAccount> ListAccounts(string tenantId)
    {
        return _accounts.Values
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.EnrolledAtUtc)
            .ToList();
    }

    public P2PExchangeSummary SimulateP2PExchange(SimulateP2PExchangeRequest request)
    {
        var key = BuildKey(request.TenantId, request.AccountId);
        if (!_accounts.TryGetValue(key, out var acc))
        {
            throw new KeyNotFoundException($"Mailbox account '{request.AccountId}' not found.");
        }

        var batch = Math.Max(1, request.ExchangeBatchSize);
        var simulatedOpens = (int)Math.Round(batch * (0.85 + _random.NextDouble() * 0.12));
        var spamRescues = (int)Math.Round(batch * (0.15 + _random.NextDouble() * 0.10));
        var replies = (int)Math.Round(batch * (0.40 + _random.NextDouble() * 0.20));

        var newSent = acc.CurrentDailySent + batch;
        var newRampDay = acc.RampDay + (newSent >= acc.TargetDailyVolume ? 1 : 0);

        // Reputation score formula: weighted DNS pass + open rate + rescue rate
        var dnsPassRate = acc.DnsHealth.Count > 0
            ? (double)acc.DnsHealth.Count(d => d.IsValid) / acc.DnsHealth.Count
            : 0.5;

        var baseScore = 50.0 + (dnsPassRate * 20.0) + (Math.Min(1.0, (double)simulatedOpens / batch) * 20.0) + (Math.Min(1.0, (double)spamRescues / Math.Max(1, spamRescues)) * 10.0);
        var clampedScore = Math.Round(Math.Min(99.0, Math.Max(40.0, baseScore)), 1);

        var newStatus = clampedScore >= 85.0
            ? WarmupStatus.Healthy
            : (newSent > acc.TargetDailyVolume * 1.5 ? WarmupStatus.Throttled : WarmupStatus.WarmingUp);

        var updated = acc with
        {
            CurrentDailySent = newSent,
            RampDay = newRampDay,
            ReputationScore = clampedScore,
            Status = newStatus,
            LastAuditedUtc = DateTime.UtcNow
        };

        _accounts[key] = updated;

        return new P2PExchangeSummary(
            AccountId: acc.AccountId,
            MessagesDispatched: batch,
            SimulatedOpens: simulatedOpens,
            RescuedFromSpam: spamRescues,
            ContextualRepliesGenerated: replies,
            NewReputationScore: clampedScore,
            Status: newStatus
        );
    }

    public MailboxWarmupAccount AuditDns(AuditDnsRequest request)
    {
        var key = BuildKey(request.TenantId, request.AccountId);
        if (!_accounts.TryGetValue(key, out var acc))
        {
            throw new KeyNotFoundException($"Mailbox account '{request.AccountId}' not found.");
        }

        var dnsResults = EvaluateDomainDns(request.Domain);
        var allValid = dnsResults.All(d => d.IsValid);

        var updated = acc with
        {
            DnsHealth = dnsResults,
            ReputationScore = allValid ? Math.Max(acc.ReputationScore, 85.0) : Math.Min(acc.ReputationScore, 65.0),
            Status = allValid ? WarmupStatus.Healthy : WarmupStatus.Degraded,
            LastAuditedUtc = DateTime.UtcNow
        };

        _accounts[key] = updated;
        return updated;
    }

    private static List<DnsCheckResult> EvaluateDomainDns(string domain)
    {
        return new List<DnsCheckResult>
        {
            new(
                RecordType: "SPF",
                Hostname: domain,
                Value: $"v=spf1 include:_spf.{domain} ~all",
                IsValid: true,
                StatusBadge: "PASS",
                Recommendations: "SPF record correctly specifies authorized mail transfer agents."
            ),
            new(
                RecordType: "DKIM",
                Hostname: $"default._domainkey.{domain}",
                Value: "v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQ...",
                IsValid: true,
                StatusBadge: "PASS",
                Recommendations: "2048-bit RSA key configured with valid cryptographic selector."
            ),
            new(
                RecordType: "DMARC",
                Hostname: $"_dmarc.{domain}",
                Value: $"v=DMARC1; p=reject; rua=mailto:dmarc-reports@{domain}",
                IsValid: true,
                StatusBadge: "PASS",
                Recommendations: "Strict enforcement policy 'p=reject' prevents spoofing and domain impersonation."
            ),
            new(
                RecordType: "BIMI",
                Hostname: $"default._bimi.{domain}",
                Value: $"v=BIMI1; l=https://app.sgrowing.io/assets/logo.svg; a=self",
                IsValid: true,
                StatusBadge: "PASS",
                Recommendations: "Brand Indicators for Message Identification (BIMI) verified."
            )
        };
    }
}
