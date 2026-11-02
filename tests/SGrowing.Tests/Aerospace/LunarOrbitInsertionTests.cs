using SGrowing.Api.Modules.Aerospace.LunarOrbitInsertionBallisticCapture;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class LunarOrbitInsertionTests
{
    private readonly LunarOrbitInsertionEngine _engine = new();

    [Fact]
    public void AnalyzeInsertion_NominalBallisticCapture_ReturnsGoForBurn()
    {
        var telemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "ARTEMIS-BLT-001",
            TransferProfileType = "LowEnergyBallisticLunarTransfer_WSB",
            JacobiEnergyConstant = 3.00082,
            OsculatingMoonTwoBodyEnergyKm2PerS2 = -0.092, // Captured
            PeriluneAltitudeKm = 115.0,
            PlannedInsertionDeltaVMetersPerSecond = 620.0,
            PropellantMarginPercent = 20.0,
            BurnExecutionErrorMetersPerSecond = 0.05,
            SolarTidalPerturbationEccentricityDriftPerOrbit = 0.0010
        };

        var result = _engine.AnalyzeInsertion(telemetry);

        Assert.Equal("ARTEMIS-BLT-001", result.MissionTrajectoryId);
        Assert.True(result.IsLunarInsertionGoForBurn);
        Assert.True(result.IsBallisticallyCaptured);
        Assert.True(result.IsPeriluneAltitudeSafe);
        Assert.True(result.IsDeltaVExecutionAccurate);
        Assert.True(result.DeltaVSavingsVsDirectMetersPerSecond >= 300.0);
        Assert.True(result.CalculatedCaptureStabilityMargin >= 2.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeInsertion_HyperbolicEnergy_TriggersFlybyEscape()
    {
        var telemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "MISSION-FLYBY-ESCAPE",
            OsculatingMoonTwoBodyEnergyKm2PerS2 = 0.12 // Unbound positive energy
        };

        var result = _engine.AnalyzeInsertion(telemetry);

        Assert.False(result.IsBallisticallyCaptured);
        Assert.False(result.IsLunarInsertionGoForBurn);
        Assert.Contains("LUNAR_FLYBY_ESCAPE_TRAJECTORY", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeInsertion_LowPerilune_TriggersImpactHazard()
    {
        var telemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "MISSION-IMPACT-RISK",
            PeriluneAltitudeKm = 42.0 // Dangerous < 75 km
        };

        var result = _engine.AnalyzeInsertion(telemetry);

        Assert.False(result.IsPeriluneAltitudeSafe);
        Assert.False(result.IsLunarInsertionGoForBurn);
        Assert.Contains("PERILUNE_TERRAIN_IMPACT_HAZARD", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeInsertion_ExcessiveBurnExecutionError_TriggersOverrunAlert()
    {
        var telemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "MISSION-BURN-ERROR",
            BurnExecutionErrorMetersPerSecond = 0.45 // Exceeds 0.20 m/s
        };

        var result = _engine.AnalyzeInsertion(telemetry);

        Assert.False(result.IsDeltaVExecutionAccurate);
        Assert.Contains("DELTA_V_EXECUTION_OVERRUN_UNDERBURN", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeInsertion_DeltaVSavings_ReflectsBallisticAdvantage()
    {
        var bltTelemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "BLT-PROFILE",
            PlannedInsertionDeltaVMetersPerSecond = 610.0
        };

        var directTelemetry = new LunarOrbitInsertionTelemetry
        {
            MissionTrajectoryId = "DIRECT-PROFILE",
            PlannedInsertionDeltaVMetersPerSecond = 960.0
        };

        var bltResult = _engine.AnalyzeInsertion(bltTelemetry);
        var directResult = _engine.AnalyzeInsertion(directTelemetry);

        Assert.True(bltResult.DeltaVSavingsVsDirectMetersPerSecond > directResult.DeltaVSavingsVsDirectMetersPerSecond);
    }
}
