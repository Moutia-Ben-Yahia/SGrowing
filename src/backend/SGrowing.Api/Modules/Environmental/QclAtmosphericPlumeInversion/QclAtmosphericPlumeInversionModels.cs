namespace SGrowing.Api.Modules.Environmental.QclAtmosphericPlumeInversion;

public sealed record QclPlumeTelemetry(
    string SensorPathId,
    string TargetMolecule,
    double OpticalPathLengthMeters,
    double QclCenterWavelengthMicrons,
    double MeasuredPathIntegratedPpmMeters,
    double CrosswindSpeedMetersPerSecond,
    double WindDirectionRelativeDegrees,
    double BeamScintillationRytovVariance,
    bool RetroreflectorAlignmentTrackingLocked
);

public sealed record QclPlumeAnalysisResult(
    string SensorPathId,
    double PathAveragedMixingRatioPpm,
    double InferredPlumeEmissionFluxKgPerHour,
    double AtmosphericTurbulenceAttenuationPercent,
    string EmissionDetectionRegime,
    string PlumeLocalizationAction,
    bool IsMeasurementValid
);

public interface IQclAtmosphericPlumeInversionEngine
{
    QclPlumeAnalysisResult AnalyzeGreenhousePlume(QclPlumeTelemetry telemetry);
}
