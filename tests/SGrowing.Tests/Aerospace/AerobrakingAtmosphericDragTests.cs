using SGrowing.Api.Modules.Aerospace.AerobrakingAtmosphericDrag;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class AerobrakingAtmosphericDragTests
{
    private readonly AerobrakingDragEngine _engine = new();

    [Fact]
    public void AnalyzeDragPass_NominalMarsPass_ReturnsGoForEntry()
    {
        var telemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "MRO-PASS-1024",
            TargetPlanet = "Mars",
            PeriapsisAltitudeKm = 126.0,
            AtmosphericDensityKgPerM3 = 4.2e-9,
            DustStormDensitySurgeFactor = 1.10,
            SpacecraftVelocityAtPeriapsisKmPerS = 4.75,
            SpacecraftDragAreaM2 = 32.0,
            SpacecraftMassKg = 1200.0,
            PeakSolarArrayTemperatureCelsius = 132.0,
            AerodynamicBendingTorqueNewtonMeters = 30.0
        };

        var result = _engine.AnalyzeDragPass(telemetry);

        Assert.Equal("MRO-PASS-1024", result.PassIdentifier);
        Assert.True(result.IsPassGoForAtmosphericEntry);
        Assert.True(result.IsPeriapsisCorridorSafe);
        Assert.True(result.IsThermalLoadWithinLimits);
        Assert.True(result.IsStructuralAeroTorqueSafe);
        Assert.True(result.CalculatedPassDeltaVMetersPerSecond > 0.0);
        Assert.True(result.OrbitalPeriodDecayMinutesPerPass > 0.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDragPass_SevereDustStorm_TriggersSurgeAlert()
    {
        var telemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "PASS-DUST-STORM",
            DustStormDensitySurgeFactor = 2.45 // Exceeds 1.80
        };

        var result = _engine.AnalyzeDragPass(telemetry);

        Assert.False(result.IsPassGoForAtmosphericEntry);
        Assert.Contains("ATMOSPHERIC_DUST_STORM_DENSITY_SURGE", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeDragPass_HighAeroTorque_TriggersMomentExceedance()
    {
        var telemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "PASS-TORQUE-ALERT",
            AerodynamicBendingTorqueNewtonMeters = 58.0 // Exceeds 45 N.m
        };

        var result = _engine.AnalyzeDragPass(telemetry);

        Assert.False(result.IsStructuralAeroTorqueSafe);
        Assert.Contains("SOLAR_ARRAY_AERODYNAMIC_MOMENT_EXCEEDANCE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDragPass_LowPeriapsis_TriggersCorridorDecay()
    {
        var telemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "PASS-DECAY-CORRIDOR",
            PeriapsisAltitudeKm = 108.0 // Below 115 km
        };

        var result = _engine.AnalyzeDragPass(telemetry);

        Assert.False(result.IsPeriapsisCorridorSafe);
        Assert.Contains("PERIAPSIS_ALTITUDE_CORRIDOR_DECAY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDragPass_DeltaVAndPeriodDecay_ScaleWithDensity()
    {
        var lowDensityTelemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "PASS-LOW-RHO",
            AtmosphericDensityKgPerM3 = 2.0e-9
        };

        var highDensityTelemetry = new AerobrakingDragTelemetry
        {
            PassIdentifier = "PASS-HIGH-RHO",
            AtmosphericDensityKgPerM3 = 6.0e-9
        };

        var lowRes = _engine.AnalyzeDragPass(lowDensityTelemetry);
        var highRes = _engine.AnalyzeDragPass(highDensityTelemetry);

        Assert.True(highRes.CalculatedPassDeltaVMetersPerSecond > lowRes.CalculatedPassDeltaVMetersPerSecond);
        Assert.True(highRes.OrbitalPeriodDecayMinutesPerPass > lowRes.OrbitalPeriodDecayMinutesPerPass);
    }
}
