using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class MultiInboxRotationTests
{
    private readonly SGrowingDbContext _db;
    private readonly MultiInboxRotationService _service;

    public MultiInboxRotationTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new MultiInboxRotationService(_db);
    }

    [Fact]
    public async Task RegisterSenderAccount_ValidInput_RegistersAccountWithHealthyDefaults()
    {
        var tenantId = Guid.NewGuid();
        var dto = new RegisterSenderInboxDto(
            Email: "alex@getgrowing.io",
            DisplayName: "Alex Growth",
            Provider: MailboxProvider.GoogleWorkspace,
            DailyQuota: 35,
            WarmupEnabled: true,
            WarmupRampDailyIncrement: 4
        );

        var registered = await _service.RegisterSenderAccountAsync(tenantId, dto);

        Assert.NotNull(registered);
        Assert.Equal("alex@getgrowing.io", registered.Email);
        Assert.Equal("Alex Growth", registered.DisplayName);
        Assert.Equal(35, registered.DailyQuota);
        Assert.Equal(0, registered.SentTodayCount);
        Assert.True(registered.HealthScore >= 90);
        Assert.Equal(InboxRotationStatus.Warmup, registered.Status);
        Assert.True(registered.HasSpf);
        Assert.True(registered.HasDkim);
        Assert.True(registered.HasDmarc);
        Assert.Equal("getgrowing.io", registered.Domain);
    }

    [Fact]
    public async Task CapacityWeightedRotation_Fairness_DistributesAccordingToRemainingCapacityAndHealth()
    {
        var tenantId = Guid.NewGuid();

        // Inbox A: Remaining Quota = 50, Health = 100 -> Weight = 5000
        var inboxA = SenderAccount.Create(tenantId, "high-cap@sgrowing.io", "High Cap", MailboxProvider.CustomSmtp, 50);
        inboxA.HealthScore = 100;

        // Inbox B: Remaining Quota = 10, Health = 50 -> Weight = 500
        var inboxB = SenderAccount.Create(tenantId, "low-cap@sgrowing.io", "Low Cap", MailboxProvider.CustomSmtp, 50);
        inboxB.HealthScore = 50;
        for (int i = 0; i < 40; i++) inboxB.RecordSend(); // 10 remaining

        _db.SenderAccounts.AddRange(inboxA, inboxB);
        await _db.SaveChangesAsync();

        // Run simulation with 300 selections (simulation resets in-memory virtual quota)
        var sim = await _service.SimulateBatchRotationAsync(tenantId, batchSize: 50, googleRatio: 0, microsoftRatio: 0, customRatio: 1.0);

        Assert.Equal(50, sim.SuccessfullyAllocated);
        var allocA = sim.Allocations.First(a => a.SenderAccountId == inboxA.Id);
        var allocB = sim.Allocations.First(a => a.SenderAccountId == inboxB.Id);

        // Inbox A had 50 quota, Inbox B had 10 quota. A should have received significantly more allocations than B.
        Assert.True(allocA.AllocatedDispatches > allocB.AllocatedDispatches);
        Assert.True(allocA.AllocatedDispatches >= 40);
        Assert.True(allocB.AllocatedDispatches <= 10);
    }

    [Fact]
    public async Task CapacityWeightedRotation_QuotaExhaustion_ExcludesExhaustedInboxesAndThrowsWhenAllExhausted()
    {
        var tenantId = Guid.NewGuid();

        // Inbox with only 1 send left
        var inbox1 = SenderAccount.Create(tenantId, "inbox1@sgrowing.io", "Inbox 1", MailboxProvider.CustomSmtp, 1);
        _db.SenderAccounts.Add(inbox1);
        await _db.SaveChangesAsync();

        // First select should succeed
        var result1 = await _service.SelectNextSenderAsync(tenantId, "prospect@example.com");
        Assert.Equal(inbox1.Id, result1.SelectedSender.Id);
        Assert.Equal(1, inbox1.SentTodayCount);
        Assert.Equal(0, inbox1.RemainingDailyQuota);

        // Next select should throw because all inboxes are exhausted
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SelectNextSenderAsync(tenantId, "prospect2@example.com"));

        Assert.Contains("exhausted daily quota", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterSendJitter_CalculatesHumanLikeDelay_WithinExpectedBounds()
    {
        var jitters = new List<int>();
        for (int i = 0; i < 1000; i++)
        {
            int jitter = _service.CalculateInterSendJitterSeconds(mean: 240, stdDev: 45, min: 120, max: 420);
            Assert.InRange(jitter, 120, 420);
            jitters.Add(jitter);
        }

        double mean = jitters.Average();
        // With 1000 samples, mean should be well within [230, 250]
        Assert.InRange(mean, 230.0, 250.0);
    }

    [Fact]
    public async Task EspMatching_PrefersMatchingProvider_ForGoogleAndMicrosoftRecipients()
    {
        var tenantId = Guid.NewGuid();

        var googleSender = SenderAccount.Create(tenantId, "rep@googleworkspace.com", "Google Rep", MailboxProvider.GoogleWorkspace, 50);
        var msSender = SenderAccount.Create(tenantId, "rep@office365.com", "MS Rep", MailboxProvider.Microsoft365, 50);
        var smtpSender = SenderAccount.Create(tenantId, "rep@customdomain.com", "SMTP Rep", MailboxProvider.CustomSmtp, 50);

        _db.SenderAccounts.AddRange(googleSender, msSender, smtpSender);
        await _db.SaveChangesAsync();

        // 1. Google recipient -> Should match Google sender
        var googleResult = await _service.SelectNextSenderAsync(tenantId, "lead@gmail.com");
        Assert.Equal(MailboxProvider.GoogleWorkspace, googleResult.SelectedSender.Provider);
        Assert.Equal("GoogleWorkspace-to-GoogleWorkspace", googleResult.EspMatchStrategy);
        Assert.Equal("gmail.com", googleResult.RecipientDomain);

        // 2. Microsoft recipient -> Should match Microsoft sender
        var msResult = await _service.SelectNextSenderAsync(tenantId, "buyer@outlook.com");
        Assert.Equal(MailboxProvider.Microsoft365, msResult.SelectedSender.Provider);
        Assert.Equal("Microsoft365-to-Microsoft365", msResult.EspMatchStrategy);
        Assert.Equal("outlook.com", msResult.RecipientDomain);

        // 3. Generic domain -> Selects across pool
        var genericResult = await _service.SelectNextSenderAsync(tenantId, "contact@independent-corp.com");
        Assert.NotNull(genericResult.SelectedSender);
        Assert.Equal("CapacityWeightedGeneric", genericResult.EspMatchStrategy);
    }

    [Fact]
    public async Task EspMatching_FallsBackToPool_WhenMatchingEspExhausted()
    {
        var tenantId = Guid.NewGuid();

        // Google sender has 0 quota left
        var googleSender = SenderAccount.Create(tenantId, "rep@googleworkspace.com", "Google Rep", MailboxProvider.GoogleWorkspace, 1);
        googleSender.RecordSend(); // quota now exhausted

        // Microsoft sender has plenty of quota
        var msSender = SenderAccount.Create(tenantId, "rep@office365.com", "MS Rep", MailboxProvider.Microsoft365, 50);

        _db.SenderAccounts.AddRange(googleSender, msSender);
        await _db.SaveChangesAsync();

        // Outbound to Gmail recipient: since Google sender is exhausted, must fall back gracefully to available MS sender
        var result = await _service.SelectNextSenderAsync(tenantId, "prospect@gmail.com");
        Assert.Equal(msSender.Id, result.SelectedSender.Id);
        Assert.Equal("CapacityWeightedFallback", result.EspMatchStrategy);
    }

    [Fact]
    public async Task AutomaticQuarantine_TriggersWhenBounceRateExceedsThreshold()
    {
        var tenantId = Guid.NewGuid();
        var account = SenderAccount.Create(tenantId, "sender@growthfast.com", "Fast Sender", MailboxProvider.CustomSmtp, 100);

        // Dispatch 97 successful emails
        for (int i = 0; i < 97; i++)
        {
            account.RecordSend();
            account.RecordDispatchEvent(wasDelivered: true, wasBounced: false, wasReplied: false);
        }

        Assert.Equal(InboxRotationStatus.Active, account.Status);
        Assert.True(account.CanSendToday());

        // Now record 3 bounces (3 / 100 = 3.0% > 2.5% safety threshold)
        for (int i = 0; i < 3; i++)
        {
            account.RecordSend();
            account.RecordDispatchEvent(wasDelivered: false, wasBounced: true, wasReplied: false);
        }

        Assert.Equal(100, account.TotalDispatched);
        Assert.Equal(3, account.TotalBounced);
        Assert.Equal(3.0, account.BounceRate);
        Assert.Equal(InboxRotationStatus.Quarantined, account.Status);
        Assert.False(account.CanSendToday());
        Assert.NotNull(account.QuarantineReason);
        Assert.Contains("exceeded safety threshold", account.QuarantineReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(account.HealthScore <= 40);
    }

    [Fact]
    public async Task ManualQuarantineAndThaw_TransitionsStateAndExcludesFromSelection()
    {
        var tenantId = Guid.NewGuid();
        var account = SenderAccount.Create(tenantId, "manual@sgrowing.io", "Manual Test", MailboxProvider.GoogleWorkspace, 40);
        _db.SenderAccounts.Add(account);
        await _db.SaveChangesAsync();

        // 1. Manually Quarantine
        var quarantined = await _service.QuarantineAccountAsync(tenantId, account.Id, "Administrative audit requested");
        Assert.Equal(InboxRotationStatus.Quarantined, quarantined.Status);
        Assert.Equal("Administrative audit requested", quarantined.QuarantineReason);
        Assert.NotNull(quarantined.QuarantinedAtUtc);
        Assert.False(quarantined.CanSendToday());

        // Attempting to select should fail since it's the only inbox and it is quarantined
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SelectNextSenderAsync(tenantId, "lead@acme.com"));

        // 2. Thaw inbox
        var thawed = await _service.ThawAccountAsync(tenantId, account.Id);
        Assert.NotEqual(InboxRotationStatus.Quarantined, thawed.Status);
        Assert.Null(thawed.QuarantineReason);
        Assert.NotNull(thawed.ThawedAtUtc);
        Assert.True(thawed.CanSendToday());

        // Selection succeeds again
        var selection = await _service.SelectNextSenderAsync(tenantId, "lead@acme.com");
        Assert.Equal(account.Id, selection.SelectedSender.Id);
    }

    [Fact]
    public async Task MidnightDailyCounterReset_ResetsSentTodayToZero()
    {
        var tenantId = Guid.NewGuid();
        var acc1 = SenderAccount.Create(tenantId, "acc1@sgrowing.io", "Acc 1", MailboxProvider.GoogleWorkspace, 50);
        var acc2 = SenderAccount.Create(tenantId, "acc2@sgrowing.io", "Acc 2", MailboxProvider.Microsoft365, 50);

        acc1.RecordSend();
        acc1.RecordSend();
        acc2.RecordSend();

        _db.SenderAccounts.AddRange(acc1, acc2);
        await _db.SaveChangesAsync();

        Assert.Equal(2, acc1.SentTodayCount);
        Assert.Equal(1, acc2.SentTodayCount);

        // Execute midnight reset
        int resetCount = await MultiInboxDispatchDaemon.ResetMidnightCountersAsync(_db);

        Assert.Equal(2, resetCount);
        Assert.Equal(0, acc1.SentTodayCount);
        Assert.Equal(0, acc2.SentTodayCount);
        Assert.Equal(50, acc1.RemainingDailyQuota);
        Assert.Equal(50, acc2.RemainingDailyQuota);
    }

    [Fact]
    public async Task DeliverabilityRadar_AggregatesAccurateTenantMetrics()
    {
        var tenantId = Guid.NewGuid();
        var googleAcc = SenderAccount.Create(tenantId, "google@sgrowing.io", "Google 1", MailboxProvider.GoogleWorkspace, 40);
        var msAcc = SenderAccount.Create(tenantId, "ms@sgrowing.io", "MS 1", MailboxProvider.Microsoft365, 30);
        var quarantinedAcc = SenderAccount.Create(tenantId, "bad@sgrowing.io", "Bad 1", MailboxProvider.CustomSmtp, 20);
        quarantinedAcc.Quarantine("High bounce test");

        _db.SenderAccounts.AddRange(googleAcc, msAcc, quarantinedAcc);
        await _db.SaveChangesAsync();

        var radar = await _service.GetDeliverabilityRadarAsync(tenantId);

        Assert.Equal(tenantId, radar.TenantId);
        Assert.Equal(3, radar.TotalInboxes);
        Assert.Equal(1, radar.QuarantinedInboxes);
        Assert.Equal(90, radar.TotalDailyCapacity); // 40 + 30 + 20
        Assert.Equal(70, radar.RemainingDailyCapacity); // Quarantined excluded: 40 + 30
        Assert.Equal(1, radar.ProviderDistribution.GoogleWorkspaceCount);
        Assert.Equal(1, radar.ProviderDistribution.Microsoft365Count);
        Assert.Equal(1, radar.ProviderDistribution.CustomSmtpCount);
        Assert.Equal(5, radar.RadarAxes.Count);
        Assert.Contains(radar.RadarAxes, a => a.AxisName == "DNS Authentication");
    }

    [Fact]
    public async Task DaemonAuditPoolHealth_DetectsDormantInboxesAndPromotesWarmup()
    {
        var tenantId = Guid.NewGuid();

        // 1. Dormant Active Account: Last dispatched 3 days ago
        var dormantAcc = SenderAccount.Create(tenantId, "dormant@sgrowing.io", "Dormant", MailboxProvider.GoogleWorkspace, 30);
        dormantAcc.Status = InboxRotationStatus.Active;
        dormantAcc.LastDispatchedAtUtc = DateTime.UtcNow.AddDays(-3);

        // 2. Matured Warmup Account: Created 16 days ago, health 95, bounce rate 0%
        var warmupAcc = SenderAccount.Create(tenantId, "warmup@sgrowing.io", "Warmup", MailboxProvider.Microsoft365, 35);
        warmupAcc.Status = InboxRotationStatus.Warmup;
        warmupAcc.CreatedAtUtc = DateTime.UtcNow.AddDays(-16);
        warmupAcc.HealthScore = 95;

        _db.SenderAccounts.AddRange(dormantAcc, warmupAcc);
        await _db.SaveChangesAsync();

        var audit = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(_db);

        Assert.Equal(2, audit.TotalInboxesAudited);
        Assert.Equal(1, audit.DormantInboxesCooled);
        Assert.Equal(1, audit.WarmupAccountsPromoted);

        Assert.Equal(InboxRotationStatus.Cooling, dormantAcc.Status);
        Assert.Equal(InboxRotationStatus.Active, warmupAcc.Status);
    }

    [Fact]
    public async Task EdgeCase_ZeroHealthAccount_ExcludedFromRotation()
    {
        var tenantId = Guid.NewGuid();
        var zeroHealthAcc = SenderAccount.Create(tenantId, "burned@sgrowing.io", "Burned", MailboxProvider.GoogleWorkspace, 50);
        zeroHealthAcc.HealthScore = 0; // Burned domain

        var healthyAcc = SenderAccount.Create(tenantId, "healthy@sgrowing.io", "Healthy", MailboxProvider.GoogleWorkspace, 50);
        healthyAcc.HealthScore = 95;

        _db.SenderAccounts.AddRange(zeroHealthAcc, healthyAcc);
        await _db.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            var res = await _service.SelectNextSenderAsync(tenantId, "prospect@gmail.com");
            Assert.Equal(healthyAcc.Id, res.SelectedSender.Id);
        }
    }
}
