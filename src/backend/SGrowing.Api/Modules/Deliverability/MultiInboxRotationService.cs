using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Deliverability;

public record RegisterSenderInboxDto(
    string Email,
    string DisplayName,
    MailboxProvider Provider = MailboxProvider.GoogleWorkspace,
    int DailyQuota = 35,
    bool WarmupEnabled = true,
    int WarmupRampDailyIncrement = 3
);

public record SelectSenderResult(
    SenderAccount SelectedSender,
    int JitterDelaySeconds,
    string EspMatchStrategy,
    double CalculatedWeight,
    DateTime ScheduledDispatchUtc,
    string RecipientDomain,
    string DetectedRecipientEsp
);

public record DeliverabilityRadarMetrics(
    Guid TenantId,
    int TotalInboxes,
    int ActiveInboxes,
    int WarmupInboxes,
    int QuarantinedInboxes,
    int CoolingInboxes,
    int TotalDailyCapacity,
    int RemainingDailyCapacity,
    int TotalSentToday,
    double AverageHealthScore,
    double AverageBounceRate,
    double AverageReplyRate,
    double AverageOpenRate,
    int TotalDispatchedLifetime,
    int TotalBouncedLifetime,
    int TotalRepliedLifetime,
    DnsHealthSummary DnsHealth,
    ProviderDistributionDto ProviderDistribution,
    string ReputationTier,
    IReadOnlyList<RadarAxisMetric> RadarAxes
);

public record DnsHealthSummary(
    double SpfPassRate,
    double DkimPassRate,
    double DmarcPassRate,
    int FullyAlignedCount
);

public record ProviderDistributionDto(
    int GoogleWorkspaceCount,
    int Microsoft365Count,
    int CustomSmtpCount
);

public record RadarAxisMetric(
    string AxisName,
    int Score,
    int Benchmark
);

public record RotationSimulationItem(
    Guid SenderAccountId,
    string Email,
    MailboxProvider Provider,
    int AllocatedDispatches,
    double AllocationPercentage,
    int PreQuotaRemaining,
    int PostQuotaRemaining,
    int HealthScore
);

public record RotationSimulationResult(
    int TotalRequestedBatchSize,
    int SuccessfullyAllocated,
    int UnallocatedDueToExhaustion,
    double AverageJitterSeconds,
    int MinJitterSeconds,
    int MaxJitterSeconds,
    IReadOnlyList<RotationSimulationItem> Allocations
);

public record PoolAuditSummary(
    int TotalAudited,
    int DormantMarkedCooling,
    int WarmupPromotedToActive,
    int QuarantinedAutoRecovered,
    DateTime AuditedAtUtc
);

public interface IMultiInboxRotationService
{
    Task<SenderAccount> RegisterSenderAccountAsync(Guid tenantId, RegisterSenderInboxDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<SenderAccount>> GetPoolAccountsAsync(Guid tenantId, CancellationToken ct = default);
    Task<SenderAccount?> GetAccountByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<SelectSenderResult> SelectNextSenderAsync(Guid tenantId, string recipientEmail, CancellationToken ct = default);
    Task<SenderAccount> QuarantineAccountAsync(Guid tenantId, Guid id, string? reason = null, CancellationToken ct = default);
    Task<SenderAccount> ThawAccountAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<SenderAccount> RecordDispatchEventAsync(Guid senderAccountId, bool wasDelivered, bool wasBounced, bool wasReplied, bool wasOpened = false, bool isSpamComplaint = false, CancellationToken ct = default);
    Task<DeliverabilityRadarMetrics> GetDeliverabilityRadarAsync(Guid tenantId, CancellationToken ct = default);
    Task<RotationSimulationResult> SimulateBatchRotationAsync(Guid tenantId, int batchSize = 100, double googleRatio = 0.6, double microsoftRatio = 0.3, double customRatio = 0.1, CancellationToken ct = default);
    int CalculateInterSendJitterSeconds(int mean = 240, int stdDev = 45, int min = 120, int max = 420);
    Task<int> ResetDailyQuotasAsync(CancellationToken ct = default);
    Task<PoolAuditSummary> AuditPoolHealthAsync(CancellationToken ct = default);
}

public class MultiInboxRotationService : IMultiInboxRotationService
{
    private readonly SGrowingDbContext _db;

