using SGrowing.Api.Modules.Aerospace.SolarCmeShockArrival;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class SolarCmeShockArrivalTests
{
    private readonly SolarCmeShockEngine _engine = new();

    [Fact]
    public void AnalyzeShock_NominalHaloCme_ReturnsDefenseReady()
    {
        var telemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "CME-SOLAR-2026-X1",
            ObservingSpacecraft = "SolarOrbiter_EUI",
            EuvDopplerLiftOffVelocityKmPerS = 880.0,
            BackgroundSolarWindSpeedKmPerS = 400.0,
            AerodynamicDragDecelerationCoeff = 1.45e-7,
            CoronalMassEjectionHalfWidthDegrees = 52.0,
            PredictedGeomagneticDstIndexNanoTesla = -110.0, // Moderate storm
            SolarEnergeticProtonFluxPfu = 42.0,
            ShockTransitDistanceAu = 1.0
        };

        var result = _engine.AnalyzeShock(telemetry);

        Assert.Equal("CME-SOLAR-2026-X1", result.EventIdentifier);
        Assert.True(result.IsDefenseGridPreparednessReady);
        Assert.True(result.IsEarthDirectedGeoeffective);
        Assert.False(result.IsSevereSuperstormPredicted);
        Assert.False(result.IsProtonRadiationHazardCritical);
        Assert.True(result.CalculatedTransitTimeHours >= 30.0 && result.CalculatedTransitTimeHours <= 65.0);
        Assert.True(result.ArrivalSpeedAtTargetKmPerS >= 400.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeShock_SevereCarringtonClassStorm_TriggersSuperstormAlert()
    {
        var telemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "CME-CARRINGTON-CLASS",
            PredictedGeomagneticDstIndexNanoTesla = -285.0 // Severe < -150 nT
        };

        var result = _engine.AnalyzeShock(telemetry);

        Assert.True(result.IsSevereSuperstormPredicted);
        Assert.False(result.IsDefenseGridPreparednessReady);
        Assert.Contains("SEVERE_GEOMAGNETIC_SUPERSTORM_RISK", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeShock_UltraFastEjection_TriggersFastShockAlert()
    {
        var telemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "CME-ULTRA-FAST",
            EuvDopplerLiftOffVelocityKmPerS = 1950.0 // > 1400 km/s
        };

        var result = _engine.AnalyzeShock(telemetry);

        Assert.Contains("HIGH_VELOCITY_FAST_SHOCK_COMPRESSION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeShock_HighProtonFlux_TriggersRadiationSurge()
    {
        var telemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "CME-PROTON-STORM",
            SolarEnergeticProtonFluxPfu = 450.0 // > 100 pfu
        };

        var result = _engine.AnalyzeShock(telemetry);

        Assert.True(result.IsProtonRadiationHazardCritical);
        Assert.Contains("SOLAR_ENERGETIC_PARTICLE_RADIATION_SURGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeShock_TransitTime_DecreasesWithLiftOffVelocity()
    {
        var slowTelemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "SLOW-CME",
            EuvDopplerLiftOffVelocityKmPerS = 650.0
        };

        var fastTelemetry = new SolarCmeShockTelemetry
        {
            EventIdentifier = "FAST-CME",
            EuvDopplerLiftOffVelocityKmPerS = 1600.0
        };

        var slowRes = _engine.AnalyzeShock(slowTelemetry);
        var fastRes = _engine.AnalyzeShock(fastTelemetry);

        Assert.True(slowRes.CalculatedTransitTimeHours > fastRes.CalculatedTransitTimeHours);
    }
}
