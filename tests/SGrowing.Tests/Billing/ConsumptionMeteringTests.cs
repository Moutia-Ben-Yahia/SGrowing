using SGrowing.Api.Modules.Billing.ConsumptionMetering;
using Xunit;

namespace SGrowing.Tests.Billing;

public class ConsumptionMeteringTests
{
    private readonly ConsumptionMeteringEngine _engine = new();

    [Fact]
    public void SeededProfile_HasActiveWarningOnHighBurnRate()
    {
        var profile = _engine.GetUsageProfile("tenant_global_ent");

        Assert.NotNull(profile);
        Assert.Equal("GrowthScale Tier", profile.CurrentTier);
        Assert.True(profile.OverQuotaWarningActive);
        Assert.Contains(profile.Metrics, m => m.BurnRatePercentage >= 80.0);
        Assert.True(profile.EstimatedMidCycleInvoiceDollars > 2000m);
    }

    [Fact]
    public void IngestUsage_UpdatesMetricAndOverageCalculation()
    {
        var request = new IngestUsageEventRequest("tenant_global_ent", UsageMetric.VoiceMinutes, 500.0);
        var updated = _engine.IngestUsage(request);

        Assert.NotNull(updated);
        var voice = updated.Metrics.Find(m => m.Metric == UsageMetric.VoiceMinutes);
        Assert.NotNull(voice);
        Assert.Equal(1700.0, voice.CurrentUsage);
        Assert.Equal(85.0, voice.BurnRatePercentage);
    }

    [Fact]
    public void AutoUpgradeTier_ScalesQuotasAndLowersBurnRatePercentage()
    {
        var request = new AutoUpgradeTierRequest("tenant_global_ent", "Enterprise Hyperscale");
        var upgraded = _engine.AutoUpgradeTier(request);

        Assert.Equal("Enterprise Hyperscale", upgraded.CurrentTier);
        var api = upgraded.Metrics.Find(m => m.Metric == UsageMetric.ApiQueries);
        Assert.NotNull(api);
        Assert.True(api.BurnRatePercentage < 50.0); // Burn rate drops from 85% to ~34%
        Assert.False(upgraded.OverQuotaWarningActive);
    }

    [Fact]
    public void GetUsageProfile_NonExistentTenant_ReturnsNull()
    {
        var profile = _engine.GetUsageProfile("tenant_non_existent_404");
        Assert.Null(profile);
    }

    [Fact]
    public void IngestUsage_UnregisteredTenant_InitializesStarterTierAndDefaultQuotas()
    {
        var request = new IngestUsageEventRequest("tenant_new_starter_01", UsageMetric.ApiQueries, 1000.0);
        var profile = _engine.IngestUsage(request);

        Assert.NotNull(profile);
        Assert.Equal("Starter Tier", profile.CurrentTier);
        Assert.Equal("tenant_new_starter_01", profile.TenantId);
        Assert.False(profile.OverQuotaWarningActive);

        var apiMetric = profile.Metrics.Find(m => m.Metric == UsageMetric.ApiQueries);
        Assert.NotNull(apiMetric);
        Assert.Equal(1000.0, apiMetric.CurrentUsage);
        Assert.Equal(100000.0, apiMetric.TierQuotaLimit);
        Assert.Equal(1.0, apiMetric.BurnRatePercentage); // 1,000 / 100,000 = 1%
    }

    [Fact]
    public void IngestUsage_ExceedingQuota_CalculatesAccurateOverageBilling()
    {
        // Starter Tier: Voice minutes quota is 500.0, overage rate is $0.10/min, base price $499
        var tenantId = "tenant_overage_test_02";
        var request = new IngestUsageEventRequest(tenantId, UsageMetric.VoiceMinutes, 700.0); // 200 excess

        var profile = _engine.IngestUsage(request);

        Assert.NotNull(profile);
        var voice = profile.Metrics.Find(m => m.Metric == UsageMetric.VoiceMinutes);
        Assert.NotNull(voice);
        Assert.Equal(700.0, voice.CurrentUsage);
        Assert.Equal(140.0, voice.BurnRatePercentage); // 700 / 500 = 140%
        Assert.True(profile.OverQuotaWarningActive);

        // Base price 499 + (200 excess * $0.10 = $20) = $519.00
        Assert.Equal(519.00m, profile.EstimatedMidCycleInvoiceDollars);
    }

    [Fact]
    public void AutoUpgradeTier_NonExistentTenant_ThrowsKeyNotFoundException()
    {
        var request = new AutoUpgradeTierRequest("tenant_unknown_999", "Enterprise Hyperscale");
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => _engine.AutoUpgradeTier(request));
    }

    [Fact]
    public void IngestUsage_MultipleMetrics_MaintainsIndependentBurnRates()
    {
        var tenantId = "tenant_multi_metric_03";
        _engine.IngestUsage(new IngestUsageEventRequest(tenantId, UsageMetric.WhatsAppMessages, 4500.0));
        var profile = _engine.IngestUsage(new IngestUsageEventRequest(tenantId, UsageMetric.AiTokensMillion, 2.0));

        var wa = profile.Metrics.Find(m => m.Metric == UsageMetric.WhatsAppMessages);
        var ai = profile.Metrics.Find(m => m.Metric == UsageMetric.AiTokensMillion);

        Assert.NotNull(wa);
        Assert.NotNull(ai);
        Assert.Equal(4500.0, wa.CurrentUsage);
        Assert.Equal(90.0, wa.BurnRatePercentage); // 4500 / 5000 = 90%
        Assert.Equal(2.0, ai.CurrentUsage);
        Assert.Equal(20.0, ai.BurnRatePercentage); // 2 / 10 = 20%
        Assert.True(profile.OverQuotaWarningActive); // Triggered by WhatsApp >= 80%
    }
}