    public MultiInboxRotationService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<SenderAccount> RegisterSenderAccountAsync(Guid tenantId, RegisterSenderInboxDto dto, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new ArgumentException("Email is required.", nameof(dto.Email));

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        // Check if account already exists for this tenant
        var existing = await _db.SenderAccounts
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Email == normalizedEmail, ct);

        if (existing != null)
        {
            existing.DisplayName = dto.DisplayName.Trim();
            existing.DailyQuota = dto.DailyQuota;
            existing.Provider = dto.Provider;
            existing.WarmupEnabled = dto.WarmupEnabled;
            existing.WarmupRampDailyIncrement = dto.WarmupRampDailyIncrement;
            existing.IsActive = true;
            existing.RecalculateHealthScore();
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var account = SenderAccount.Create(
            tenantId: tenantId,
            email: normalizedEmail,
            displayName: dto.DisplayName,
            provider: dto.Provider,
            targetDailyQuota: dto.DailyQuota,
            rampDailyIncrement: dto.WarmupRampDailyIncrement,
            initialStatus: dto.WarmupEnabled ? InboxRotationStatus.Warmup : InboxRotationStatus.Active,
            warmupEnabled: dto.WarmupEnabled
        );

        _db.SenderAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<IReadOnlyList<SenderAccount>> GetPoolAccountsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.SenderAccounts
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Email)
            .ToListAsync(ct);
    }

