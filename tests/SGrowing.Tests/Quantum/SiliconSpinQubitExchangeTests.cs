using SGrowing.Api.Modules.Quantum.SiliconSpinQubitExchange;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class SiliconSpinQubitExchangeTests
{
    private readonly SiliconSpinQubitEngine _engine = new();

    [Fact]
    public void AnalyzeSpinQubit_NominalSweetSpot_ReturnsLogicReady()
    {
        var telemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-PAIR-28SI-001",
            SubstrateType = "Silicon28_MOS",
            DetuningBiasMillivolts = 0.0, // At exact sweet spot
            InterDotTunnelCouplingMicroElectronVolts = 8.0,
            OnSiteChargingEnergyMilliElectronVolts = 3.5,
            ValleySplittingEnergyMicroElectronVolts = 150.0,
            RmsDetuningChargeNoiseMicrovolts = 1.2,
            ExchangeGateDurationNanoseconds = 15.0,
            PauliSpinBlockadeReadoutFidelityPercent = 99.4,
            SpinDephasingTimeT2StarMicroseconds = 25.0
        };

        var result = _engine.AnalyzeSpinQubit(telemetry);

        Assert.Equal("DOT-PAIR-28SI-001", result.DotPairId);
        Assert.True(result.IsTwoQubitLogicReady);
        Assert.True(result.IsAtExchangeSweetSpot);
        Assert.True(result.IsValleySplittingAdequate);
        Assert.True(result.IsPauliBlockadeReadoutOptimal);
        Assert.True(result.TwoQubitSwapGateFidelityPercent >= 99.0);
        Assert.True(result.CalculatedExchangeCouplingMhz > 0.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSpinQubit_LowValleySplitting_TriggersCollapseDeficit()
    {
        var telemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-PAIR-VALLEY-DEFICIT",
            ValleySplittingEnergyMicroElectronVolts = 55.0 // Below 90 ueV
        };

        var result = _engine.AnalyzeSpinQubit(telemetry);

        Assert.False(result.IsValleySplittingAdequate);
        Assert.False(result.IsTwoQubitLogicReady);
        Assert.Contains("VALLEY_SPLITTING_COLLAPSE_DEFICIT", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeSpinQubit_OffSweetSpot_TriggersChargeNoiseDephasing()
    {
        var telemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-PAIR-OFF-SWEETSPOT",
            DetuningBiasMillivolts = 1.2, // Far from sweet spot
            RmsDetuningChargeNoiseMicrovolts = 4.5
        };

        var result = _engine.AnalyzeSpinQubit(telemetry);

        Assert.False(result.IsAtExchangeSweetSpot);
        Assert.Contains("CHARGE_NOISE_EXCHANGE_DEPHASING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSpinQubit_DegradedReadout_TriggersPauliBlockadeAlert()
    {
        var telemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-PAIR-READOUT-FAIL",
            PauliSpinBlockadeReadoutFidelityPercent = 94.5 // Below 98.0%
        };

        var result = _engine.AnalyzeSpinQubit(telemetry);

        Assert.False(result.IsPauliBlockadeReadoutOptimal);
        Assert.Contains("PAULI_SPIN_BLOCKADE_LATCHING_FAILURE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSpinQubit_SwapFidelity_DegradesWithChargeNoise()
    {
        var quietTelemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-QUIET",
            DetuningBiasMillivolts = 0.05,
            RmsDetuningChargeNoiseMicrovolts = 1.0
        };

        var noisyTelemetry = new SiliconSpinQubitTelemetry
        {
            DotPairId = "DOT-NOISY",
            DetuningBiasMillivolts = 1.5,
            RmsDetuningChargeNoiseMicrovolts = 12.0
        };

        var quietRes = _engine.AnalyzeSpinQubit(quietTelemetry);
        var noisyRes = _engine.AnalyzeSpinQubit(noisyTelemetry);

        Assert.True(quietRes.TwoQubitSwapGateFidelityPercent > noisyRes.TwoQubitSwapGateFidelityPercent);
    }
}
