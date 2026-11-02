using FluentAssertions;
using SGrowing.Api.Modules.SupplyChain.DemandSensing;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public sealed class DemandSensingTests
{
    private readonly WeatherDrivenDemandSensingEngine _sensingEngine = new();
    private readonly HyperlocalInventoryStagingCoordinator _stagingCoordinator = new();

    #region Weather Demand Sensing Tests

    [Fact]
    public void SenseDemand_ExtremeHeatwaveInOuargla_SurgesBeveragesAndFlagsStockoutRisk()
    {
        var weather = new LocalWeatherTelemetry(
            RegionId: "DZA-OUARGLA",
            ObservedAtUtc: DateTime.UtcNow,
            TemperatureCelsius: 44.0, // 44C Sahara heatwave
            PrecipitationMmPerHour: 0.0,
            RelativeHumidityPercent: 12.0,
            WindSpeedKmH: 22.0,
            Phenomenon: WeatherPhenomenon.HeatwaveExtreme
        );

        var skuProfiles = new List<SkuDemandProfile>
        {
            // Cold beverages: low stock
            new("SKU-MINERAL-WATER-1.5L", SkuWeatherElasticityCategory.ColdBeveragesAndIce, BaselineDailyDemandUnits: 1000.0, DemandStdDevDaily: 150.0, LeadTimeDays: 2, CurrentInventoryUnits: 1200.0),
            // Winter coats: high stock
            new("SKU-WINTER-PARKA", SkuWeatherElasticityCategory.WinterApparelAndHeaters, BaselineDailyDemandUnits: 50.0, DemandStdDevDaily: 10.0, LeadTimeDays: 5, CurrentInventoryUnits: 500.0)
        };

        var forecasts = _sensingEngine.SenseDemand(new EvaluateRegionalDemandRequest("DZA-OUARGLA", weather, skuProfiles));

        forecasts.Should().HaveCount(2);

        var water = forecasts.First(f => f.Sku == "SKU-MINERAL-WATER-1.5L");
        water.WeatherDemandMultiplier.Should().BeGreaterThanOrEqualTo(2.0, "Extreme heat doubles mineral water velocity.");
        water.SensedDailyDemand.Should().BeGreaterThan(2000.0);
        water.StockoutRiskFlagged.Should().BeTrue();
        water.ProjectedInventoryDeficitUnits.Should().BeGreaterThan(0.0);
        water.WeatherImpactExplanation.Should().Contain("HEATWAVE SURGE");

        var parka = forecasts.First(f => f.Sku == "SKU-WINTER-PARKA");
        parka.WeatherDemandMultiplier.Should().BeLessThan(0.5, "Summer heat collapses winter apparel demand.");
        parka.StockoutRiskFlagged.Should().BeFalse();
    }

    [Fact]
    public void SenseDemand_HeavyRainfall_SurgesRainGear()
    {
        var weather = new LocalWeatherTelemetry(
            RegionId: "DZA-ALGIERS-COAST",
            ObservedAtUtc: DateTime.UtcNow,
            TemperatureCelsius: 16.0,
            PrecipitationMmPerHour: 28.0, // Heavy coastal storm
            RelativeHumidityPercent: 95.0,
            WindSpeedKmH: 45.0,
            Phenomenon: WeatherPhenomenon.PrecipitationFlood
        );

        var skuProfiles = new List<SkuDemandProfile>
        {
            new("SKU-COMPACT-UMBRELLA", SkuWeatherElasticityCategory.RainGearAndUmbrellas, BaselineDailyDemandUnits: 80.0, DemandStdDevDaily: 20.0, LeadTimeDays: 3, CurrentInventoryUnits: 150.0)
        };

        var forecasts = _sensingEngine.SenseDemand(new EvaluateRegionalDemandRequest("DZA-ALGIERS-COAST", weather, skuProfiles));

        var umbrella = forecasts[0];
        umbrella.WeatherDemandMultiplier.Should().Be(2.8);
        umbrella.StockoutRiskFlagged.Should().BeTrue();
        umbrella.WeatherImpactExplanation.Should().Contain("PRECIPITATION SURGE");
    }

    #endregion

    #region Hyperlocal Inventory Staging Tests

    [Fact]
    public void CoordinateStaging_StockoutRisks_Generates48HourTransferOrders()
    {
        var forecasts = new List<SensedDemandForecast>
        {
            new("SKU-ELECTROLYTE-POWDER", "DZA-GHARDAIA", 200.0, 2.2, 440.0, 150.0, 450.0, StockoutRiskFlagged: true, "HEATWAVE SURGE")
        };

        var recommendations = _stagingCoordinator.CoordinateStaging(forecasts, "WH-ALGIERS-CENTRAL", "WH-GHARDAIA");

        recommendations.Should().ContainSingle();
        var rec = recommendations[0];
        rec.Sku.Should().Be("SKU-ELECTROLYTE-POWDER");
        rec.TransferQuantityUnits.Should().BeGreaterThan(450); // with buffer
        rec.TransferUrgency.Should().Be("CRITICAL_48H");
        rec.RequiredArrivalUtc.Should().BeBefore(DateTime.UtcNow.AddHours(49));
    }

    #endregion
}
