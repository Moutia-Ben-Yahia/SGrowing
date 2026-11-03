using SGrowing.Api.Modules.Advertising.WeatherAdTriggers;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class WeatherAdTriggersTests
{
    [Fact]
    public void GetOverview_ReportsActiveRulesAndMetros()
    {
        var engine = new WeatherAdTriggersEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveRulesCount >= 2);
        Assert.True(overview.MonitoredMetrosCount >= 2);
        Assert.True(overview.TriggeredEventsLast24Hours >= 1);
        Assert.True(overview.AverageRoasLiftMultiplier > 1.2);
        Assert.NotEmpty(overview.Rules);
        Assert.NotEmpty(overview.RecentObservations);
        Assert.NotEmpty(overview.RecentExecutions);
    }

    [Fact]
    public void TriggerCampaign_CreatesActiveRuleAndSimulatesExecution()
    {
        var engine = new WeatherAdTriggersEngine();
        var request = new TriggerCampaignRequest(
            "CAMP-BEVERAGE-HOTTEA",
            "FREEZE_BELOW_0C",
            "US_CHICAGO_IL",
            "CREATIVE-HOT-HERBAL-TEA",
            1.55m
        );

        var rule = engine.TriggerCampaign(request);

        Assert.NotNull(rule);
        Assert.StartsWith("WTR-RULE-", rule.RuleId);
        Assert.Equal("US_CHICAGO_IL", rule.GeoTargetMetro);
        Assert.Equal(1.55m, rule.BidMultiplier);
        Assert.True(rule.IsActive);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Rules, r => r.RuleId == rule.RuleId);
        Assert.Contains(overview.RecentExecutions, e => e.RuleId == rule.RuleId);
    }

    [Fact]
    public void RecordWeatherObservation_AppendsTelemetryData()
    {
        var engine = new WeatherAdTriggersEngine();
        var request = new RecordWeatherObservationRequest(
            "US_MIAMI_FL",
            33.5,
            12.0,
            38,
            "SUNNY_HEAT"
        );

        var obs = engine.RecordWeatherObservation(request);

        Assert.NotNull(obs);
        Assert.StartsWith("OBS-", obs.ObservationId);
        Assert.Equal("US_MIAMI_FL", obs.GeoTargetMetro);
        Assert.Equal(33.5, obs.TemperatureCelsius);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentObservations, o => o.ObservationId == obs.ObservationId);
    }
}
