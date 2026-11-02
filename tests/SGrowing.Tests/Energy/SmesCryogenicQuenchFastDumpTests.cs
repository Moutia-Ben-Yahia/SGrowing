using System;
using SGrowing.Api.Modules.Energy.SmesCryogenicQuenchFastDump;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class SmesCryogenicQuenchFastDumpTests
{
    private readonly SmesCryogenicQuenchFastDumpEngine _engine = new();

    [Fact]
    public void AnalyzeQuenchState_NominalSuperconductingState_MaintainsPersistentSwitch()
    {
        var telemetry = new SmesQuenchTelemetry(
            CoilId: "REBCO-SMES-COIL-01",
            StoredEnergyMegajoules: 10.0,
            OperatingCurrentAmperes: 1200.0,
            CoilInductanceHenries: 13.88,
            MeasuredTotalVoltageMillivolts: 12.00,
            CoWoundInductiveVoltageMillivolts: 12.00,
            ColdHeadTemperatureKelvin: 20.5,
            HotSpotHeatingRateKelvinPerSec: 0.1,
            DumpResistorOhms: 2.5,
            CurrentRampRateAmpsPerSec: 50.0);

        var result = _engine.AnalyzeQuenchState(telemetry);

        Assert.Equal("SUPERCONDUCTING_STATE_NOMINAL", result.QuenchProtectionStatus);
        Assert.Equal("MAINTAIN_PERSISTENT_CURRENT_SWITCH_CONDUCTION", result.ProtectionActionRecommendation);
        Assert.False(result.IsFastDumpTriggered);
        Assert.Equal(0.0, result.ResistiveVoltageMicrovolts);
    }

    [Fact]
    public void AnalyzeQuenchState_QuenchOnsetAboveThreshold_TriggersFastDump()
    {
        // V_total = 12.25 mV vs 12.10 inductive -> V_res = 0.15 mV = 150 uV >= 100 uV
        var telemetry = new SmesQuenchTelemetry(
            CoilId: "REBCO-SMES-COIL-02",
            StoredEnergyMegajoules: 10.0,
            OperatingCurrentAmperes: 1200.0,
            CoilInductanceHenries: 13.88,
            MeasuredTotalVoltageMillivolts: 12.25,
            CoWoundInductiveVoltageMillivolts: 12.10,
            ColdHeadTemperatureKelvin: 22.0,
            HotSpotHeatingRateKelvinPerSec: 25.0, // > 15.0 K/s
            DumpResistorOhms: 2.5,
            CurrentRampRateAmpsPerSec: 50.0);

        var result = _engine.AnalyzeQuenchState(telemetry);

        Assert.Equal("QUENCH_DETECTED_CRITICAL", result.QuenchProtectionStatus);
        Assert.Equal("TRIGGER_PYROTECHNIC_FAST_DUMP_AND_ISOLATE_CONVERTER", result.ProtectionActionRecommendation);
        Assert.True(result.IsFastDumpTriggered);
        Assert.Equal(150.0, result.ResistiveVoltageMicrovolts);
    }

    [Fact]
    public void AnalyzeQuenchState_ElevatedColdHeadTemperature_IssuesCryoWarning()
    {
        var telemetry = new SmesQuenchTelemetry(
            CoilId: "REBCO-SMES-COIL-03",
            StoredEnergyMegajoules: 10.0,
            OperatingCurrentAmperes: 1200.0,
            CoilInductanceHenries: 13.88,
            MeasuredTotalVoltageMillivolts: 12.01,
            CoWoundInductiveVoltageMillivolts: 12.00,
            ColdHeadTemperatureKelvin: 31.5, // > 28.0 K
            HotSpotHeatingRateKelvinPerSec: 1.5,
            DumpResistorOhms: 2.5,
            CurrentRampRateAmpsPerSec: 50.0);

        var result = _engine.AnalyzeQuenchState(telemetry);

        Assert.Equal("CRYOGENIC_TEMPERATURE_WARNING", result.QuenchProtectionStatus);
        Assert.Equal("BOOST_HELIUM_CIRCULATOR_SPEED_AND_RAMP_DOWN_CURRENT_20_PERCENT", result.ProtectionActionRecommendation);
        Assert.False(result.IsFastDumpTriggered);
    }

    [Fact]
    public void AnalyzeQuenchState_ElevatedResistiveVoltageBelowQuench_IssuesWarning()
    {
        // V_res = 0.06 mV = 60 uV (>= 40 uV, < 100 uV)
        var telemetry = new SmesQuenchTelemetry(
            CoilId: "REBCO-SMES-COIL-04",
            StoredEnergyMegajoules: 10.0,
            OperatingCurrentAmperes: 1200.0,
            CoilInductanceHenries: 13.88,
            MeasuredTotalVoltageMillivolts: 12.06,
            CoWoundInductiveVoltageMillivolts: 12.00,
            ColdHeadTemperatureKelvin: 21.0,
            HotSpotHeatingRateKelvinPerSec: 2.0,
            DumpResistorOhms: 2.5,
            CurrentRampRateAmpsPerSec: 50.0);

        var result = _engine.AnalyzeQuenchState(telemetry);

        Assert.Equal("CRYOGENIC_TEMPERATURE_WARNING", result.QuenchProtectionStatus);
        Assert.False(result.IsFastDumpTriggered);
        Assert.Equal(60.0, result.ResistiveVoltageMicrovolts);
    }
}
