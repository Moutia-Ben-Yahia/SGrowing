namespace SGrowing.Api.Modules.SupplyChain.DemandSensing;

/// <summary>
/// Autonomous demand sensing engine modifying baseline forecasts using real-time
/// meteorological radar, temperature gradients, and weather phenomenon multipliers.
/// </summary>
public sealed class WeatherDrivenDemandSensingEngine
{
    private const double ZScore95 = 1.645; // 95% service level

    public IReadOnlyList<SensedDemandForecast> SenseDemand(EvaluateRegionalDemandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var list = new List<SensedDemandForecast>();

        foreach (var sku in request.SkuProfiles)
        {
            var multiplier = CalculateWeatherMultiplier(sku.Category, request.Weather, out var explanation);
            var sensedDemand = sku.BaselineDailyDemandUnits * multiplier;

            // Dynamic Safety Stock: SS = M_weather * Z * sigma_D * sqrt(L)
            var dynamicSafetyStock = multiplier * ZScore95 * sku.DemandStdDevDaily * Math.Sqrt(Math.Max(1, sku.LeadTimeDays));

            // Lead-time demand = sensedDemand * L + safety stock
            var totalDemandOverLeadTime = (sensedDemand * sku.LeadTimeDays) + dynamicSafetyStock;
            var deficit = Math.Max(0.0, totalDemandOverLeadTime - sku.CurrentInventoryUnits);
            var isStockoutRisk = deficit > 0;

            list.Add(new SensedDemandForecast(
                Sku: sku.Sku,
                RegionId: request.RegionId,
                BaselineDailyDemand: Math.Round(sku.BaselineDailyDemandUnits, 1),
                WeatherDemandMultiplier: Math.Round(multiplier, 2),
                SensedDailyDemand: Math.Round(sensedDemand, 1),
                DynamicSafetyStockUnits: Math.Round(dynamicSafetyStock, 1),
                ProjectedInventoryDeficitUnits: Math.Round(deficit, 1),
                StockoutRiskFlagged: isStockoutRisk,
                WeatherImpactExplanation: explanation
            ));
        }

        return list;
    }

    private static double CalculateWeatherMultiplier(
        SkuWeatherElasticityCategory category,
        LocalWeatherTelemetry weather,
        out string explanation)
    {
        var m = 1.0;
        explanation = "Normal weather conditions; baseline demand model maintained.";

        switch (category)
        {
            case SkuWeatherElasticityCategory.ColdBeveragesAndIce:
                if (weather.Phenomenon == WeatherPhenomenon.HeatwaveExtreme || weather.TemperatureCelsius > 35.0)
                {
                    var excessTemp = Math.Max(0.0, weather.TemperatureCelsius - 30.0);
                    m = 1.5 + (0.05 * excessTemp); // e.g. 40C -> 1.5 + 0.5 = 2.0x (100% surge)
                    explanation = $"HEATWAVE SURGE: Ambient temperature ({weather.TemperatureCelsius:F1}C) triggers +{((m - 1.0) * 100):F0}% beverage velocity.";
                }
                break;

            case SkuWeatherElasticityCategory.RainGearAndUmbrellas:
                if (weather.PrecipitationMmPerHour > 5.0 || weather.Phenomenon == WeatherPhenomenon.PrecipitationFlood)
                {
                    m = 2.8;
                    explanation = $"PRECIPITATION SURGE: Heavy rainfall ({weather.PrecipitationMmPerHour:F1} mm/h) drives +180% demand for rain gear.";
                }
                break;

            case SkuWeatherElasticityCategory.AirPurifiersAndFilters:
                if (weather.Phenomenon == WeatherPhenomenon.SiroccoSandstorm || weather.WindSpeedKmH > 60.0)
                {
                    m = 3.2;
                    explanation = $"SANDSTORM ALERT: Sirocco particulate event triggers +220% demand for air purifiers and filters.";
                }
                break;

            case SkuWeatherElasticityCategory.WinterApparelAndHeaters:
                if (weather.Phenomenon == WeatherPhenomenon.MountainSnowFreeze || weather.TemperatureCelsius < 5.0)
                {
                    m = 2.4;
                    explanation = $"FREEZE WAVE: Sub-5C temperatures drive +140% demand for winter thermal apparel and heaters.";
                }
                else if (weather.TemperatureCelsius > 30.0)
                {
                    m = 0.25; // 75% drop in summer
                    explanation = "SUMMER SLUMP: High temperatures collapse winter apparel demand by -75%.";
                }
                break;

            case SkuWeatherElasticityCategory.StapleFoodPantry:
                if (weather.Phenomenon is WeatherPhenomenon.PrecipitationFlood or WeatherPhenomenon.SiroccoSandstorm)
                {
                    m = 1.4; // 40% panic/buffer buying
                    explanation = "INCLEMENT WEATHER PANIC: Households stock up on essential staples (+40%).";
                }
                break;
        }

        return Math.Max(0.1, m);
    }
}
