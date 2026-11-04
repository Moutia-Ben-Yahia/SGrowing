using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class TenantUsageAndEntitlementsTests
{
    private readonly SGrowingDbContext _db;
    private readonly TenantUsageMeteringService _metering;

    public TenantUsageAndEntitlementsTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _metering = new TenantUsageMeteringService(_db);
    }

    [Fact]
    public async Task Metering_RecordsUsageAndAggregatesSummary()
    {
        var tenantId = Guid.NewGuid();

        // Record AI tokens, voice minutes, and outbound messages
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 250_000, "Support Turn");
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 150_000, "RAG Synthesis");
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.VoiceMinutes, 45, "Inbound Support Call");
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.OutboundMessages, 500, "WhatsApp Broadcast");

        var summary = await _metering.GetUsageSummaryAsync(tenantId);

        Assert.Equal(400_000, summary.UsedAiTokens);
        Assert.Equal(45, summary.UsedVoiceMinutes);
        Assert.Equal(500, summary.UsedMessages);
        Assert.True(summary.TokenUsagePercent > 0);
    }

    [Fact]
    public async Task Entitlements_EnforcesQuotasAndFlagsWarnings()
    {
        var tenantId = Guid.NewGuid();

        // Setup Starter tier: 100 voice minutes included
        var entitlement = FeatureEntitlement.CreateDefault(tenantId, PlanTier.Starter);
        await _db.FeatureEntitlements.AddAsync(entitlement);
        await _db.SaveChangesAsync();

        // 1. Check under quota: 50 minutes -> allowed
        var check1 = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.VoiceMinutes, 50);
        Assert.True(check1.Allowed);
        Assert.False(check1.RequiresOverageWarning);

        // Record 85 minutes (85% of 100) -> warning flagged
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.VoiceMinutes, 85);
        var check2 = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.VoiceMinutes, 0);
        Assert.True(check2.Allowed);
        Assert.True(check2.RequiresOverageWarning);

        // Check exceeding 100 minutes (need 20 more = 105 total) -> blocked
        var check3 = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.VoiceMinutes, 20);
        Assert.False(check3.Allowed);
        Assert.Contains("exceeded", check3.Message);
    }

    [Fact]
    public async Task GetUsageSummary_CreatesDefaultGrowthEntitlement_WhenNoneExists()
    {
        var tenantId = Guid.NewGuid();

        // Verify database initially has no entitlement for tenant
        var existing = await _db.FeatureEntitlements.FirstOrDefaultAsync(e => e.TenantId == tenantId);
        Assert.Null(existing);

        var summary = await _metering.GetUsageSummaryAsync(tenantId);

        Assert.NotNull(summary);
        Assert.Equal(PlanTier.Growth, summary.PlanTier);
        Assert.True(summary.QuotaAiTokens > 0);

        var saved = await _db.FeatureEntitlements.FirstOrDefaultAsync(e => e.TenantId == tenantId);
        Assert.NotNull(saved);
        Assert.Equal(PlanTier.Growth, saved.PlanTier);
    }

    [Fact]
    public async Task Entitlements_EnterpriseTier_PermitsUnlimitedQuotaUsage()
    {
        var tenantId = Guid.NewGuid();

        var enterpriseEntitlement = FeatureEntitlement.CreateDefault(tenantId, PlanTier.Enterprise);
        await _db.FeatureEntitlements.AddAsync(enterpriseEntitlement);
        await _db.SaveChangesAsync();

        // Request 100,000,000 AI tokens (far beyond standard limits)
        var check = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.AiTokens, 100_000_000);

        Assert.True(check.Allowed);
        Assert.Equal("Within plan quota.", check.Message);
    }

    [Fact]
    public async Task GetUsageSummary_FiltersOutRecordsPriorToBillingCycleStart()
    {
        var tenantId = Guid.NewGuid();

        // Setup subscription with billing cycle starting 5 days ago
        var cycleStart = DateTime.UtcNow.AddDays(-5);
        var sub = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BillingCycleStartUtc = cycleStart,
            PlanTier = PlanTier.Growth
        };
        await _db.TenantSubscriptions.AddAsync(sub);

        // Add older record (10 days ago, prior to cycle)
        var oldRecord = UsageMeterRecord.Create(tenantId, UsageMetricType.AiTokens, 50_000, "Old Month");
        oldRecord.RecordedAtUtc = DateTime.UtcNow.AddDays(-10);
        await _db.UsageMeterRecords.AddAsync(oldRecord);

        // Add current record (2 days ago, within cycle)
        var currentRecord = UsageMeterRecord.Create(tenantId, UsageMetricType.AiTokens, 30_000, "Current Month");
        currentRecord.RecordedAtUtc = DateTime.UtcNow.AddDays(-2);
        await _db.UsageMeterRecords.AddAsync(currentRecord);

        await _db.SaveChangesAsync();

        var summary = await _metering.GetUsageSummaryAsync(tenantId);

        // Only 30,000 within current billing cycle should be counted
        Assert.Equal(30_000, summary.UsedAiTokens);
    }

    [Fact]
    public async Task Entitlements_OutboundMessages_EnforcesWarningAndBlockThresholds()
    {
        var tenantId = Guid.NewGuid();

        // Starter tier has 1,000 messages included
        var starter = FeatureEntitlement.CreateDefault(tenantId, PlanTier.Starter);
        starter.MonthlyIncludedMessages = 1000;
        await _db.FeatureEntitlements.AddAsync(starter);
        await _db.SaveChangesAsync();

        // 1. Safe under quota (400 messages)
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.OutboundMessages, 400);
        var checkSafe = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.OutboundMessages, 100);
        Assert.True(checkSafe.Allowed);
        Assert.False(checkSafe.RequiresOverageWarning);

        // 2. 80%+ threshold warning (850 total projected)
        var checkWarn = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.OutboundMessages, 450);
        Assert.True(checkWarn.Allowed);
        Assert.True(checkWarn.RequiresOverageWarning);
        Assert.Contains("Warning: 80%+", checkWarn.Message);

        // 3. Exceeded quota (1050 total projected)
        var checkBlock = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.OutboundMessages, 650);
        Assert.False(checkBlock.Allowed);
        Assert.Contains("Quota exceeded", checkBlock.Message);
    }

    [Fact]
    public async Task CheckEntitlementAsync_AiTokens_EnforcesWarningAndBlockThresholds()
    {
        var tenantId = Guid.NewGuid();

        // Setup Starter tier: 500,000 AI tokens included
        var starter = FeatureEntitlement.CreateDefault(tenantId, PlanTier.Starter);
        starter.MonthlyIncludedAiTokens = 500_000;
        await _db.FeatureEntitlements.AddAsync(starter);
        await _db.SaveChangesAsync();

        // 1. Record 420,000 tokens (84% of quota) -> warning flagged
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 420_000);
        var checkWarn = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.AiTokens, 0);
        Assert.True(checkWarn.Allowed);
        Assert.True(checkWarn.RequiresOverageWarning);

        // 2. Request 100,000 additional tokens (520,000 total > 500,000) -> blocked
        var checkBlock = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.AiTokens, 100_000);
        Assert.False(checkBlock.Allowed);
        Assert.Contains("Quota exceeded", checkBlock.Message);
    }

    [Fact]
    public async Task RecordUsageAsync_MultipleTenants_IsolatesUsageAggregationsByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _metering.RecordUsageAsync(tenantA, UsageMetricType.VoiceMinutes, 30);
        await _metering.RecordUsageAsync(tenantB, UsageMetricType.VoiceMinutes, 75);

        var summaryA = await _metering.GetUsageSummaryAsync(tenantA);
        var summaryB = await _metering.GetUsageSummaryAsync(tenantB);

        Assert.Equal(30, summaryA.UsedVoiceMinutes);
        Assert.Equal(75, summaryB.UsedVoiceMinutes);
    }
}
