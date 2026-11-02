namespace SGrowing.Tests.Aerospace;

using SGrowing.Api.Modules.Aerospace.ScramjetIsolatorShockTrain;
using Xunit;

public class ScramjetIsolatorShockTrainTests
{
    private readonly ScramjetIsolatorShockTrainEngine _engine = new();

    [Fact]
    public void AnalyzeShockTrain_NominalCruise_MaintainsSafeMarginAndCruiseFuel()
    {
        // Low combustor backpressure (75 kPa vs 50 kPa inlet) -> PR = 0.5
        // S is small, margin is high (> 60%)
        var telemetry = new ScramjetIsolatorTelemetry(
            EngineId: "X-51A-SCRAMJET-01",
            FlightMachNumber: 6.0,
            IsolatorInletMachNumber: 2.5,
            IsolatorDuctHeightMm: 120.0,
            IsolatorDuctLengthMm: 600.0,
            InletStaticPressureKpa: 50.0,
            CombustorBackpressureKpa: 75.0,
            BoundaryLayerMomentumThicknessMm: 2.0,
            FuelFlowRateKgPerSec: 1.2);

        var result = _engine.AnalyzeShockTrain(telemetry);

        Assert.Equal("X-51A-SCRAMJET-01", result.EngineId);
        Assert.Equal("SUPERCONDUCTING_RAMJET_NOMINAL", result.EngineOperatingRegime);
        Assert.Equal("MAINTAIN_CRUISE_FUEL_FLOW", result.RecommendedControlActuation);
        Assert.False(result.IsUnstartImminent);
        Assert.True(result.UnstartSafetyMarginPercent > 50.0);
    }

    [Fact]
    public void AnalyzeShockTrain_ModerateCombustorPressure_ActivatesBleed()
    {
        // Backpressure = 200 kPa vs 50 kPa -> PR = 3.0
        // S grows to ~300 mm, leaving margin ~25-30% (< 35%)
        var telemetry = new ScramjetIsolatorTelemetry(
            EngineId: "HYPERSONIC-CRUISE-02",
            FlightMachNumber: 6.5,
            IsolatorInletMachNumber: 2.5,
            IsolatorDuctHeightMm: 120.0,
            IsolatorDuctLengthMm: 600.0,
            InletStaticPressureKpa: 50.0,
            CombustorBackpressureKpa: 160.0,
            BoundaryLayerMomentumThicknessMm: 2.5,
            FuelFlowRateKgPerSec: 1.6);

        var result = _engine.AnalyzeShockTrain(telemetry);

        Assert.Equal("ISOLATOR_SHOCK_CONFINEMENT", result.EngineOperatingRegime);
        Assert.Equal("ACTIVATE_BOUNDARY_LAYER_BLEED", result.RecommendedControlActuation);
        Assert.False(result.IsUnstartImminent);
        Assert.True(result.UnstartSafetyMarginPercent < 35.0);
    }

    [Fact]
    public void AnalyzeShockTrain_SevereBackpressure_TriggersRapidFuelCutUnstartPrevention()
    {
        // High backpressure 350 kPa vs 50 kPa -> PR = 6.0
        // S extends beyond 550 mm, leaving margin < 15%
        var telemetry = new ScramjetIsolatorTelemetry(
            EngineId: "SCRAMJET-UNSTART-RISK",
            FlightMachNumber: 7.0,
            IsolatorInletMachNumber: 2.8,
            IsolatorDuctHeightMm: 140.0,
            IsolatorDuctLengthMm: 650.0,
            InletStaticPressureKpa: 50.0,
            CombustorBackpressureKpa: 350.0,
            BoundaryLayerMomentumThicknessMm: 3.0,
            FuelFlowRateKgPerSec: 2.5);

        var result = _engine.AnalyzeShockTrain(telemetry);

        Assert.True(result.IsUnstartImminent);
        Assert.Equal("CRITICAL_INLET_UNSTART_SURGE", result.EngineOperatingRegime);
        Assert.Equal("RAPID_FUEL_CUT_AND_COWL_FLAP_DUMP", result.RecommendedControlActuation);
        Assert.Contains("CRITICAL UNSTART HAZARD", result.FlightSafetyAdvisory);
    }

    [Fact]
    public void AnalyzeShockTrain_SubsonicIsolatorMach_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new ScramjetIsolatorTelemetry(
            EngineId: "ERR-SUBSONIC",
            FlightMachNumber: 5.0,
            IsolatorInletMachNumber: 0.9, // Subsonic < 1.0
            IsolatorDuctHeightMm: 100.0,
            IsolatorDuctLengthMm: 500.0,
            InletStaticPressureKpa: 50.0,
            CombustorBackpressureKpa: 80.0,
            BoundaryLayerMomentumThicknessMm: 2.0,
            FuelFlowRateKgPerSec: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeShockTrain(invalid));
    }
}