    public async Task<SenderAccount?> GetAccountByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.SenderAccounts
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);
    }

    public static MailboxProvider? DetectRecipientEsp(string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
            return null;

        var domain = recipientEmail.Split('@').LastOrDefault()?.Trim().ToLowerInvariant() ?? "";

        // Google Workspace detection
        if (domain == "gmail.com" || domain == "googlemail.com" || domain.EndsWith(".google.com"))
            return MailboxProvider.GoogleWorkspace;

        // Microsoft 365 detection
        if (domain == "outlook.com" || domain == "hotmail.com" || domain == "live.com" || domain == "msn.com" || domain == "office365.com" || domain.EndsWith(".microsoft.com"))
            return MailboxProvider.Microsoft365;

        return null;
    }

    public async Task<SelectSenderResult> SelectNextSenderAsync(Guid tenantId, string recipientEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Recipient email must be provided.", nameof(recipientEmail));

        var domain = recipientEmail.Contains('@')
            ? recipientEmail.Split('@').LastOrDefault()?.Trim().ToLowerInvariant() ?? ""
            : recipientEmail.Trim().ToLowerInvariant();

        var detectedEsp = DetectRecipientEsp(recipientEmail);

        // Fetch candidate inboxes for tenant
        var accounts = await _db.SenderAccounts
            .Where(s => s.TenantId == tenantId && s.IsActive && s.Status != InboxRotationStatus.Quarantined)
            .ToListAsync(ct);

        // Eligible candidates must have remaining daily quota and positive health score
        var eligible = accounts
            .Where(a => a.RemainingDailyQuota > 0 && a.HealthScore > 0)
            .ToList();

        if (eligible.Count == 0)
        {
            throw new InvalidOperationException("No available sender inboxes in pool. All accounts have exhausted daily quota, zero health score, or are quarantined.");
        }

        string matchStrategy;
        List<SenderAccount> candidatePool;

        // ESP-Matching preference: If recipient is Google or Microsoft, prefer matching ESP sender accounts
        if (detectedEsp.HasValue)
        {
            var espMatches = eligible.Where(a => a.Provider == detectedEsp.Value).ToList();
            if (espMatches.Count > 0)
            {
                candidatePool = espMatches;
                matchStrategy = $"{detectedEsp.Value}-to-{detectedEsp.Value}";
            }
            else
            {
                candidatePool = eligible;
                matchStrategy = "CapacityWeightedFallback";
            }
        }
        else
        {
            candidatePool = eligible;
            matchStrategy = "CapacityWeightedGeneric";
        }

        // Capacity-Weighted Selection: Weight = max(0, DailyLimit - SentToday) * HealthScore
        var weights = candidatePool.Select(a =>
        {
            double w = Math.Max(0, a.DailyQuota - a.SentTodayCount) * a.HealthScore;
            return (Account: a, Weight: w);
        }).ToList();

        double totalWeight = weights.Sum(x => x.Weight);
        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("Calculated selection weights for all candidate inboxes are zero.");
        }

        // Roll weighted lottery
        double roll = Random.Shared.NextDouble() * totalWeight;
        double cumulative = 0;
        SenderAccount selected = candidatePool[0];
        double chosenWeight = 0;

        foreach (var item in weights)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
            {
                selected = item.Account;
                chosenWeight = item.Weight;
                break;
            }
        }

        // Record the send and save state
        selected.RecordSend();
        await _db.SaveChangesAsync(ct);

        int jitterSeconds = CalculateInterSendJitterSeconds();
        var scheduledUtc = DateTime.UtcNow.AddSeconds(jitterSeconds);

        return new SelectSenderResult(
            SelectedSender: selected,
            JitterDelaySeconds: jitterSeconds,
            EspMatchStrategy: matchStrategy,
            CalculatedWeight: Math.Round(chosenWeight, 2),
            ScheduledDispatchUtc: scheduledUtc,
            RecipientDomain: domain,
            DetectedRecipientEsp: detectedEsp?.ToString() ?? "CustomSmtpOrOther"
        );
    }

    public int CalculateInterSendJitterSeconds(int mean = 240, int stdDev = 45, int min = 120, int max = 420)
    {
        // Box-Muller transform for normal distribution
        double u1 = 1.0 - Random.Shared.NextDouble();
        double u2 = 1.0 - Random.Shared.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        double randNormal = mean + (stdDev * randStdNormal);
        return (int)Math.Round(Math.Clamp(randNormal, min, max));
    }

    public async Task<SenderAccount> QuarantineAccountAsync(Guid tenantId, Guid id, string? reason = null, CancellationToken ct = default)
    {
        var account = await _db.SenderAccounts.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);
        if (account == null)
            throw new KeyNotFoundException($"Sender account with ID '{id}' was not found for tenant '{tenantId}'.");

        account.Quarantine(reason ?? "Manual administrative quarantine");
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<SenderAccount> ThawAccountAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var account = await _db.SenderAccounts.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);
        if (account == null)
            throw new KeyNotFoundException($"Sender account with ID '{id}' was not found for tenant '{tenantId}'.");

        account.Thaw();
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<SenderAccount> RecordDispatchEventAsync(
        Guid senderAccountId,
        bool wasDelivered,
        bool wasBounced,
        bool wasReplied,
        bool wasOpened = false,
        bool isSpamComplaint = false,
        CancellationToken ct = default)
    {
        var account = await _db.SenderAccounts.FirstOrDefaultAsync(s => s.Id == senderAccountId, ct);
        if (account == null)
            throw new KeyNotFoundException($"Sender account with ID '{senderAccountId}' was not found.");

        account.RecordDispatchEvent(wasDelivered, wasBounced, wasReplied, wasOpened, isSpamComplaint);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task<DeliverabilityRadarMetrics> GetDeliverabilityRadarAsync(Guid tenantId, CancellationToken ct = default)
    {
        var inboxes = await _db.SenderAccounts
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        int totalInboxes = inboxes.Count;
        int activeInboxes = inboxes.Count(i => i.Status == InboxRotationStatus.Active && i.IsActive);
        int warmupInboxes = inboxes.Count(i => i.Status == InboxRotationStatus.Warmup);
        int quarantinedInboxes = inboxes.Count(i => i.Status == InboxRotationStatus.Quarantined);
        int coolingInboxes = inboxes.Count(i => i.Status == InboxRotationStatus.Cooling);

        int totalDailyCapacity = inboxes.Sum(i => i.DailyQuota);
        int remainingDailyCapacity = inboxes
            .Where(i => i.Status != InboxRotationStatus.Quarantined && i.IsActive)
            .Sum(i => i.RemainingDailyQuota);
        int totalSentToday = inboxes.Sum(i => i.SentTodayCount);

        double avgHealthScore = totalInboxes > 0
            ? Math.Round(inboxes.Average(i => i.HealthScore), 1)
            : 100.0;

        int totalDispatchedLifetime = inboxes.Sum(i => i.TotalDispatched);
        int totalBouncedLifetime = inboxes.Sum(i => i.TotalBounced);
        int totalRepliedLifetime = inboxes.Sum(i => i.TotalReplied);
        int totalOpenedLifetime = inboxes.Sum(i => i.TotalOpened);

        double avgBounceRate = totalDispatchedLifetime > 0
            ? Math.Round(((double)totalBouncedLifetime / totalDispatchedLifetime) * 100.0, 2)
            : 0.0;

        double avgReplyRate = totalDispatchedLifetime > 0
            ? Math.Round(((double)totalRepliedLifetime / totalDispatchedLifetime) * 100.0, 2)
            : 0.0;

        double avgOpenRate = totalDispatchedLifetime > 0
            ? Math.Round(((double)totalOpenedLifetime / totalDispatchedLifetime) * 100.0, 2)
            : 0.0;

        double spfPassRate = totalInboxes > 0
            ? Math.Round(((double)inboxes.Count(i => i.HasSpf) / totalInboxes) * 100.0, 1)
            : 100.0;

        double dkimPassRate = totalInboxes > 0
            ? Math.Round(((double)inboxes.Count(i => i.HasDkim) / totalInboxes) * 100.0, 1)
            : 100.0;

        double dmarcPassRate = totalInboxes > 0
            ? Math.Round(((double)inboxes.Count(i => i.HasDmarc) / totalInboxes) * 100.0, 1)
            : 100.0;

        int fullyAligned = inboxes.Count(i => i.HasSpf && i.HasDkim && i.HasDmarc);

        var dnsSummary = new DnsHealthSummary(spfPassRate, dkimPassRate, dmarcPassRate, fullyAligned);

        var providerDist = new ProviderDistributionDto(
            GoogleWorkspaceCount: inboxes.Count(i => i.Provider == MailboxProvider.GoogleWorkspace),
            Microsoft365Count: inboxes.Count(i => i.Provider == MailboxProvider.Microsoft365),
            CustomSmtpCount: inboxes.Count(i => i.Provider == MailboxProvider.CustomSmtp)
        );

        string tier = avgHealthScore >= 90 ? "Optimal" :
                      avgHealthScore >= 75 ? "Good" :
                      avgHealthScore >= 50 ? "Caution" : "Critical";

        // Radar axes
        int authScore = (int)Math.Round((spfPassRate * 0.35) + (dkimPassRate * 0.35) + (dmarcPassRate * 0.30));
        int placementScore = Math.Clamp((int)Math.Round(100.0 - (avgBounceRate * 18.0)), 0, 100);
        int engagementScore = Math.Clamp((int)Math.Round(avgReplyRate * 5.0) + (int)Math.Round(avgOpenRate * 0.5), 0, 100);
        int capacityHeadroom = totalDailyCapacity > 0
            ? Math.Clamp((int)Math.Round(((double)remainingDailyCapacity / totalDailyCapacity) * 100.0), 0, 100)
            : 100;
        int listHygiene = Math.Clamp(100 - (quarantinedInboxes * 20), 0, 100);

        var axes = new List<RadarAxisMetric>
        {
            new("DNS Authentication", authScore, 95),
            new("Inbox Placement", placementScore, 90),
            new("Engagement & Reply", engagementScore > 0 ? engagementScore : 60, 70),
            new("Capacity Headroom", capacityHeadroom, 80),
            new("Hygiene & Safety", listHygiene, 90)
        };

        return new DeliverabilityRadarMetrics(
            TenantId: tenantId,
            TotalInboxes: totalInboxes,
            ActiveInboxes: activeInboxes,
            WarmupInboxes: warmupInboxes,
            QuarantinedInboxes: quarantinedInboxes,
            CoolingInboxes: coolingInboxes,
            TotalDailyCapacity: totalDailyCapacity,
            RemainingDailyCapacity: remainingDailyCapacity,
            TotalSentToday: totalSentToday,
            AverageHealthScore: avgHealthScore,
            AverageBounceRate: avgBounceRate,
            AverageReplyRate: avgReplyRate,
            AverageOpenRate: avgOpenRate,
            TotalDispatchedLifetime: totalDispatchedLifetime,
            TotalBouncedLifetime: totalBouncedLifetime,
            TotalRepliedLifetime: totalRepliedLifetime,
            DnsHealth: dnsSummary,
            ProviderDistribution: providerDist,
            ReputationTier: tier,
            RadarAxes: axes
        );
    }

    public async Task<RotationSimulationResult> SimulateBatchRotationAsync(
        Guid tenantId,
        int batchSize = 100,
        double googleRatio = 0.6,
        double microsoftRatio = 0.3,
        double customRatio = 0.1,
        CancellationToken ct = default)
    {
        var inboxes = await _db.SenderAccounts
            .Where(s => s.TenantId == tenantId && s.IsActive && s.Status != InboxRotationStatus.Quarantined)
            .ToListAsync(ct);

        if (inboxes.Count == 0)
        {
            return new RotationSimulationResult(
                TotalRequestedBatchSize: batchSize,
                SuccessfullyAllocated: 0,
                UnallocatedDueToExhaustion: batchSize,
                AverageJitterSeconds: 0,
                MinJitterSeconds: 0,
                MaxJitterSeconds: 0,
                Allocations: Array.Empty<RotationSimulationItem>()
            );
        }

        // Virtual tracking structure
        var virtualInboxes = inboxes.Select(i => new
        {
            Source = i,
            RemainingQuota = i.RemainingDailyQuota,
            Health = i.HealthScore,
            Provider = i.Provider,
            Email = i.Email,
            Id = i.Id,
            Allocated = 0
        }).Select(x => new VirtualSender(x.Id, x.Email, x.Provider, x.RemainingQuota, x.RemainingQuota, x.Health, 0)).ToList();

        int googleCount = (int)Math.Round(batchSize * googleRatio);
        int msCount = (int)Math.Round(batchSize * microsoftRatio);
        int customCount = batchSize - googleCount - msCount;

        var leadDomains = new List<string>();
        for (int i = 0; i < googleCount; i++) leadDomains.Add("gmail.com");
        for (int i = 0; i < msCount; i++) leadDomains.Add("outlook.com");
        for (int i = 0; i < customCount; i++) leadDomains.Add("target-enterprise.com");

        int successfulAllocations = 0;
        int failedAllocations = 0;
        var jitters = new List<int>();

        foreach (var domain in leadDomains)
        {
            var detectedEsp = DetectRecipientEsp($"test@{domain}");
            var available = virtualInboxes.Where(v => v.RemainingQuota > 0 && v.Health > 0).ToList();

            if (available.Count == 0)
            {
                failedAllocations++;
                continue;
            }

            List<VirtualSender> candidatePool;
            if (detectedEsp.HasValue)
            {
                var matches = available.Where(v => v.Provider == detectedEsp.Value).ToList();
                candidatePool = matches.Count > 0 ? matches : available;
            }
            else
            {
                candidatePool = available;
            }

            double totalWeight = candidatePool.Sum(c => c.RemainingQuota * c.Health);
            if (totalWeight <= 0)
            {
                failedAllocations++;
                continue;
            }

            double roll = Random.Shared.NextDouble() * totalWeight;
            double cumulative = 0;
            VirtualSender picked = candidatePool[0];

            foreach (var cand in candidatePool)
            {
                cumulative += cand.RemainingQuota * cand.Health;
                if (roll <= cumulative)
                {
                    picked = cand;
                    break;
                }
            }

            picked.RemainingQuota--;
            picked.Allocated++;
            successfulAllocations++;
            jitters.Add(CalculateInterSendJitterSeconds());
        }

        var allocations = virtualInboxes.Select(v => new RotationSimulationItem(
            SenderAccountId: v.Id,
            Email: v.Email,
            Provider: v.Provider,
            AllocatedDispatches: v.Allocated,
            AllocationPercentage: successfulAllocations > 0 ? Math.Round(((double)v.Allocated / successfulAllocations) * 100.0, 1) : 0.0,
            PreQuotaRemaining: v.PreQuotaRemaining,
            PostQuotaRemaining: v.RemainingQuota,
            HealthScore: v.Health
        )).ToList();

        return new RotationSimulationResult(
            TotalRequestedBatchSize: batchSize,
            SuccessfullyAllocated: successfulAllocations,
            UnallocatedDueToExhaustion: failedAllocations,
            AverageJitterSeconds: jitters.Count > 0 ? Math.Round(jitters.Average(), 1) : 240,
            MinJitterSeconds: jitters.Count > 0 ? jitters.Min() : 120,
            MaxJitterSeconds: jitters.Count > 0 ? jitters.Max() : 420,
            Allocations: allocations
        );
    }

    public async Task<int> ResetDailyQuotasAsync(CancellationToken ct = default)
    {
        var accounts = await _db.SenderAccounts.ToListAsync(ct);
        foreach (var account in accounts)
        {
            account.ResetDailyQuota();
        }

        await _db.SaveChangesAsync(ct);
        return accounts.Count;
    }

    public async Task<PoolAuditSummary> AuditPoolHealthAsync(CancellationToken ct = default)
    {
        var accounts = await _db.SenderAccounts.ToListAsync(ct);
        int dormantCooling = 0;
        int warmupPromoted = 0;
        int quarantinedRecovered = 0;

        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        var fourteenDaysAgo = DateTime.UtcNow.AddDays(-14);
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

        foreach (var acc in accounts)
        {
            // 1. Check dormant active inboxes: active for > 2 days with zero dispatches
            if (acc.Status == InboxRotationStatus.Active && acc.LastDispatchedAtUtc.HasValue && acc.LastDispatchedAtUtc.Value < twoDaysAgo)
            {
                acc.Status = InboxRotationStatus.Cooling;
                dormantCooling++;
            }

            // 2. Warmup promotion: warmup > 14 days, health >= 90, low bounce rate
            if (acc.Status == InboxRotationStatus.Warmup && acc.CreatedAtUtc <= fourteenDaysAgo && acc.HealthScore >= 90 && acc.BounceRate <= 1.0)
            {
                acc.Status = InboxRotationStatus.Active;
                warmupPromoted++;
            }

            // 3. Staged quarantine recovery: quarantined > 3 days, low bounce rate, DNS pass
            if (acc.Status == InboxRotationStatus.Quarantined && acc.QuarantinedAtUtc.HasValue && acc.QuarantinedAtUtc.Value < threeDaysAgo && acc.HasSpf && acc.HasDkim && acc.HasDmarc)
            {
                acc.Thaw();
                acc.Status = InboxRotationStatus.Cooling; // Staged re-entry
                quarantinedRecovered++;
            }
        }

        if (dormantCooling > 0 || warmupPromoted > 0 || quarantinedRecovered > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new PoolAuditSummary(
            TotalAudited: accounts.Count,
            DormantMarkedCooling: dormantCooling,
            WarmupPromotedToActive: warmupPromoted,
            QuarantinedAutoRecovered: quarantinedRecovered,
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    private class VirtualSender
    {
        public Guid Id { get; }
        public string Email { get; }
        public MailboxProvider Provider { get; }
        public int PreQuotaRemaining { get; }
        public int RemainingQuota { get; set; }
        public int Health { get; }
        public int Allocated { get; set; }

        public VirtualSender(Guid id, string email, MailboxProvider provider, int preQuota, int remainingQuota, int health, int allocated)
        {
            Id = id;
            Email = email;
            Provider = provider;
            PreQuotaRemaining = preQuota;
            RemainingQuota = remainingQuota;
            Health = health;
            Allocated = allocated;
        }
    }
}
