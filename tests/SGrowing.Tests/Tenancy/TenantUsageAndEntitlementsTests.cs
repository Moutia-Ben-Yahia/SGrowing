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
}
