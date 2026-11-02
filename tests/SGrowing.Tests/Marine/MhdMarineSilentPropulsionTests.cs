using FluentAssertions;
using SGrowing.Api.Modules.Marine.MhdMarineSilentPropulsion;
using Xunit;

namespace SGrowing.Tests.Marine;

public sealed class MhdMarineSilentPropulsionTests
{
    private readonly MhdMarineSilentPropulsionEngine _engine = new();

    [Fact]
    public void AnalyzeMhdThruster_NominalCruise_ReturnsSilentLorentzCruise()
    {
        var telemetry = new MhdMarineTelemetry(
            VesselDuctId: "SSN-MHD-YAMATO-01",
            SeawaterFlowVelocityMetersPerSecond: 12.0,
            SeawaterConductivitySiemensPerMeter: 4.5,
            MagneticFluxDensityTesla: 10.5,
            TransverseCurrentDensityAmpsPerMeter2: 1800.0,
            ThrusterDuctCrossSectionAreaM2: 1.8,
            ThrusterDuctLengthMeters: 6.0,
            HydroacousticCavitationNoiseDecibels: 42.0,
            SuperconductingCryostatQuenchProtectionArmed: true
        );

        var result = _engine.AnalyzeMhdThruster(telemetry);

        result.ThrusterOperationalRegime.Should().Be("SILENT_SUPERCONDUCTING_LORENTZ_CRUISE");
        result.IsPropulsionStealthMaintained.Should().BeTrue();
        result.ElectromagneticLorentzThrustKilonewtons.Should().BeGreaterThan(150.0);
        result.AcousticNoiseSuppressionDecibels.Should().BeGreaterThan(50.0);
        result.DuctPropulsiveEfficiency.Should().BeGreaterThan(0.20);
    }

    [Fact]
    public void AnalyzeMhdThruster_ExcessiveMagneticField_ReturnsQuenchHazard()
    {
        var telemetry = new MhdMarineTelemetry(
            VesselDuctId: "SSN-QUENCH-02",
            SeawaterFlowVelocityMetersPerSecond: 10.0,
            SeawaterConductivitySiemensPerMeter: 4.5,
            MagneticFluxDensityTesla: 15.5, // Exceeds upper critical field limit
            TransverseCurrentDensityAmpsPerMeter2: 1500.0,
            ThrusterDuctCrossSectionAreaM2: 1.5,
            ThrusterDuctLengthMeters: 5.0,
            HydroacousticCavitationNoiseDecibels: 45.0,
            SuperconductingCryostatQuenchProtectionArmed: true
        );

        var result = _engine.AnalyzeMhdThruster(telemetry);

        result.ThrusterOperationalRegime.Should().Be("MAGNETIC_CRITICAL_CURRENT_QUENCH_HAZARD");
        result.IsPropulsionStealthMaintained.Should().BeFalse();
        result.ElectrohydrodynamicCommand.Should().Contain("dump resistor");
    }

    [Fact]
    public void AnalyzeMhdThruster_HighElectrodeCurrentDensity_ReturnsThermalSaturation()
    {
        var telemetry = new MhdMarineTelemetry(
            VesselDuctId: "SSN-HOT-ELECTRODE-03",
            SeawaterFlowVelocityMetersPerSecond: 14.0,
            SeawaterConductivitySiemensPerMeter: 4.5,
            MagneticFluxDensityTesla: 10.0,
            TransverseCurrentDensityAmpsPerMeter2: 3600.0, // High current density > 3000
            ThrusterDuctCrossSectionAreaM2: 1.8,
            ThrusterDuctLengthMeters: 6.0,
            HydroacousticCavitationNoiseDecibels: 48.0,
            SuperconductingCryostatQuenchProtectionArmed: true
        );

        var result = _engine.AnalyzeMhdThruster(telemetry);

        result.ThrusterOperationalRegime.Should().Be("ELECTRODE_THERMAL_SATURATION_WARNING");
        result.IsPropulsionStealthMaintained.Should().BeFalse();
        result.ElectrohydrodynamicCommand.Should().Contain("thermal boundary boiling");
    }

    [Fact]
    public void AnalyzeMhdThruster_CavitationBubbles_ReturnsChlorineBubbleCavitation()
    {
        var telemetry = new MhdMarineTelemetry(
            VesselDuctId: "SSN-NOISY-BUBBLE-04",
            SeawaterFlowVelocityMetersPerSecond: 8.0,
            SeawaterConductivitySiemensPerMeter: 4.5,
            MagneticFluxDensityTesla: 9.0,
            TransverseCurrentDensityAmpsPerMeter2: 2200.0,
            ThrusterDuctCrossSectionAreaM2: 1.6,
            ThrusterDuctLengthMeters: 5.5,
            HydroacousticCavitationNoiseDecibels: 72.0, // High acoustic signature > 65 dB
            SuperconductingCryostatQuenchProtectionArmed: true
        );

        var result = _engine.AnalyzeMhdThruster(telemetry);

        result.ThrusterOperationalRegime.Should().Be("CHLORINE_OVERPOTENTIAL_BUBBLE_CAVITATION");
        result.IsPropulsionStealthMaintained.Should().BeFalse();
        result.ElectrohydrodynamicCommand.Should().Contain("chlorine evolution");
    }
}
