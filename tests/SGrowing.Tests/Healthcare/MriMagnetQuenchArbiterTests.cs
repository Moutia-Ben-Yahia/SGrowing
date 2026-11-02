using FluentAssertions;
using SGrowing.Api.Modules.Healthcare.MriMagnetQuenchArbiter;
using Xunit;

namespace SGrowing.Tests.Healthcare;

public class MriMagnetQuenchArbiterTests
{
    private readonly MriMagnetQuenchArbiterEngine _engine = new();

    [Fact]
    public void EvaluateQuenchRisk_NominalSuperconducting_ReturnsSteadyState()
    {
        var state = new MriMagnetState(
            ScannerId: "MRI-3T-01",
            MagneticFieldTesla: 3.0,
            OperatingCurrentAmperes: 800.0,
            CoilResistiveVoltageMilliVolts: 0.2,
            LiquidHeliumBoilOffRateLitersPerMin: 1.1,
            CryostatVacuumPressureMbar: 1e-6,
            ColdHeadTemperatureKelvin: 4.2
        );

        var limits = new QuenchProtectionLimits();
        var request = new MriMagnetQuenchRequest(state, limits);
        var decision = _engine.EvaluateQuenchRisk(request);

        decision.ScannerId.Should().Be("MRI-3T-01");
        decision.QuenchPrecursorDetected.Should().BeFalse();
        decision.EmergencyDumpTriggered.Should().BeFalse();
        decision.SafetyInterlockStatus.Should().Be("SUPERCONDUCTING_STEADY_STATE");
        decision.EstimatedHotSpotTemperatureKelvin.Should().Be(4.2);
        decision.StoredMagneticEnergyMegaJoules.Should().Be(2.5);
    }

    [Fact]
    public void EvaluateQuenchRisk_ResistiveVoltagePrecursor_AlertsQuenchPrecursor()
    {
        // 65 mV resistive voltage exceeds 50 mV threshold
        var state = new MriMagnetState(
            ScannerId: "MRI-7T-RESEARCH",
            MagneticFieldTesla: 7.0,
            OperatingCurrentAmperes: 1200.0,
            CoilResistiveVoltageMilliVolts: 65.0,
            LiquidHeliumBoilOffRateLitersPerMin: 8.0,
            CryostatVacuumPressureMbar: 2e-5,
            ColdHeadTemperatureKelvin: 4.8
        );

        var limits = new QuenchProtectionLimits(MaxAllowableResistiveVoltageMilliVolts: 50.0);
        var request = new MriMagnetQuenchRequest(state, limits);
        var decision = _engine.EvaluateQuenchRisk(request);

        decision.QuenchPrecursorDetected.Should().BeTrue();
        decision.EmergencyDumpTriggered.Should().BeFalse();
        decision.SafetyInterlockStatus.Should().Be("QUENCH_PRECURSOR_DETECTED");
        decision.RecommendedAction.Should().Contain("RAMP_DOWN_STANDBY");
        decision.StoredMagneticEnergyMegaJoules.Should().BeGreaterThan(10.0);
    }

    [Fact]
    public void EvaluateQuenchRisk_CatastrophicQuench_FiresQuenchHeatersAndDumpResistor()
    {
        // Severe normal zone: 180 mV resistive voltage and 32 L/min boil off
        var state = new MriMagnetState(
            ScannerId: "MRI-7T-EMERGENCY",
            MagneticFieldTesla: 7.0,
            OperatingCurrentAmperes: 1500.0,
            CoilResistiveVoltageMilliVolts: 180.0,
            LiquidHeliumBoilOffRateLitersPerMin: 32.0,
            CryostatVacuumPressureMbar: 5e-3,
            ColdHeadTemperatureKelvin: 9.5
        );

        var limits = new QuenchProtectionLimits(DumpResistorOhms: 0.8);
        var request = new MriMagnetQuenchRequest(state, limits);
        var decision = _engine.EvaluateQuenchRisk(request);

        decision.EmergencyDumpTriggered.Should().BeTrue();
        decision.SafetyInterlockStatus.Should().Be("EMERGENCY_QUENCH_HEATER_ACTIVE");
        decision.RecommendedAction.Should().Contain("TRIGGER_DUMP_AND_VENT");
        decision.EnergyExtractionRateMegaWatts.Should().BeGreaterThan(1.0);
        decision.EstimatedHotSpotTemperatureKelvin.Should().BeGreaterThan(50.0);
    }

    [Fact]
    public void EvaluateQuenchRisk_VacuumLoss_TriggersQuenchPrecursorWarning()
    {
        // Vacuum degrades past 1e-4 mbar
        var state = new MriMagnetState(
            ScannerId: "MRI-VACUUM-DEGRADATION",
            MagneticFieldTesla: 3.0,
            OperatingCurrentAmperes: 600.0,
            CoilResistiveVoltageMilliVolts: 5.0,
            LiquidHeliumBoilOffRateLitersPerMin: 4.0,
            CryostatVacuumPressureMbar: 3e-4,
            ColdHeadTemperatureKelvin: 4.4
        );

        var limits = new QuenchProtectionLimits(MaxAllowableVacuumPressureMbar: 1e-4);
        var request = new MriMagnetQuenchRequest(state, limits);
        var decision = _engine.EvaluateQuenchRisk(request);

        decision.QuenchPrecursorDetected.Should().BeTrue();
        decision.EmergencyDumpTriggered.Should().BeFalse();
        decision.SafetyInterlockStatus.Should().Be("QUENCH_PRECURSOR_DETECTED");
    }
}
