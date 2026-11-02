namespace SGrowing.Api.Modules.Marine.MhdMarineSilentPropulsion;

public sealed record MhdMarineTelemetry(
    string VesselDuctId,
    double SeawaterFlowVelocityMetersPerSecond,
    double SeawaterConductivitySiemensPerMeter,
    double MagneticFluxDensityTesla,
    double TransverseCurrentDensityAmpsPerMeter2,
    double ThrusterDuctCrossSectionAreaM2,
    double ThrusterDuctLengthMeters,
    double HydroacousticCavitationNoiseDecibels,
    bool SuperconductingCryostatQuenchProtectionArmed
);

public sealed record MhdMarineAnalysisResult(
    string VesselDuctId,
    double ElectromagneticLorentzThrustKilonewtons,
    double DuctPropulsiveEfficiency,
    double AcousticNoiseSuppressionDecibels,
    string ThrusterOperationalRegime,
    string ElectrohydrodynamicCommand,
    bool IsPropulsionStealthMaintained
);

public interface IMhdMarineSilentPropulsionEngine
{
    MhdMarineAnalysisResult AnalyzeMhdThruster(MhdMarineTelemetry telemetry);
}
