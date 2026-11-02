using System;
using SGrowing.Api.Modules.Aerospace.HypersonicScramjetIsolatorShockTrain;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class HypersonicScramjetIsolatorShockTrainTests
{
    private readonly HypersonicScramjetIsolatorShockTrainEngine _engine = new();

    [Fact]
    public void AnalyzeIsolatorShockTrain_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeIsolatorShockTrain(null!));
    }

    [Fact]
    public void AnalyzeIsolatorShockTrain_NegativeLength_ThrowsArgumentException()
    {
        var telemetry = new HypersonicScramjetTelemetry(
            ScramjetEngineId: "SCRAM-MACH7-01",
            FlightMachNumber: 7.0,
            IsolatorInletMachNumber: 2.2,
            IsolatorHydraulicDiameterMeters: 0.08,
            IsolatorLengthMeters: -0.5, // Negative!
            IsolatorInletStaticPressureKilopascals: 40.0,
            CombustorBackpressureKilopascals: 100.0,
            FanningFrictionFactor: 0.004
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeIsolatorShockTrain(telemetry));
    }

    [Fact]
    public void AnalyzeIsolatorShockTrain_StableContained_ReturnsStableSupersonicRegime()
    {
        // D = 0.08, f = 0.004 -> 1/sqrt(f) = 15.81, D * 15.81 = 1.265
        // P_in = 50 kPa, P_out = 90 kPa -> P_ratio = 1.8, p_diff = 0.8
        // term = 1.05 * 0.8 + 0.075 * 0.64 = 0.84 + 0.048 = 0.888
        // L_st = 1.265 * 0.888 = 1.123 m... wait, if L = 2.0 m:
        // margin = (2.0 - 1.123)/2.0 = 43.8% >= 35.0%
        var telemetry = new HypersonicScramjetTelemetry(
            ScramjetEngineId: "X51-WAVERIDER-DUAL",
            FlightMachNumber: 6.5,
            IsolatorInletMachNumber: 2.5,
            IsolatorHydraulicDiameterMeters: 0.08,
            IsolatorLengthMeters: 2.0,
            IsolatorInletStaticPressureKilopascals: 50.0,
            CombustorBackpressureKilopascals: 90.0,
            FanningFrictionFactor: 0.004
        );

        var result = _engine.AnalyzeIsolatorShockTrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("X51-WAVERIDER-DUAL", result.ScramjetEngineId);
        Assert.True(result.UnstartSafetyMarginPercent >= 35.0);
        Assert.True(result.IsIsolatorFlowStable);
        Assert.Equal("STABLE_SUPERSONIC_ISOLATOR_SHOCK_CONTAINED_REGIME", result.IsolatorFlowRegime);
        Assert.Contains("stably contained within isolator", result.CombustorControlGuidance);
    }

    [Fact]
    public void AnalyzeIsolatorShockTrain_SevereBackpressure_ReturnsCatastrophicUnstartHazard()
    {
        // Massive backpressure: P_out = 350 kPa vs P_in = 40 kPa -> P_ratio = 8.75
        // Shock train length >> isolator length
        var telemetry = new HypersonicScramjetTelemetry(
            ScramjetEngineId: "SCRAM-UNSTART-02",
            FlightMachNumber: 6.0,
            IsolatorInletMachNumber: 2.0,
            IsolatorHydraulicDiameterMeters: 0.10,
            IsolatorLengthMeters: 0.60, // Short duct
            IsolatorInletStaticPressureKilopascals: 40.0,
            CombustorBackpressureKilopascals: 300.0, // High backpressure
            FanningFrictionFactor: 0.004
        );

        var result = _engine.AnalyzeIsolatorShockTrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CATASTROPHIC_INLET_UNSTART_SHOCK_DISGORGEMENT_HAZARD", result.IsolatorFlowRegime);
        Assert.False(result.IsIsolatorFlowStable);
        Assert.Contains("immediately throttle back combustor fuel flow", result.CombustorControlGuidance);
    }

    [Fact]
    public void AnalyzeIsolatorShockTrain_ZeroBackpressure_ReturnsDeflagrationBlowout()
    {
        var telemetry = new HypersonicScramjetTelemetry(
            ScramjetEngineId: "SCRAM-BLOWOUT-03",
            FlightMachNumber: 5.5,
            IsolatorInletMachNumber: 2.0,
            IsolatorHydraulicDiameterMeters: 0.08,
            IsolatorLengthMeters: 1.0,
            IsolatorInletStaticPressureKilopascals: 50.0,
            CombustorBackpressureKilopascals: 45.0, // P_out < P_in!
            FanningFrictionFactor: 0.004
        );

        var result = _engine.AnalyzeIsolatorShockTrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ZERO_BACKPRESSURE_DEFLAGRATION_BLOWOUT", result.IsolatorFlowRegime);
        Assert.False(result.IsIsolatorFlowStable);
        Assert.Contains("Combustion flameout occurred", result.CombustorControlGuidance);
    }
}
