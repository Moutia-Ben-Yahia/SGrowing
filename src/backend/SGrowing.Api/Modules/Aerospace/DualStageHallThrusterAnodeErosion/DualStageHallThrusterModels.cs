namespace SGrowing.Api.Modules.Aerospace.DualStageHallThrusterAnodeErosion;

public sealed record DualStageHallTelemetry(
    string ThrusterId,
    string PropellantType,
    double IonizationStagePowerWatts,
    double AccelerationStageVoltageVolts,
    double AnodePropellantMassFlowMgPerSec,
    double ChannelWallErosionRateMicronsPerHour,
    double MagneticShieldingCuspDisplacementMillimeters,
    double GimbalVectorMisalignmentDegrees,
    bool ActiveCathodeNeutralizerRedundantReady
);

public sealed record DualStageHallAnalysisResult(
    string ThrusterId,
    double TotalThrustMillinewtons,
    double SpecificImpulseSeconds,
    double ProjectedThrusterRemainingLifetimeHours,
    string ThrusterHealthRegime,
    string MagneticTrimCommand,
    bool IsThrusterOperational
);

public interface IDualStageHallThrusterAnodeErosionEngine
{
    DualStageHallAnalysisResult AnalyzeThrusterErosionAndThrust(DualStageHallTelemetry telemetry);
}
