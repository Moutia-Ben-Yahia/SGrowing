namespace SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;

public sealed record MaglevInductrackTelemetry(
    string BogieId,
    double VehicleSpeedKph,
    double HalbachPeakMagneticFieldTesla,
    double AirgapMillimeters,
    double TrackCoilResistanceOhms,
    double TrackCoilInductanceMicroHenries,
    double VehicleWeightKilonewtons,
    double TrackTemperatureCelsius,
    bool ActiveNullFluxGuidanceDampingEnabled
);

public sealed record MaglevInductrackAnalysisResult(
    string BogieId,
    double ElectrodynamicLiftForceKilonewtons,
    double MagneticDragForceKilonewtons,
    double LiftToDragRatio,
    double TransitionSpeedKph,
    string SuspensionRegime,
    string LinearMotorPropulsionCommand,
    bool IsLevitationStable
);

public interface IMaglevInductrackNullFluxEngine
{
    MaglevInductrackAnalysisResult AnalyzeLevitationState(MaglevInductrackTelemetry telemetry);
}
