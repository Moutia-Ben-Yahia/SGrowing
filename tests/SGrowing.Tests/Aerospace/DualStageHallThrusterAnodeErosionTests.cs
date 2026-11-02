using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.DualStageHallThrusterAnodeErosion;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class DualStageHallThrusterAnodeErosionTests
{
    private readonly DualStageHallThrusterAnodeErosionEngine _engine = new();

    [Fact]
    public void AnalyzeThrusterErosionAndThrust_NominalShielded_ReturnsNominalPropulsion()
    {
        var telemetry = new DualStageHallTelemetry(
            ThrusterId: "HALL-GATEWAY-HERMES-01",
            PropellantType: "Xenon",
            IonizationStagePowerWatts: 850.0,
            AccelerationStageVoltageVolts: 750.0,
            AnodePropellantMassFlowMgPerSec: 18.5,
            ChannelWallErosionRateMicronsPerHour: 1.25,
            MagneticShieldingCuspDisplacementMillimeters: 0.8,
            GimbalVectorMisalignmentDegrees: 0.35,
            ActiveCathodeNeutralizerRedundantReady: true
        );

        var result = _engine.AnalyzeThrusterErosionAndThrust(telemetry);

        result.ThrusterHealthRegime.Should().Be("MAGNETIC_SHIELDING_NOMINAL_PROPULSION");
        result.IsThrusterOperational.Should().BeTrue();
        result.SpecificImpulseSeconds.Should().BeGreaterThan(3000.0);
        result.TotalThrustMillinewtons.Should().BeGreaterThan(500.0);
        result.ProjectedThrusterRemainingLifetimeHours.Should().BeGreaterThan(15000.0);
    }

    [Fact]
    public void AnalyzeThrusterErosionAndThrust_HighGimbalMisalignment_ReturnsGimbalDrift()
    {
        var telemetry = new DualStageHallTelemetry(
            ThrusterId: "HALL-DRIFT-02",
            PropellantType: "Xenon",
            IonizationStagePowerWatts: 800.0,
            AccelerationStageVoltageVolts: 650.0,
            AnodePropellantMassFlowMgPerSec: 16.0,
            ChannelWallErosionRateMicronsPerHour: 1.5,
            MagneticShieldingCuspDisplacementMillimeters: 0.9,
            GimbalVectorMisalignmentDegrees: 2.8, // Misaligned > 2.2 deg
            ActiveCathodeNeutralizerRedundantReady: true
        );

        var result = _engine.AnalyzeThrusterErosionAndThrust(telemetry);

        result.ThrusterHealthRegime.Should().Be("GIMBAL_VECTOR_DRIFT_EXCEEDED");
        result.IsThrusterOperational.Should().BeFalse();
        result.MagneticTrimCommand.Should().Contain("gimbal stepper actuator");
    }

    [Fact]
    public void AnalyzeThrusterErosionAndThrust_HighChannelWallErosion_ReturnsErosionHazard()
    {
        var telemetry = new DualStageHallTelemetry(
            ThrusterId: "HALL-EROSION-03",
            PropellantType: "Krypton",
            IonizationStagePowerWatts: 950.0,
            AccelerationStageVoltageVolts: 800.0,
            AnodePropellantMassFlowMgPerSec: 14.0,
            ChannelWallErosionRateMicronsPerHour: 4.5, // High erosion rate > 3.2
            MagneticShieldingCuspDisplacementMillimeters: 2.5,
            GimbalVectorMisalignmentDegrees: 0.4,
            ActiveCathodeNeutralizerRedundantReady: true
        );

        var result = _engine.AnalyzeThrusterErosionAndThrust(telemetry);

        result.ThrusterHealthRegime.Should().Be("ACCELERATION_CHANNEL_EROSION_HAZARD");
        result.IsThrusterOperational.Should().BeFalse();
        result.MagneticTrimCommand.Should().Contain("trim coil current");
    }

    [Fact]
    public void AnalyzeThrusterErosionAndThrust_OfflineNeutralizer_ReturnsNeutralizerAnomaly()
    {
        var telemetry = new DualStageHallTelemetry(
            ThrusterId: "HALL-CATHODE-OFFLINE-04",
            PropellantType: "Xenon",
            IonizationStagePowerWatts: 850.0,
            AccelerationStageVoltageVolts: 700.0,
            AnodePropellantMassFlowMgPerSec: 18.0,
            ChannelWallErosionRateMicronsPerHour: 1.2,
            MagneticShieldingCuspDisplacementMillimeters: 0.7,
            GimbalVectorMisalignmentDegrees: 0.3,
            ActiveCathodeNeutralizerRedundantReady: false // Disarmed/offline
        );

        var result = _engine.AnalyzeThrusterErosionAndThrust(telemetry);

        result.ThrusterHealthRegime.Should().Be("DOUBLE_CHARGED_ION_ANOMALY");
        result.IsThrusterOperational.Should().BeFalse();
        result.MagneticTrimCommand.Should().Contain("backup BaO hollow cathode");
    }
}
