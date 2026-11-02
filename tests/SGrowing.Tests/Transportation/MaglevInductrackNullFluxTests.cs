using FluentAssertions;
using SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;
using Xunit;

namespace SGrowing.Tests.Transportation;

public sealed class MaglevInductrackNullFluxTests
{
    private readonly MaglevInductrackNullFluxEngine _engine = new();

    [Fact]
    public void AnalyzeLevitationState_NominalHighSpeed_ReturnsOptimalLevitation()
    {
        var telemetry = new MaglevInductrackTelemetry(
            BogieId: "MAGLEV-CHUO-L0-01",
            VehicleSpeedKph: 500.0,
            HalbachPeakMagneticFieldTesla: 1.45,
            AirgapMillimeters: 18.0,
            TrackCoilResistanceOhms: 0.0025,
            TrackCoilInductanceMicroHenries: 8.5,
            VehicleWeightKilonewtons: 350.0,
            TrackTemperatureCelsius: 30.0,
            ActiveNullFluxGuidanceDampingEnabled: true
        );

        var result = _engine.AnalyzeLevitationState(telemetry);

        result.SuspensionRegime.Should().Be("HIGH_SPEED_SUPERCONDUCTING_LEVITATION_OPTIMAL");
        result.IsLevitationStable.Should().BeTrue();
        result.ElectrodynamicLiftForceKilonewtons.Should().BeGreaterThan(300.0);
        result.LiftToDragRatio.Should().BeGreaterThan(5.0);
        result.TransitionSpeedKph.Should().BeInRange(70.0, 100.0);
    }

    [Fact]
    public void AnalyzeLevitationState_LowAirgap_ReturnsTouchdownHazard()
    {
        var telemetry = new MaglevInductrackTelemetry(
            BogieId: "MAGLEV-COLLAPSE-02",
            VehicleSpeedKph: 300.0,
            HalbachPeakMagneticFieldTesla: 1.20,
            AirgapMillimeters: 6.5, // Dangerous airgap < 8 mm
            TrackCoilResistanceOhms: 0.0025,
            TrackCoilInductanceMicroHenries: 8.5,
            VehicleWeightKilonewtons: 380.0,
            TrackTemperatureCelsius: 35.0,
            ActiveNullFluxGuidanceDampingEnabled: true
        );

        var result = _engine.AnalyzeLevitationState(telemetry);

        result.SuspensionRegime.Should().Be("AIRGAP_COLLAPSE_TOUCHDOWN_HAZARD");
        result.IsLevitationStable.Should().BeFalse();
        result.LinearMotorPropulsionCommand.Should().Contain("secondary mechanical landing wheels");
    }

    [Fact]
    public void AnalyzeLevitationState_OverheatedTrack_ReturnsEddyOverheatingWarning()
    {
        var telemetry = new MaglevInductrackTelemetry(
            BogieId: "MAGLEV-HOTTRACK-03",
            VehicleSpeedKph: 450.0,
            HalbachPeakMagneticFieldTesla: 1.40,
            AirgapMillimeters: 16.0,
            TrackCoilResistanceOhms: 0.0025,
            TrackCoilInductanceMicroHenries: 8.5,
            VehicleWeightKilonewtons: 350.0,
            TrackTemperatureCelsius: 82.0, // High temperature > 75C
            ActiveNullFluxGuidanceDampingEnabled: true
        );

        var result = _engine.AnalyzeLevitationState(telemetry);

        result.SuspensionRegime.Should().Be("TRACK_EDDY_OVERHEATING_WARNING");
        result.IsLevitationStable.Should().BeFalse();
        result.LinearMotorPropulsionCommand.Should().Contain("forced-air cooling");
    }

    [Fact]
    public void AnalyzeLevitationState_NearTransitionSpeed_ReturnsMagneticDragPeak()
    {
        var telemetry = new MaglevInductrackTelemetry(
            BogieId: "MAGLEV-TAKEOFF-04",
            VehicleSpeedKph: 90.0, // Near transition speed ~84 km/h
            HalbachPeakMagneticFieldTesla: 1.45,
            AirgapMillimeters: 14.0,
            TrackCoilResistanceOhms: 0.0025,
            TrackCoilInductanceMicroHenries: 8.5,
            VehicleWeightKilonewtons: 320.0,
            TrackTemperatureCelsius: 28.0,
            ActiveNullFluxGuidanceDampingEnabled: false
        );

        var result = _engine.AnalyzeLevitationState(telemetry);

        result.SuspensionRegime.Should().Be("TRANSITION_SPEED_MAGNETIC_DRAG_PEAK");
        result.IsLevitationStable.Should().BeTrue();
        result.LinearMotorPropulsionCommand.Should().Contain("magnetic drag peak");
    }
}
