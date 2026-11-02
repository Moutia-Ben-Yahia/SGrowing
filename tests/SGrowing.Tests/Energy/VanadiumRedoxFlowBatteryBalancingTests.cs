using System;
using SGrowing.Api.Modules.Energy.VanadiumRedoxFlowBatteryBalancing;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class VanadiumRedoxFlowBatteryBalancingTests
{
    private readonly VanadiumRedoxFlowBatteryBalancingEngine _engine = new();

    [Fact]
    public void AnalyzeElectrolyteBalance_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeElectrolyteBalance(null!));
    }

    [Fact]
    public void AnalyzeElectrolyteBalance_NegativeConcentration_ThrowsArgumentException()
    {
        var telemetry = new VanadiumFlowBatteryTelemetry(
            FlowBatteryStackId: "VRFB-STACK-01",
            NegalyteV2ConcentrationMolar: -0.5, // Negative!
            NegalyteV3ConcentrationMolar: 1.0,
            PosalyteV4ConcentrationMolar: 1.0,
            PosalyteV5ConcentrationMolar: 0.5,
            ElectrolyteTemperatureCelsius: 25.0,
            PumpElectrolyteFlowRateLitersPerMinute: 30.0,
            StackOpenCircuitVoltageVolts: 1.40
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeElectrolyteBalance(telemetry));
    }

    [Fact]
    public void AnalyzeElectrolyteBalance_OptimalSymmetricBalance_ReturnsOptimalRegime()
    {
        var telemetry = new VanadiumFlowBatteryTelemetry(
            FlowBatteryStackId: "VRFB-MWH-GRID-01",
            NegalyteV2ConcentrationMolar: 1.25,
            NegalyteV3ConcentrationMolar: 0.35, // Neg SOC = 1.25 / 1.60 = 78.1%
            PosalyteV4ConcentrationMolar: 0.36,
            PosalyteV5ConcentrationMolar: 1.24, // Pos SOC = 1.24 / 1.60 = 77.5% -> Delta = 0.6%
            ElectrolyteTemperatureCelsius: 28.0,
            PumpElectrolyteFlowRateLitersPerMinute: 50.0,
            StackOpenCircuitVoltageVolts: 1.48
        );

        var result = _engine.AnalyzeElectrolyteBalance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("VRFB-MWH-GRID-01", result.FlowBatteryStackId);
        Assert.True(result.SocImbalanceDeltaPercent <= 3.0);
        Assert.True(result.IsElectrolyteBalanced);
        Assert.Equal("OPTIMAL_SYMMETRIC_REVERSIBLE_VRFB_REGIME", result.VrfbOperatingRegime);
        Assert.Contains("Maximum Coulombic and round-trip energy efficiency", result.RebalancingControlGuidance);
    }

    [Fact]
    public void AnalyzeElectrolyteBalance_ThermalPrecipitationRisk_ReturnsV2O5PrecipitationRisk()
    {
        var telemetry = new VanadiumFlowBatteryTelemetry(
            FlowBatteryStackId: "VRFB-HOT-TANK-02",
            NegalyteV2ConcentrationMolar: 1.5,
            NegalyteV3ConcentrationMolar: 0.1,
            PosalyteV4ConcentrationMolar: 0.05,
            PosalyteV5ConcentrationMolar: 1.70, // >= 1.65 M
            ElectrolyteTemperatureCelsius: 43.0, // >= 40 C!
            PumpElectrolyteFlowRateLitersPerMinute: 40.0,
            StackOpenCircuitVoltageVolts: 1.55
        );

        var result = _engine.AnalyzeElectrolyteBalance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_V2O5_THERMAL_PRECIPITATION_RISK", result.VrfbOperatingRegime);
        Assert.False(result.IsElectrolyteBalanced);
        Assert.Contains("irreversible V2O5 precipitation", result.RebalancingControlGuidance);
    }

    [Fact]
    public void AnalyzeElectrolyteBalance_SevereCrossoverImbalance_ReturnsRebalancingTriggered()
    {
        var telemetry = new VanadiumFlowBatteryTelemetry(
            FlowBatteryStackId: "VRFB-UNBALANCED-03",
            NegalyteV2ConcentrationMolar: 1.40,
            NegalyteV3ConcentrationMolar: 0.20, // Neg SOC = 87.5%
            PosalyteV4ConcentrationMolar: 0.90,
            PosalyteV5ConcentrationMolar: 0.70, // Pos SOC = 43.8% -> Delta = 43.7% >= 15%
            ElectrolyteTemperatureCelsius: 25.0,
            PumpElectrolyteFlowRateLitersPerMinute: 45.0,
            StackOpenCircuitVoltageVolts: 1.35
        );

        var result = _engine.AnalyzeElectrolyteBalance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_CROSSOVER_CAPACITY_IMBALANCE_REBALANCING_TRIGGERED", result.VrfbOperatingRegime);
        Assert.False(result.IsElectrolyteBalanced);
        Assert.Contains("auxiliary electrochemical rebalancing cell", result.RebalancingControlGuidance);
    }
}
