namespace SGrowing.Tests.Agriculture;

using FluentAssertions;
using SGrowing.Api.Modules.Agriculture.PrecisionAgrigenomics;
using Xunit;

public class PrecisionAgrigenomicsTests
{
    private readonly PrecisionAgrigenomicsEngine _engine = new();

    [Fact]
    public void GeneratePrescriptions_VariableCanopyVigor_AppliesVariableRateNitrogen()
    {
        var zones = new List<FarmFieldZone>
        {
            new("ZONE-NORTH", "Winter Wheat", AreaHectares: 10.0, SoilPh: 6.8, SoilMoistureFraction: 0.28, HistoricalYieldTonsPerHa: 7.5),
            new("ZONE-SOUTH", "Winter Wheat", AreaHectares: 15.0, SoilPh: 6.5, SoilMoistureFraction: 0.22, HistoricalYieldTonsPerHa: 7.2)
        };

        var surveys = new List<DroneVegetativeIndices>
        {
            new("ZONE-NORTH", Ndvi: 0.82, Ndre: 0.45, Savi: 0.78, DateTimeOffset.UtcNow), // High vigor
            new("ZONE-SOUTH", Ndvi: 0.42, Ndre: 0.22, Savi: 0.38, DateTimeOffset.UtcNow)  // Chlorotic stress
        };

        var weather = new WeatherObservation(TemperatureCelsius: 24.0, RelativeHumidityPercent: 45.0, SolarRadiationMjPerM2: 22.0, WindSpeedMps: 3.2);
        var request = new AgrigenomicsPrescriptionRequest(zones, surveys, weather, NitrogenPricePerKgEur: 1.50);

        var result = _engine.GeneratePrescriptions(request);

        result.TotalZonesEvaluated.Should().Be(2);
        result.TotalAreaHectares.Should().Be(25.0);
        result.Prescriptions.Should().HaveCount(2);

        var northPrescription = result.Prescriptions.First(p => p.ZoneId == "ZONE-NORTH");
        var southPrescription = result.Prescriptions.First(p => p.ZoneId == "ZONE-SOUTH");

        northPrescription.NitrogenKgPerHa.Should().Be(60.0); // Maintenance
        southPrescription.NitrogenKgPerHa.Should().Be(140.0); // Remediation
        result.TotalNitrogenRequiredKg.Should().Be((10.0 * 60.0) + (15.0 * 140.0)); // 2700 kg
        result.TotalFertilizerCostEur.Should().Be(2700m * 1.50m);
        result.AgroEcologicalAdvice.Should().Contain("Targeted Remediation");
    }

    [Fact]
    public void GeneratePrescriptions_UniformOptimalCanopy_RecommendsMaintenanceDoses()
    {
        var zones = new List<FarmFieldZone>
        {
            new("ZONE-CENTRAL", "Grain Maize", AreaHectares: 20.0, SoilPh: 7.0, SoilMoistureFraction: 0.30, HistoricalYieldTonsPerHa: 11.0)
        };

        var surveys = new List<DroneVegetativeIndices>
        {
            new("ZONE-CENTRAL", Ndvi: 0.88, Ndre: 0.52, Savi: 0.84, DateTimeOffset.UtcNow)
        };

        var weather = new WeatherObservation(TemperatureCelsius: 26.0, RelativeHumidityPercent: 55.0, SolarRadiationMjPerM2: 25.0, WindSpeedMps: 2.0);
        var request = new AgrigenomicsPrescriptionRequest(zones, surveys, weather);

        var result = _engine.GeneratePrescriptions(request);

        var prescription = result.Prescriptions.Should().ContainSingle().Subject;
        prescription.VigorStatus.Should().Contain("Optimal Vigor");
        prescription.NitrogenKgPerHa.Should().Be(60.0);
        result.AgroEcologicalAdvice.Should().Contain("Field Uniformity High");
    }

    [Fact]
    public void GeneratePrescriptions_MissingDroneSurvey_FallsBackToDefaultModerateVigor()
    {
        var zones = new List<FarmFieldZone>
        {
            new("ZONE-REMOTE", "Barley", AreaHectares: 8.0, SoilPh: 6.9, SoilMoistureFraction: 0.25, HistoricalYieldTonsPerHa: 6.0)
        };

        var weather = new WeatherObservation(20.0, 60.0, 18.0, 1.5);
        var request = new AgrigenomicsPrescriptionRequest(zones, DroneSurveys: [], weather);

        var result = _engine.GeneratePrescriptions(request);

        var prescription = result.Prescriptions.Should().ContainSingle().Subject;
        prescription.VigorStatus.Should().Contain("Moderate Vegetative Vigor");
        prescription.NitrogenKgPerHa.Should().Be(100.0);
    }

    [Fact]
    public void GeneratePrescriptions_ExtremeHeatEvapotranspiration_CalculatesElevatedEt0()
    {
        var zones = new List<FarmFieldZone>
        {
            new("ZONE-DESERT", "Durum Wheat", 5.0, 7.4, 0.15, 4.5)
        };

        var hotWeather = new WeatherObservation(TemperatureCelsius: 38.0, RelativeHumidityPercent: 15.0, SolarRadiationMjPerM2: 30.0, WindSpeedMps: 6.0);
        var request = new AgrigenomicsPrescriptionRequest(zones, [], hotWeather);

        var result = _engine.GeneratePrescriptions(request);

        result.Prescriptions[0].EvapotranspirationMmPerDay.Should().BeGreaterThan(5.0);
    }
}
