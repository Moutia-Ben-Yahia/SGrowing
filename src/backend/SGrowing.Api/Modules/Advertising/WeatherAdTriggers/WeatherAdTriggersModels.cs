namespace SGrowing.Api.Modules.Advertising.WeatherAdTriggers;

public record WeatherTriggerRule(
    string RuleId,
    string CampaignId,
    string ConditionType, // HEATWAVE_ABOVE_32C, RAIN_PRECIPITATION_ABOVE_80PCT, FREEZE_BELOW_0C, SEVERE_AQI_HAZARD
    string GeoTargetMetro,
    string TargetedVariantCreativeId,
    decimal BidMultiplier, // e.g. 1.5x during heatwave
    bool IsActive
);

public record WeatherObservation(
    string ObservationId,
    string GeoTargetMetro,
    double TemperatureCelsius,
    double PrecipitationProbabilityPct,
    int AirQualityIndexAqi,
    string WeatherConditionSummary, // SUNNY_HEAT, HEAVY_RAIN, SLEET_SNOW, SMOKE_HAZE
    DateTime ObservedAt
);

public record WeatherTriggeredExecution(
    string ExecutionId,
    string RuleId,
    string GeoTargetMetro,
    string ActivatedVariantCreativeId,
    decimal AppliedBidMultiplier,
    int TriggeredImpressionsCount,
    DateTime ExecutedAt
);

public record WeatherTriggersOverview(
    int ActiveRulesCount,
    int MonitoredMetrosCount,
    int TriggeredEventsLast24Hours,
    double AverageRoasLiftMultiplier,
    List<WeatherTriggerRule> Rules,
    List<WeatherObservation> RecentObservations,
    List<WeatherTriggeredExecution> RecentExecutions
);

public record TriggerCampaignRequest(
    string CampaignId,
    string ConditionType,
    string GeoTargetMetro,
    string TargetedVariantCreativeId,
    decimal BidMultiplier
);

public record RecordWeatherObservationRequest(
    string GeoTargetMetro,
    double TemperatureCelsius,
    double PrecipitationProbabilityPct,
    int AirQualityIndexAqi,
    string WeatherConditionSummary
);
