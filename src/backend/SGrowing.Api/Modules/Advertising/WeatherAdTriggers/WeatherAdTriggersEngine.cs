using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.WeatherAdTriggers;

public class WeatherAdTriggersEngine
{
    private readonly ConcurrentDictionary<string, WeatherTriggerRule> _rules = new();
    private readonly ConcurrentBag<WeatherObservation> _observations = new();
    private readonly ConcurrentBag<WeatherTriggeredExecution> _executions = new();

    public WeatherAdTriggersEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var r1 = new WeatherTriggerRule(
            "WTR-RULE-001",
            "CAMP-BEVERAGE-COLDBREW",
            "HEATWAVE_ABOVE_32C",
            "US_AUSTIN_TX",
            "CREATIVE-COLD-BREW-ICE-HERO",
            1.45m,
            true
        );

        var r2 = new WeatherTriggerRule(
            "WTR-RULE-002",
            "CAMP-RIDEHAIL-SURGE",
            "RAIN_PRECIPITATION_ABOVE_80PCT",
            "UK_LONDON_METRO",
            "CREATIVE-RAIN-CAB-DISPATCH",
            1.60m,
            true
        );

        _rules[r1.RuleId] = r1;
        _rules[r2.RuleId] = r2;

        _observations.Add(new WeatherObservation(
            "OBS-001",
            "US_AUSTIN_TX",
            34.8,
            5.0,
            42,
            "SUNNY_HEAT",
            DateTime.UtcNow.AddMinutes(-30)
        ));

        _observations.Add(new WeatherObservation(
            "OBS-002",
            "UK_LONDON_METRO",
            13.2,
            92.0,
            24,
            "HEAVY_RAIN",
            DateTime.UtcNow.AddMinutes(-15)
        ));

        _executions.Add(new WeatherTriggeredExecution(
            "EXEC-WTR-001",
            "WTR-RULE-001",
            "US_AUSTIN_TX",
            "CREATIVE-COLD-BREW-ICE-HERO",
            1.45m,
            28400,
            DateTime.UtcNow.AddMinutes(-25)
        ));
    }

    public WeatherTriggersOverview GetOverview()
    {
        var rulesList = _rules.Values.ToList();
        var obsList = _observations.ToList();
        var execList = _executions.ToList();

        var metros = rulesList.Select(r => r.GeoTargetMetro)
            .Concat(obsList.Select(o => o.GeoTargetMetro))
            .Distinct()
            .Count();

        return new WeatherTriggersOverview(
            rulesList.Count(r => r.IsActive),
            metros,
            execList.Count,
            1.42, // +42% ROAS lift benchmark
            rulesList,
            obsList,
            execList
        );
    }

    public WeatherTriggerRule TriggerCampaign(TriggerCampaignRequest request)
    {
        var ruleId = $"WTR-RULE-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var rule = new WeatherTriggerRule(
            ruleId,
            request.CampaignId,
            request.ConditionType,
            request.GeoTargetMetro,
            request.TargetedVariantCreativeId,
            request.BidMultiplier,
            true
        );

        _rules[ruleId] = rule;

        // Automatically simulate execution
        _executions.Add(new WeatherTriggeredExecution(
            $"EXEC-WTR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            ruleId,
            request.GeoTargetMetro,
            request.TargetedVariantCreativeId,
            request.BidMultiplier,
            1500,
            DateTime.UtcNow
        ));

        return rule;
    }

    public WeatherObservation RecordWeatherObservation(RecordWeatherObservationRequest request)
    {
        var obsId = $"OBS-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var obs = new WeatherObservation(
            obsId,
            request.GeoTargetMetro,
            request.TemperatureCelsius,
            request.PrecipitationProbabilityPct,
            request.AirQualityIndexAqi,
            request.WeatherConditionSummary,
            DateTime.UtcNow
        );

        _observations.Add(obs);
        return obs;
    }
}
