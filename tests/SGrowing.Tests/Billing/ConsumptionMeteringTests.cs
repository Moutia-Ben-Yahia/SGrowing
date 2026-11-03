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
}
