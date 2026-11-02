namespace SGrowing.Api.Modules.SupplyChain.DemandSensing;

public enum WeatherPhenomenon
{
    NormalClear,
    HeatwaveExtreme,
    PrecipitationFlood,
    SiroccoSandstorm,
    MountainSnowFreeze
}

public enum SkuWeatherElasticityCategory
{
    ColdBeveragesAndIce,
    RainGearAndUmbrellas,
    AirPurifiersAndFilters,
    WinterApparelAndHeaters,
    StapleFoodPantry
}

public sealed record LocalWeatherTelemetry(
    string RegionId,
    DateTime ObservedAtUtc,
    double TemperatureCelsius,
    double PrecipitationMmPerHour,
    double RelativeHumidityPercent,
    double WindSpeedKmH,
    WeatherPhenomenon Phenomenon
);

public sealed record SkuDemandProfile(
    string Sku,
    SkuWeatherElasticityCategory Category,
    double BaselineDailyDemandUnits,
    double DemandStdDevDaily,
    int LeadTimeDays,
    double CurrentInventoryUnits
);

public sealed record SensedDemandForecast(
    string Sku,
    string RegionId,
    double BaselineDailyDemand,
    double WeatherDemandMultiplier,
    double SensedDailyDemand,
    double DynamicSafetyStockUnits,
    double ProjectedInventoryDeficitUnits,
    bool StockoutRiskFlagged,
    string WeatherImpactExplanation
);

public sealed record RegionalInventoryTransferRecommendation(
    string RecommendationId,
    string Sku,
    string SourceWarehouseId,
    string TargetWarehouseId,
    int TransferQuantityUnits,
    DateTime RequiredArrivalUtc,
    string TransferUrgency,
    string Rationale
);

public sealed record EvaluateRegionalDemandRequest(
    string RegionId,
    LocalWeatherTelemetry Weather,
    IReadOnlyList<SkuDemandProfile> SkuProfiles
);
