using FluentAssertions;
using SGrowing.Api.Modules.Energy.DilutionRefrigeratorDemagnetization;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class DilutionRefrigeratorDemagnetizationTests
{
    private readonly DilutionRefrigeratorDemagnetizationEngine _engine = new();

    [Fact]
    public void AnalyzeSubKelvinRefrigeration_NominalConditions_ReturnsOptimalCoherence()
    {
        var telemetry = new DilutionRefrigeratorTelemetry(
            CryostatId: "CRYO-IBM-001",
            MixingChamberTempKelvin: 0.012, // 12 mK
            StillTempKelvin: 0.80,          // 800 mK
            He3CirculationFlowRateMolPerSec: 0.00015, // 150 umol/s
            AdrMagneticFieldTesla: 0.0,
            AdrPillTemperatureKelvin: 0.012,
            SuperconductingQubitThermalLoadMicroWatts: 0.5,
            ThermalSwitchEngaged: true
        );

        var result = _engine.AnalyzeSubKelvinRefrigeration(telemetry);

        result.OperatingRegime.Should().Be("QUANTUM_COHERENCE_OPTIMAL");
        result.IsThermalStable.Should().BeTrue();
        result.DilutionCoolingPowerMicroWatts.Should().BeGreaterThan(0.0);
        result.CoolingMarginRatio.Should().BeGreaterThan(1.20);
    }

    [Fact]
    public void AnalyzeSubKelvinRefrigeration_HighMixingChamberTemp_ReturnsQuenchWarning()
    {
        var telemetry = new DilutionRefrigeratorTelemetry(
            CryostatId: "CRYO-RIGETTI-002",
            MixingChamberTempKelvin: 0.095, // 95 mK > 80 mK
            StillTempKelvin: 1.10,
            He3CirculationFlowRateMolPerSec: 0.00008,
            AdrMagneticFieldTesla: 0.0,
            AdrPillTemperatureKelvin: 0.095,
            SuperconductingQubitThermalLoadMicroWatts: 2.0,
            ThermalSwitchEngaged: true
        );

        var result = _engine.AnalyzeSubKelvinRefrigeration(telemetry);

        result.OperatingRegime.Should().Be("THERMAL_RUNAWAY_QUENCH_WARNING");
        result.IsThermalStable.Should().BeFalse();
        result.RecommendedAction.Should().Contain("1K pot thermal dump");
    }

    [Fact]
    public void AnalyzeSubKelvinRefrigeration_ThrottledCirculation_ReturnsFlowThrottled()
    {
        var telemetry = new DilutionRefrigeratorTelemetry(
            CryostatId: "CRYO-OXFORD-003",
            MixingChamberTempKelvin: 0.035, // 35 mK
            StillTempKelvin: 0.85,
            He3CirculationFlowRateMolPerSec: 0.00002, // very low flow
            AdrMagneticFieldTesla: 0.0,
            AdrPillTemperatureKelvin: 0.035,
            SuperconductingQubitThermalLoadMicroWatts: 1.5,
            ThermalSwitchEngaged: true
        );

        var result = _engine.AnalyzeSubKelvinRefrigeration(telemetry);

        result.OperatingRegime.Should().Be("CIRCULATION_FLOW_THROTTLED");
        result.IsThermalStable.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeSubKelvinRefrigeration_ActiveAdrSweep_ReturnsDemagnetizationSweep()
    {
        var telemetry = new DilutionRefrigeratorTelemetry(
            CryostatId: "CRYO-ADR-004",
            MixingChamberTempKelvin: 0.015,
            StillTempKelvin: 0.75,
            He3CirculationFlowRateMolPerSec: 0.00012,
            AdrMagneticFieldTesla: 3.5, // High field undergoing adiabatic demag
            AdrPillTemperatureKelvin: 0.018,
            SuperconductingQubitThermalLoadMicroWatts: 0.4,
            ThermalSwitchEngaged: false // thermally decoupled
        );

        var result = _engine.AnalyzeSubKelvinRefrigeration(telemetry);

        result.OperatingRegime.Should().Be("ADR_DEMAGNETIZATION_SWEEP");
        result.IsThermalStable.Should().BeTrue();
        result.AdrDemagnetizationEntropyTransferJoulesPerKelvin.Should().BeGreaterThan(0.0);
    }
}
